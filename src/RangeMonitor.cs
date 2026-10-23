using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PataponAccess;

internal static class RangeMonitor
{
    private static bool enabled,missionActive;
    private static long nextSample,nextHeartbeat,samples,errors,lastError;
    private static string last="";
    public static string CurrentStatus {get;private set;}=I18n.T("ui.ca39bb204b65","目前沒有可用的射程資料");
    private static string TargetStatus(bool inside)=>(inside?I18n.T("range.targets.in","攻擊目標在射程內"):I18n.T("range.targets.out","沒有偵測到射程內攻擊目標"))+I18n.T("ui.5c091b7a3aa3","。射程資訊不代表必定命中。");
    public static void Install(bool logging,bool cue,bool objectCue)
    {
        enabled=logging||cue;RangeCue.Enabled=cue;ObjectRangeCue.Initialize(false);
        if(!enabled)return;
        var h=new Harmony("org.patapon.access.range");
        foreach(string game in new[]{"P1S","P2S"})foreach(string method in new[]{"mainNormal","watchAlert"})try{
            var type=typeof(LauncherMain).Assembly.GetType(game+".Game.Mission.WatchGameMarch");
            h.Patch(type!.GetMethod(method),postfix:new HarmonyMethod(typeof(RangeMonitor).GetMethod(nameof(SampleRush),BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("range-hook-installed",new{game,method});
        }catch(Exception e){Monitor.Record("range-hook-failed",new{game,method,error=e.Message});}
        Patch(h,"mainNormal",nameof(Sample));
        Patch(h,"watchAlert",nameof(Sample));
        foreach(var method in new[]{"getNumAttackSightLongEnemy","getNumAttackSightShortEnemy","getNumAttackSightLongGimmick","getNumAttackSightShortGimmick"})Patch(h,method,nameof(CountObserved));
        foreach(string name in new[]{"mainNormal","watchAlert"}) {
            try { h.Patch(typeof(P2.Game.Mission.WatchGameMarch).GetMethod(name),postfix:new HarmonyMethod(typeof(RangeMonitor).GetMethod(nameof(SampleP2),BindingFlags.NonPublic|BindingFlags.Static))); }
            catch(Exception e){Monitor.Record("range-hook-failed",new{game="P2",method=name,error=e.Message});}
        }
        Monitor.Record("range-observer",new{game="P1",mode="native-read-only-getters",cueBand="LongEnemy",note="Counts are player-squad sight flags, not enemy counts or hit confirmation. Range meaning remains experimental."});
    }
    private static void Patch(Harmony h,string name,string callback)
    {
        try {
            var m=typeof(P1.Game.Mission.WatchGameMarch).GetMethod(name)!;
            h.Patch(m,postfix:new HarmonyMethod(typeof(RangeMonitor).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
            Reader.Log.LogInfo("Range hook: "+name);
            Monitor.Record("range-hook-installed",new{method=m.DeclaringType+"."+name,signature=m.ToString()});
        }catch(Exception e){Monitor.Record("range-hook-failed",new{method=name,error=e.ToString()});Reader.Log.LogWarning("Range hook: "+name+": "+e.Message);}
    }
    public static void Begin()
    {
        EnemyRangeGeometry.Begin();DirectEnemySight.Begin();CartRange.Begin();CurrentStatus=I18n.T("ui.82af2da4b341","正在讀取射程");missionActive=true;nextSample=nextHeartbeat=0;samples=errors=0;last="";RangeCue.Reset();ObjectRangeCue.Reset();
    }
    public static void End(){EnemyRangeGeometry.End();DirectEnemySight.End();CartRange.End();CurrentStatus=I18n.T("ui.02e9ab48a558","目前不在戰鬥中");missionActive=false;RangeCue.Reset();ObjectRangeCue.Reset();}
    private static void SampleRush(object __instance)
    {
        if(!enabled||!missionActive)return;
        long now=Environment.TickCount64;if(now<nextSample)return;nextSample=now+100;
        try{
            var type=__instance.GetType();string game=type.Namespace!.Split('.')[0];
            int Count(string name)=>Convert.ToInt32(type.GetMethod(name)!.Invoke(__instance,null));
            int le=Count("getNumAttackSightLongEnemy"),se=Count("getNumAttackSightShortEnemy");
            int lg=Count("getNumAttackSightLongGimmick"),sg=Count("getNumAttackSightShortGimmick");
            int max=game=="P1S"?3:4;if(new[]{le,se,lg,sg}.Any(n=>n<0||n>max))return;
            string signature=$"{game}:{le}:{se}:{lg}:{sg}";
            if(signature!=last||now>=nextHeartbeat){Monitor.Record("range-state",new{game,longEnemySquads=le,shortEnemySquads=se,longGimmickSquads=lg,shortGimmickSquads=sg});last=signature;nextHeartbeat=now+1000;}
            RangeCue.Observe(le>0||lg>0,le+lg,se+sg,game+"AttackableTargets");
            CurrentStatus=TargetStatus(le>0||lg>0);
            ObjectRangeCue.Observe(game,lg,sg);
        }catch(Exception e){if(now-lastError>5000){lastError=now;Monitor.Record("range-error",new{game="rush",error=e.Message});}}
    }
    private static long nativeCountCalls;
    private static void CountObserved(MethodBase __originalMethod,int __result)
    {
        nativeCountCalls++;
        // Full joint samples are logged below; this counter proves these hooks run.
    }
    private static void SampleP2(P2.Game.Mission.WatchGameMarch __instance)
    {
        if(!enabled||!missionActive)return;
        long now=Environment.TickCount64;if(now<nextSample)return;nextSample=now+100;
        try {
            int le=__instance.getNumAttackSightLongEnemy(),se=__instance.getNumAttackSightShortEnemy();
            int lg=__instance.getNumAttackSightLongGimmick(),sg=__instance.getNumAttackSightShortGimmick();
            if(le<0||le>4||se<0||se>4||lg<0||lg>4||sg<0||sg>4)throw new InvalidOperationException("P2 sight counts outside 0..4");
            string key=$"P2:{le}:{se}:{lg}:{sg}";
            if(key!=last||now>=nextHeartbeat){Monitor.Record("range-state",new{game="P2",longEnemySquads=le,shortEnemySquads=se,longGimmickSquads=lg,shortGimmickSquads=sg});last=key;nextHeartbeat=now+1000;}
            CurrentStatus=TargetStatus(le>0||lg>0);
            RangeCue.Observe(le>0||lg>0,le+lg,se+sg,"P2AttackableTargets");
            ObjectRangeCue.Observe("P2",lg,sg);
        }catch(Exception e){if(now-lastError>10000){lastError=now;Monitor.Record("range-error",new{game="P2",error=e.Message});}}
    }
    private static void Sample(P1.Game.Mission.WatchGameMarch __instance)
    {
        if(!enabled||!missionActive)return;
        long now=Environment.TickCount64;if(now<nextSample)return;nextSample=now+100;
        try {
            int le=__instance.getNumAttackSightLongEnemy(),se=__instance.getNumAttackSightShortEnemy();
            int lg=__instance.getNumAttackSightLongGimmick(),sg=__instance.getNumAttackSightShortGimmick();
            samples++;
            if(le<0||le>3||se<0||se>3||lg<0||lg>3||sg<0||sg>3)throw new InvalidOperationException("Sight flag count outside expected 0..3");
            string key=$"{le}:{se}:{lg}:{sg}";
            if(key!=last||now>=nextHeartbeat) {
                Monitor.Record("range-state",new{game="P1",longEnemySquads=le,shortEnemySquads=se,longGimmickSquads=lg,shortGimmickSquads=sg,changed=key!=last,samples,nativeCountCalls,errors,meaning="Native sight flags; experimental range interpretation"});
                last=key;nextHeartbeat=now+1000;
            }
            bool direct=DirectEnemySight.Inside(now)||EnemyRangeGeometry.Inside(now);
            CurrentStatus=TargetStatus(le>0||direct||lg>0);
            RangeCue.Observe(le>0||direct||lg>0,le+lg,se+sg,"P1AttackableTargets");
            ObjectRangeCue.Observe("P1",lg,sg);
        }catch(Exception e){errors++;if(now-lastError>10000){lastError=now;Monitor.Record("range-error",new{error=e.ToString(),samples,errors});}}
    }
}

internal static class RangeCue
{
    public static bool Enabled;
    public static long LastPlaybackAt {get;private set;}
    private static readonly RangeTransition state=new();
    private static long lastSound,requestedAt;
    private static int requested;
    private static string requestedBand="LongEnemy";
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string path,IntPtr module,uint flags);
    public static void Reset(){state.Reset();System.Threading.Interlocked.Exchange(ref requested,0);}
    public static void Observe(bool value,int longCount,int shortCount,string band="LongEnemy")
    {
        long now=Environment.TickCount64;
        int change=state.Observe(value,now);
        if(change>0){
            Monitor.Record("enemy-range-enter",new{longCount,shortCount,band,experimental=true});
            if(Enabled&&now-lastSound>=1500){lastSound=now;requestedAt=now;requestedBand=band;System.Threading.Interlocked.Exchange(ref requested,1);}
        }else if(change<0){Monitor.Record("enemy-range-leave",new{band});if(Enabled){requestedAt=now;requestedBand=band;System.Threading.Interlocked.Exchange(ref requested,-1);}}
    }
    public static void Scan()
    {
        int direction=System.Threading.Interlocked.Exchange(ref requested,0);
        if(direction==0)return;
        if(Environment.TickCount64-requestedAt>1000)return;
        string band=requestedBand;
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,direction>0?"enemy-range.wav":"enemy-range-leave.wav");
        bool success=File.Exists(file)&&CueAudio.Play(file);
        if(success)LastPlaybackAt=Environment.TickCount64;
        Monitor.Record("enemy-range-cue",new{success,direction=direction>0?"enter":"leave",band,experimental=true});
        Reader.Log.LogInfo("Enemy range cue "+(direction>0?"enter":"leave")+" playback="+success);
    }
}





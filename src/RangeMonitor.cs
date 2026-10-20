using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PataponAccess;

internal static class RangeMonitor
{
    private static bool enabled,missionActive;
    private static long nextSample,nextHeartbeat,samples,errors,lastError;
    private static string last="";
    public static string CurrentStatus {get;private set;}="目前沒有可用的射程資料";
    public static void Install(bool logging,bool cue)
    {
        enabled=logging||cue;RangeCue.Enabled=cue;
        if(!enabled)return;
        var h=new Harmony("org.patapon.access.range");
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
        EnemyRangeGeometry.Begin();DirectEnemySight.Begin();CartRange.Begin();CurrentStatus="正在讀取射程";missionActive=true;nextSample=nextHeartbeat=0;samples=errors=0;last="";RangeCue.Reset();
    }
    public static void End(){EnemyRangeGeometry.End();DirectEnemySight.End();CartRange.End();CurrentStatus="目前不在戰鬥中";missionActive=false;RangeCue.Reset();}
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
            CurrentStatus=(le>0?"敵方部隊在攻擊範圍內":"沒有偵測到射程內敵方部隊")+"。"+
                (lg>0?"可攻擊場景物件在攻擊範圍內":"沒有偵測到射程內可攻擊場景物件")+"。射程資訊不代表必定命中。";
            RangeCue.Observe(P2RangeTargets.Inside(le,lg),le,se,"P2LongEnemyOrGimmick");
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
                Monitor.Record("range-state",new{longEnemySquads=le,shortEnemySquads=se,longGimmickSquads=lg,shortGimmickSquads=sg,changed=key!=last,samples,nativeCountCalls,errors,meaning="Native sight flags; experimental range interpretation"});
                last=key;nextHeartbeat=now+1000;
            }
            bool direct=DirectEnemySight.Inside(now)||EnemyRangeGeometry.Inside(now);
            CurrentStatus=(le>0||direct?"敵方部隊在攻擊範圍內":"沒有偵測到射程內敵方部隊")+"。"+(lg>0?"場景物件也在攻擊範圍內":"沒有偵測到射程內場景物件")+"。這是一般目標資訊；馬車另以三聲鐘音提示。";
            RangeCue.Observe(le>0||direct,le,se);
        }catch(Exception e){errors++;if(now-lastError>10000){lastError=now;Monitor.Record("range-error",new{error=e.ToString(),samples,errors});}}
    }
}

internal static class RangeCue
{
    public static bool Enabled;
    public static long LastPlaybackAt {get;private set;}
    private static bool inside;
    private static long trueSince,falseSince,lastSound,requestedAt;
    private static int requested;
    private static string requestedBand="LongEnemy";
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string path,IntPtr module,uint flags);
    public static void Reset(){inside=false;trueSince=falseSince=0;System.Threading.Interlocked.Exchange(ref requested,0);}
    public static void Observe(bool value,int longCount,int shortCount,string band="LongEnemy")
    {
        long now=Environment.TickCount64;
        if(value) {
            falseSince=0;if(trueSince==0)trueSince=now;
            if(!inside&&now-trueSince>=200) {
                inside=true;
                Monitor.Record("enemy-range-enter",new{longCount,shortCount,band,experimental=true});
                if(Enabled&&now-lastSound>=1500){lastSound=now;requestedAt=now;requestedBand=band;System.Threading.Interlocked.Exchange(ref requested,1);}
            }
        }else {
            trueSince=0;if(falseSince==0)falseSince=now;
            if(inside&&now-falseSince>=500){inside=false;Monitor.Record("enemy-range-leave",new{band});if(Enabled){requestedAt=now;requestedBand=band;System.Threading.Interlocked.Exchange(ref requested,-1);}}
        }
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





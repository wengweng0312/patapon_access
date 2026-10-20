using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class CannonWarning
{
    private static readonly CannonWarningState state=new();
    private static readonly Dictionary<IntPtr,uint> motions=new();
    public static void BeginMission(){state.Reset();motions.Clear();}
    public static void Observe(string source,string text)
    {
        if(!state.Observe(BattleMonitor.Active,source,text))return;
        Monitor.Record("p2-cannon-preparation",new{source,text,trigger="native cannon charging dialogue",leadTimeVerified=false});
        BossAttackCue.Special(new IntPtr(-21003),"p2-cannon-brothers",0,"verified-cannon-charging-dialogue");
    }
    public static void InstallTrace(bool diagnostics)
    {
        if(!diagnostics)return;
        try {
            var method=typeof(P2.Game.Gimmick.Model).GetMethod("playMotion")!;
            new Harmony("org.patapon.access.p2-cannon-trace").Patch(method,postfix:new HarmonyMethod(typeof(CannonWarning).GetMethod(nameof(Motion),BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("p2-gimmick-motion-hook",new{method="Model.playMotion"});
        }catch(Exception e){Reader.Log.LogWarning("Cannon trace: "+e.Message);}
    }
    private static void Motion(P2.Game.Gimmick.Model __instance,uint __0,float __1,bool __2,bool __3)
    {
        if(!BattleMonitor.Active)return;
        try {
            var key=__instance.Pointer;if(motions.TryGetValue(key,out uint old)&&old==__0)return;
            if(motions.Count>128)motions.Clear();motions[key]=__0;
            var desc=__instance.modelDesc_;
            Monitor.Record("p2-gimmick-motion-request",new{model=desc?.mainModelName,subModel=desc?.subModelName,motion=__0,waitTime=float.IsFinite(__1)?(float?)__1:null,loop=__2,clear=__3});
        }catch(Exception e){Monitor.Record("p2-gimmick-motion-error",new{error=e.Message});}
    }
}

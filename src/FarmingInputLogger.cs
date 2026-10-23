using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class FarmingInputLogger
{
    private sealed record Scope(IntPtr Analyzer,string Game,P2.System.Sound.BeatTimer? Judge,P2.System.Sound.BeatTimer? Music);
    [ThreadStatic] private static Scope? scope;
    public static void Install(Harmony hooks)
    {
        HarmonyMethod Callback(string name)=>new(typeof(FarmingInputLogger).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static));
        hooks.Patch(typeof(P2.Sound.SubGame.Bean.Command).GetMethod("update"),prefix:Callback(nameof(Enter)),postfix:Callback(nameof(Score)),finalizer:Callback(nameof(Exit)));
        // The inlined Bean input path calls this substantial native body. Scope
        // by its analyzer pointer so other minigames/shared aliases are ignored.
        hooks.Patch(typeof(P2.Sound.SubGame.Script.Analyzer).GetMethod("hitCheck"),prefix:Callback(nameof(BeforeHit)),postfix:Callback(nameof(AfterHit)));
    }
    public static void InstallDance(Harmony hooks)
    {
        HarmonyMethod Callback(string name)=>new(typeof(FarmingInputLogger).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static));
        hooks.Patch(typeof(P2.Sound.SubGame.Bell.Command).GetMethod("update"),prefix:Callback(nameof(EnterDance)),postfix:Callback(nameof(ScoreDance)),finalizer:Callback(nameof(Exit)));
    }
    private static void Enter(P2.Sound.SubGame.Bean.Command __instance,P2.System.Sound.BeatTimer __1,out Scope? __state)
    {__state=scope;try{scope=new(__instance.key_?.Pointer??IntPtr.Zero,"farming",__instance.commandBeatTimer_??__1,__instance.director_?.beatTimer_);}catch{scope=null;}}
    private static void EnterDance(P2.Sound.SubGame.Bell.Command __instance,P2.System.Sound.BeatTimer __1,out Scope? __state)
    {__state=scope;try{scope=new(__instance.key_?.Pointer??IntPtr.Zero,"dance",__1,__instance.director_?.beatTimer_);}catch{scope=null;}}
    private static void Exit(Scope? __state)=>scope=__state;
    private sealed record Hit(string Game,int Expected,uint Count,uint Current,uint Group,uint List,double At,object? Judge,object? Music);
    private static void BeforeHit(P2.Sound.SubGame.Script.Analyzer __instance,out Hit? __state)
    {
        __state=null;if(scope==null||scope.Analyzer==IntPtr.Zero||__instance.Pointer!=scope.Analyzer)return;
        try {
            var c=__instance.cursor;var commands=c?.commandList?.command;uint index=c?.curCount??uint.MaxValue;int expected=int.MinValue;
            if(commands!=null&&index<commands.Length){var hits=commands[(int)index]?.hit;uint user=__instance.userId_;if(hits!=null&&user<hits.Length)expected=hits[(int)user];}
            if(c!=null)__state=new(scope.Game,expected,c.count,c.curCount,c.groupCount,c.listCount,MinigameClock.NowMs,MinigameClock.Snapshot(scope.Judge),MinigameClock.Snapshot(scope.Music));
        }catch(Exception e){Monitor.Record("farming-input-log-error",new{error=e.Message});}
    }
    private static void AfterHit(int __0,P2.Sound.SubGame.Script.Analyzer.HitResult __result,Hit? __state)
    {if(__state!=null)Monitor.Record(__state.Game+"-input-result",new{percussion=__0,expected=__state.Expected,count=__state.Count,current=__state.Current,group=__state.Group,list=__state.List,result=(int)__result,observed_time=__state.At,judgement_clock=__state.Judge,music_clock=__state.Music,time_unit="milliseconds",clock_source="Stopwatch-monotonic",source="native-Analyzer.hitCheck-scoped-to-"+__state.Game});}
    private static string lastScore="";
    private static void Score(P2.Sound.SubGame.Bean.Command __instance)
    {
        try {
            var main=__instance.mainScorer_;var sub=__instance.subScorer_;if(main==null||sub==null)return;
            string signature=$"{__instance.Pointer}:{main.okCount_}:{main.ngCount_}:{main.allCount_}:{sub.okCount_}:{sub.ngCount_}:{sub.allCount_}";
            if(signature==lastScore)return;lastScore=signature;
            Monitor.Record("farming-score-result",new{ok=main.okCount_,ng=main.ngCount_,all=main.allCount_,currentOk=sub.okCount_,currentNg=sub.ngCount_,currentAll=sub.allCount_,ended=__instance.isEnd_});
        }catch(Exception e){Monitor.Record("farming-input-log-error",new{error=e.Message});}
    }
    private static string lastDanceScore="";
    private static void ScoreDance(P2.Sound.SubGame.Bell.Command __instance)
    {
        try{
            var main=__instance.mainScorer_;var sub=__instance.subScorer_;if(main==null||sub==null)return;
            string signature=$"{__instance.Pointer}:{main.okCount_}:{main.ngCount_}:{main.allCount_}:{sub.okCount_}:{sub.ngCount_}:{sub.allCount_}";
            if(signature==lastDanceScore)return;lastDanceScore=signature;
            Monitor.Record("dance-score-result",new{ok=main.okCount_,ng=main.ngCount_,all=main.allCount_,currentOk=sub.okCount_,currentNg=sub.ngCount_,currentAll=sub.allCount_,observed_time=MinigameClock.NowMs});
        }catch(Exception e){Monitor.Record("dance-input-log-error",new{error=e.Message});}
    }
}

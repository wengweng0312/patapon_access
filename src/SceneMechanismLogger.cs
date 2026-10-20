using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class SceneMechanismLogger
{
    private static readonly Dictionary<IntPtr,(string state,long at)> snapshots=new();
    private static readonly Dictionary<IntPtr,long> sampledAt=new();
    private static long lastError;
    public static void BeginMission(){snapshots.Clear();sampledAt.Clear();DrumPuzzleAccess.Clear();}
    public static void Install(bool diagnostics)
    {
        // Runtime puzzle reading remains available when diagnostics are disabled.
        var h=new Harmony("org.patapon.access.scene-mechanism-log");
        Hook(h,typeof(P2.Game.Gimmick.Gimmick),"update",nameof(Update));
        Hook(h,typeof(P2.Game.Gimmick.Gimmick),"enable",nameof(Enabled));
        Hook(h,typeof(P2.Game.Gimmick.Gimmick),"enable2d",nameof(TwoDimensional));
        Hook(h,typeof(P2.Game.Map.Event.Collision),"callScriptLabel",nameof(Event));
    }
    private static void Hook(Harmony h,Type type,string method,string callback)
    {
        try {
            var m=type.GetMethod(method)??throw new MissingMethodException(method);
            if(m.GetParameters().Any(p=>p.ParameterType.IsByRef))throw new InvalidOperationException("By-ref mechanism hook rejected");
            h.Patch(m,postfix:new HarmonyMethod(typeof(SceneMechanismLogger).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("scene-mechanism-hook",new{type=type.FullName,method});
        }catch(Exception e){Reader.Log.LogWarning("Mechanism logger hook: "+e.Message);}
    }
    private static void Error(Exception e)
    {long now=Environment.TickCount64;if(now-lastError>=5000){lastError=now;Monitor.Record("scene-mechanism-error",new{error=e.Message});}}
    private static void Update(P2.Game.Gimmick.Gimmick __instance)=>Sample(__instance,"update");
    private static void Enabled(P2.Game.Gimmick.Gimmick __instance,bool __0)=>Sample(__instance,"enable",__0);
    private static void TwoDimensional(P2.Game.Gimmick.Gimmick __instance,bool __0)=>Sample(__instance,"enable2d",__0);
    private static void Event(P2.Game.Map.Event.Collision __instance,int __0,int __1,int __2,bool __result)
    {
        if(!BattleMonitor.Active)return;
        try {
            var gimmick=__instance.TryCast<P2.Game.Gimmick.Gimmick>();
            string? name=gimmick?.layoutParam_?.name;
            if(!MechanismNames.IsDrumPuzzle(name))return;
            Monitor.Record("scene-mechanism-event",new{name,scriptLabel=__0,userId=__1,userId2=__2,result=__result});
            Sample(gimmick!,"script-event");
        }catch(Exception e){Error(e);}
    }
    private static void Sample(P2.Game.Gimmick.Gimmick gimmick,string source,bool? requested=null)
    {
        if(!BattleMonitor.Active)return;
        try {
            var layout=gimmick.layoutParam_;string? name=layout?.name;
            if(!MechanismNames.IsDrumPuzzle(name))return;
            var key=gimmick.Pointer;long now=Environment.TickCount64;
            if(source=="update"&&sampledAt.TryGetValue(key,out long sample)&&now-sample<100)return;
            if(sampledAt.Count>128)sampledAt.Clear();sampledAt[key]=now;
            var p=gimmick.position_;
            DrumPuzzleAccess.Observe(name!,gimmick.isEnable_,p.x,p.y);
            bool enabled=gimmick.isEnable_,viewable=gimmick.isViewable_,twoD=gimmick.is2d();
            string state=$"{p.x:F1}:{p.y:F1}:{enabled}:{viewable}:{twoD}:{gimmick.isHit_}:{gimmick.isInnerTarget_}:{gimmick.prevTargetType_}:{gimmick.prevTargetId_}";
            bool found=snapshots.TryGetValue(key,out var old),changed=!found||state!=old.state;
            if(source!="script-event"&&!changed&&now-old.at<5000)return;
            if(snapshots.Count>128)snapshots.Clear();snapshots[key]=(state,now);
            Monitor.Record("scene-mechanism-state",new{name,role=MechanismNames.Role(name!),parameter=layout?.paramName,source,requested,enabled,viewable,twoD,hit=gimmick.isHit_,inside=gimmick.isInnerTarget_,x=float.IsFinite(p.x)?(float?)p.x:null,y=float.IsFinite(p.y)?(float?)p.y:null,changed,note="Raw native flags; viewable alone does not prove the glyph is rendered or near the player."});
        }catch(Exception e){Error(e);}
    }
}

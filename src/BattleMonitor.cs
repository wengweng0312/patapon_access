using HarmonyLib;
using System.Reflection;

namespace PataponAccess;

// Deliberately observe only primitive arguments and invocation counts.
// Native object fields may be invalid even when their managed wrapper is non-null.
internal static class BattleMonitor
{
    private static bool enabled;
    private static long checks,possible,unitCollisions,gimmickCollisions,nextHealth,lastSample;
    private static bool? lastPossible;
    public static bool Active {get;private set;}
    public static int Mission {get;private set;}
    public static void Install(bool value)
    {
        enabled=value;
        var h=new Harmony("org.patapon.access.battle-observation");
        Patch(h,typeof(P2.MainGame.Mission.MissionScene),"initialize",nameof(MissionStart));
        Patch(h,typeof(P2.MainGame.Mission.MissionScene),"terminate",nameof(MissionEnd));
        // Mission lifecycle is functional even when diagnostic recording is disabled.
        if(enabled) {
        Patch(h,typeof(P2.Game.Unit.BehaviorCtrl),"behaviorFunc_isPossibleAttack",nameof(AttackCheck));
        Patch(h,typeof(P2.Game.Unit.UnitBase),"checkHit",nameof(UnitCollision));
        Patch(h,typeof(P2.Game.Gimmick.Gimmick),"checkHit",nameof(GimmickCollision));
        Patch(h,typeof(P1.Game.Unit.BehaviorCtrl),"behaviorFunc_isPossibleAttack",nameof(AttackCheck));
        Patch(h,typeof(P1.Game.Unit.UnitBase),"checkHit",nameof(UnitCollision));
        Patch(h,typeof(P1.Game.Gimmick.Gimmick),"checkHit",nameof(GimmickCollision));
        Patch(h,typeof(P1.Game.Map.Weather.Controller),"changeWeather",nameof(WeatherChange));
        }
        Patch(h,typeof(P1.MainGame.Mission.MissionScene),"initialize",nameof(MissionStart));
        Patch(h,typeof(P1.MainGame.Mission.MissionScene),"terminate",nameof(MissionEnd));
        Monitor.Record("battle-observer",new{mode="primitive-values-only",game="P1",note="No native object fields read. Collision counts are not hit or damage confirmation."});
    }
    private static void Patch(Harmony h,Type type,string name,string callback)
    {
        try {
            var m=type.GetMethod(name)??throw new MissingMethodException(name);
            if(m.ReturnType.IsByRef || m.GetParameters().Any(p=>p.ParameterType.IsByRef || (p.ParameterType.IsValueType&&!p.ParameterType.IsPrimitive&&!p.ParameterType.IsEnum)))throw new InvalidOperationException("Unsupported signature");
            h.Patch(m,postfix:new HarmonyMethod(typeof(BattleMonitor).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("battle-hook-installed",new{method=type.FullName+"."+name,signature=m.ToString()});
        }catch(Exception e){Monitor.Record("battle-hook-failed",new{method=type.FullName+"."+name,error=e.Message});}
    }
    private static void AttackCheck(float __4,bool __result)
    {
        checks++;if(__result)possible++;
        long now=Environment.TickCount64;
        // Shared stream: cannot attribute a result to a specific actor or faction.
        if(now-lastSample<100)return;
        if(lastPossible==__result&&now-lastSample<1000)return;
        lastSample=now;lastPossible=__result;
        Monitor.Record("attack-check-sample",new{possibleAttack=__result,rangeArgument=float.IsFinite(__4)?(float?)__4:null,checks,possible,scope="unattributed; may include either faction"});
    }
    private static void UnitCollision(){unitCollisions++;}
    private static void GimmickCollision(){gimmickCollisions++;}
    private static void WeatherChange(uint __0,float __1,P1.Game.Map.Weather.ParamId __2)
        =>Monitor.Record("weather-request",new{weatherId=__0,transitionSeconds=float.IsFinite(__1)?(float?)__1:null,source=__2.ToString(),note="Requested weather only; actual wind and rain not read."});
    private static void MissionStart(MethodBase __originalMethod){MiracleAccess.Clear();Active=true;Mission++;RangeMonitor.Begin();RewardAccess.BeginMission();CannonWarning.BeginMission();SceneMechanismLogger.BeginMission();lastPossible=null;Monitor.Record("mission-start",new{mission=Mission,game=__originalMethod.DeclaringType!.Namespace!.Split('.')[0]});}
    private static void MissionEnd(){CampHintAccess.NewVisit();MiracleAccess.Clear();Active=false;RangeMonitor.End();RewardAccess.EndMission();Monitor.Record("mission-end",new{mission=Mission});}
    public static void Health()
    {
        if(!enabled||Environment.TickCount64<nextHealth)return;
        nextHealth=Environment.TickCount64+10000;
        Monitor.Record("battle-observer-health",new{checks,possible,unitCollisionCalls=unitCollisions,gimmickCollisionCalls=gimmickCollisions,mode="primitive-values-only"});
    }
}




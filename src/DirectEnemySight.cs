using P1.Game.Unit;
namespace PataponAccess;
internal static class DirectEnemySight
{
    private static bool active;
    private static readonly Dictionary<(IntPtr,bool),(bool value,long seen)> samples=new();
    private static long nextLog;
    public static void Begin(){active=true;samples.Clear();nextLog=0;}
    public static void End(){active=false;samples.Clear();}
    public static void Observe(UnitSquad squad,SquadCtrl.SquadCtrlType ctrl,int target,bool collision,bool result)
    {
        // Native isInsideView branches: 3=gimmicks, 5=opposing troop, not arbitrary scenery.
        if(!active||target!=5||ctrl!=SquadCtrl.SquadCtrlType.SquadCtrlType_Attack)return;
        try {
            if(!squad.isActive_||squad.pUnitTroop_?.troopInfo_?.troopType!=TroopType.TroopType_Player)return;
            long now=Environment.TickCount64;var key=(squad.Pointer,collision);
            bool changed=!samples.TryGetValue(key,out var old)||old.value!=result;
            if(samples.Count>64)samples.Clear();
            samples[key]=(result,now);
            if(changed||now>=nextLog){nextLog=now+1000;Monitor.Record("direct-enemy-sight",new{squad=key.Item1.ToInt64(),collision,result,target,control=ctrl.ToString(),experimental=true});}
        }catch(Exception e){Monitor.Record("direct-enemy-sight-error",new{error=e.Message});}
    }
    public static bool Inside(long now)=>samples.Values.Any(s=>s.value&&now-s.seen<=600);
}

using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class P1TotemAccess
{
    public static readonly DrumPuzzleState State=new();
    private static readonly Dictionary<uint,string> profiles=new();
    private static readonly HashSet<IntPtr> scripts=new();
    private static long nextObject,nextPlayer,lastError,nextStateLog;
    private static string lastState="";
    public static void Clear(){State.Clear();profiles.Clear();scripts.Clear();nextObject=nextPlayer=nextStateLog=0;lastState="";}
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.p1-totem");
        Hook(h,typeof(P1.Game.Talk.CommandMap),"getGimmickPosition",nameof(Script));
        Hook(h,typeof(P1.Game.Gimmick.Gimmick),"update",nameof(Object));
        Hook(h,typeof(P1.Game.Unit.FlagUnit),"update",nameof(Player));
    }
    private static void Hook(Harmony h,Type type,string method,string callback)
    {
        try{var m=type.GetMethod(method)!;if(m.GetParameters().Any(p=>p.ParameterType.IsByRef))return;
            h.Patch(m,postfix:new HarmonyMethod(typeof(P1TotemAccess).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("p1-totem-hook",new{type=type.FullName,method});
        }catch(Exception e){Error(e);}
    }
    private static void Error(Exception e){long now=Environment.TickCount64;if(now-lastError<5000)return;lastError=now;Monitor.Record("p1-totem-error",new{error=e.Message});}
    private static void Script(P1.Pyd.Script.Talk.Controller __0)
    {
        if(!BattleMonitor.Active)return;
        try{
            var contexts=__0.context_;int i=__0.contextIndex_;
            if(contexts==null||i<0||i>=contexts.Length)return;
            var buffer=contexts[i]?.buffer;
            if(buffer==null||buffer.Length>2_000_000||scripts.Contains(buffer.Pointer))return;
            if(scripts.Count>=32)return;scripts.Add(buffer.Pointer);
            var parsed=P1TotemScript.Read(buffer.ToArray());
            foreach(var p in parsed){profiles[p.LayoutId]=p.Sequence;Monitor.Record("p1-totem-script-profile",new{layoutId=p.LayoutId,sequence=p.Sequence,source="native-script-state-machine"});}
        }catch(Exception e){Error(e);}
    }
    private static void Object(P1.Game.Gimmick.Gimmick __instance)
    {
        if(!BattleMonitor.Active)return;
        try{
            WorldObjectLog.Observe(__instance);
            if(__instance.layoutParam_?.paramName!="TOTEMPOLE")return;
            long now=Environment.TickCount64;if(now<nextObject)return;nextObject=now+100;
            uint id=__instance.id_;var pos=__instance.position_;
            string sequence=profiles.TryGetValue(id,out var text)?text:"";
            State.ObserveBody(__instance.isEnable_,pos.x,pos.y,sequence,now);
            string snapshot=$"{id}:{__instance.isEnable_}:{pos.x:F1}:{pos.y:F1}:{sequence}";
            if(snapshot!=lastState||now>=nextStateLog){lastState=snapshot;nextStateLog=now+5000;
                Monitor.Record("p1-totem-state",new{layoutId=id,enabled=__instance.isEnable_,x=float.IsFinite(pos.x)?(float?)pos.x:null,y=float.IsFinite(pos.y)?(float?)pos.y:null,sequence,profileAvailable=sequence.Length>0});}
        }catch(Exception e){Error(e);}
    }
    private static void Player(P1.Game.Unit.FlagUnit __instance)
    {
        if(!BattleMonitor.Active)return;
        long now=Environment.TickCount64;if(now<nextPlayer)return;nextPlayer=now+100;
        try{
            var troop=__instance.pUnitTroop_;
            if(troop?.troopInfo_?.troopType!=P1.Game.Unit.TroopType.TroopType_Player)return;
            var top=troop.getUnitTop();var flag=__instance.flagUnitModel_;
            if(top==null||flag==null||top.unitModel_==null||!top.isActive_||top.isDead_)return;
            State.ObservePlayer(flag.pos_.x,top.unitModel_.pos_.x,now);
        }catch(Exception e){Error(e);}
    }
}

using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using System.Reflection;
using System.Text.RegularExpressions;
namespace PataponAccess;
internal static class P2BossAccess
{
    private static readonly Regex family=new(@"^unit2\d\d_",RegexOptions.CultureInvariant);
    private static readonly Dictionary<IntPtr,(uint motion,int action,int phase,int address)> previous=new();
    private static long lastError;
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.p2-boss");
        Patch(h,typeof(P2.GameSystem.Actor.Script.ScriptCommand_Actor),"setMotion",nameof(ActorMotion));
        Patch(h,typeof(P2.GameSystem.Actor.Script.ScriptCommand_Actor),"resetAttackUniqueId",nameof(ActorAttack));
        Patch(h,typeof(P2.Game.Talk.CommandUnitBase),"setMotion",nameof(UnitMotion));
        Patch(h,typeof(P2.Game.Talk.CommandUnitBase),"resetAttackUniqueId",nameof(UnitAttack));
        Monitor.Record("boss-observer",new{game="P2",scope="shared unitbase attack-phase startup structure; shark timer-helper variant; attack-frame markers remain diagnostic only"});
    }
    private static void Patch(Harmony h,Type type,string method,string callback)
    {
        try {h.Patch(type.GetMethod(method),postfix:new HarmonyMethod(typeof(P2BossAccess).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));Monitor.Record("boss-script-hook",new{game="P2",type=type.FullName,method});}
        catch(Exception e){Reader.Log.LogWarning("P2 boss hook: "+e.Message);}
    }
    private static void ActorMotion(P2.GameSystem.Actor.Script.ScriptCommand_Actor __instance,P2.Pyd.Script.Talk.Controller __0)=>Observe(__instance.pActorObj_,__0,false);
    private static void ActorAttack(P2.GameSystem.Actor.Script.ScriptCommand_Actor __instance,P2.Pyd.Script.Talk.Controller __0)=>Observe(__instance.pActorObj_,__0,true);
    private static void UnitMotion(P2.Game.Talk.CommandUnitBase __instance,P2.Pyd.Script.Talk.Controller __0)=>Observe(__instance.pUnitBase_,__0,false);
    private static void UnitAttack(P2.Game.Talk.CommandUnitBase __instance,P2.Pyd.Script.Talk.Controller __0)=>Observe(__instance.pUnitBase_,__0,true);
    private static void Observe(P2.GameSystem.Actor.ActorObj? actor,P2.Pyd.Script.Talk.Controller controller,bool attack)
    {
        if(!BattleMonitor.Active)return;
        try {
            string? unit=actor?.TryCast<P2.Game.Unit.UnitBase>()?.getCharaParam()?.name;
            if(unit==null||!family.IsMatch(unit))return;
            var model=actor!.pActorModel_;var vars=controller.variable_?.integerArray_;
            var contexts=controller.context_;int index=controller.contextIndex_;
            if(model==null||vars==null||vars.Length<=0x21||contexts==null||index<0||index>=contexts.Length)return;
            int action=vars[0x20].i,phase=vars[0x21].i;
            var buffer=contexts[index]?.buffer;var pack=controller.pack_;
            if(buffer==null||pack==null||buffer.Length<8)return;
            int address=pack.adr;if(address<0||address>buffer.Length)return;
            var key=model.Pointer;uint motion=model.motionId_;
            var state=(motion,action,phase,address);
            byte[] header=new byte[8],continuation=new byte[Math.Min(128,buffer.Length-address)];
            for(int i=0;i<header.Length;i++)header[i]=buffer[i];
            for(int i=0;i<continuation.Length;i++)continuation[i]=buffer[address+i];
            bool entry=!attack&&P2BossRules.IsEntry(unit,action,phase,address,header,continuation);
            if(!attack&&!entry&&previous.TryGetValue(key,out var old)&&old==state)return;
            if(previous.Count>128)previous.Clear();previous[key]=state;
            Monitor.Record(attack?"boss-attack-marker":"boss-script-motion",new{game="P2",unit,motion,action,phase,packAddress=address,attackEntry=entry});
            if(entry)BossAttackCue.Special(key,unit,(int)motion,"p2-shared-script-preparation");
        }catch(Exception e){long now=Environment.TickCount64;if(now-lastError>5000){lastError=now;Monitor.Record("boss-script-error",new{game="P2",error=e.Message});}}
    }
}

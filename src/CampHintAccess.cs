using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class CampHintAccess
{
    private static readonly CampHintMemory memory=new();
    private static IntPtr owner;
    private static long nextRead;
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.camp-hints");
        // getColonyMessage is a tiny inlined array getter in this build. Observe
        // the current render owner instead, never detour the by-ref update method.
        try {h.Patch(typeof(P2.Bases.Camp.Controller).GetMethod("render"),prefix:new HarmonyMethod(typeof(CampHintAccess).GetMethod(nameof(Read),BindingFlags.Static|BindingFlags.NonPublic)));Monitor.Record("camp-hint-hook",new{game="P2",source="Controller.render",messageIds=new[]{162,166,167,174}});}
        catch(Exception e){Reader.Log.LogWarning("Camp hint observer: "+e.Message);Monitor.Record("camp-hint-hook-error",new{error=e.Message});}
    }
    private static void Read(P2.Bases.Camp.Controller __instance)
    {
        try{
            long now=Environment.TickCount64;
            if(owner==__instance.Pointer&&now<nextRead)return;
            nextRead=now+2000;
            var list=__instance.colonyMessagePtr_;if(list==null)return;
            foreach(uint id in new uint[]{162,166,167,174})if(id<list.Length)Message(__instance,id,list[(int)id]);
        }catch(Exception e){Monitor.Record("camp-hint-error",new{error=e.Message});}
    }
    private static void Message(P2.Bases.Camp.Controller __instance,uint __0,string __result)
    {
        owner=__instance.Pointer;
        if(!CampHintRules.IsProtected("P2",__0)||string.IsNullOrWhiteSpace(__result))return;
        string key="P2:colony:"+__0;
        if(!memory.Record(__instance.Pointer,__0,__result))return;
        Monitor.Record("camp-hint-template",new{game="P2",messageId=__0,key,template=__result});
    }
    public static bool Identify(string game,string raw,out string key)=>memory.Identify(game,raw,out key);
    public static bool WasSpoken(string key)=>memory.WasSpoken(key);
    public static void Offer(string key,string text)=>memory.Offer(key,text,Environment.TickCount64);
    public static void Dispatched(string text,bool accepted)=>memory.Dispatched(text,accepted,Environment.TickCount64);
    public static void NewVisit()=>memory.NewVisit();
}

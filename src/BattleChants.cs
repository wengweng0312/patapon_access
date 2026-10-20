using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class BattleChants
{
    private static ConfigEntry<bool>? setting;
    private static readonly Dictionary<string,long> suppressedVisible=new();
    [ThreadStatic] private static int flagDepth;
    [ThreadStatic] private static int playerDepth;
    private static readonly HashSet<string> unknownSources=new();
    public static bool Enabled=>setting?.Value??false;
    public static string Source=>flagDepth>0?"flag-unit":playerDepth>0?"player-unit":"unclassified";
    public static void Configure(ConfigEntry<bool> value){setting=value;}
    public static string Toggle(){if(setting!=null)setting.Value=!setting.Value;return Enabled?I18n.T("ui.e6e68a82079a","已開啟戰鬥口號朗讀"):I18n.T("ui.4c9fe0c4df87","已關閉戰鬥口號朗讀");}
    public static void Install(Harmony h)
    {
        foreach(string ns in new[]{"P1","P2"})try {
            var type=typeof(LauncherMain).Assembly.GetType(ns+".Game.Unit.FlagUnit");
            var method=type?.GetMethod("callMessageWindowW");if(method==null||method.GetParameters().Any(p=>p.ParameterType.IsByRef))continue;
            h.Patch(method,prefix:new HarmonyMethod(typeof(BattleChants).GetMethod(nameof(Enter),BindingFlags.NonPublic|BindingFlags.Static)),finalizer:new HarmonyMethod(typeof(BattleChants).GetMethod(nameof(Leave),BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("battle-chant-scope-hook",new{game=ns,method=method.Name});
        }catch(Exception e){Reader.Log.LogWarning("Battle chant scope: "+e.Message);}
        PlayerHook(h,typeof(P1.Game.Unit.UnitBase),nameof(PlayerP1));
        PlayerHook(h,typeof(P2.Game.Unit.UnitBase),nameof(PlayerP2));
    }
    private static void PlayerHook(Harmony h,Type type,string enter)
    {
        try {
            var method=type.GetMethod("callMessageWindowW");if(method==null||method.GetParameters().Any(p=>p.ParameterType.IsByRef))return;
            h.Patch(method,prefix:new HarmonyMethod(typeof(BattleChants).GetMethod(enter,BindingFlags.NonPublic|BindingFlags.Static)),finalizer:new HarmonyMethod(typeof(BattleChants).GetMethod(nameof(PlayerLeave),BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("battle-chant-player-hook",new{type=type.FullName,method=method.Name});
        }catch(Exception e){Reader.Log.LogWarning("Player chant scope: "+e.Message);}
    }
    private static void PlayerP1(P1.Game.Unit.UnitBase __instance,out int __state)
    {__state=playerDepth;try{if(__instance.pUnitSquad_?.pUnitTroop_?.troopInfo_?.troopType==P1.Game.Unit.TroopType.TroopType_Player)playerDepth++;}catch{}}
    private static void PlayerP2(P2.Game.Unit.UnitBase __instance,out int __state)
    {__state=playerDepth;try{if(__instance.pUnitSquad_?.pUnitTroop_?.troopInfo_?.troopType==P2.Game.Unit.TroopType.TroopType_Player)playerDepth++;}catch{}}
    private static void PlayerLeave(int __state){playerDepth=__state;}
    private static void Enter(out int __state){__state=flagDepth;flagDepth++;}
    private static void Leave(int __state){flagDepth=__state;}
    public static bool Suppress(string text)
    {
        bool playerSource=flagDepth>0||playerDepth>0;
        bool skip=BattleChantText.ShouldSuppress(Enabled,BattleMonitor.Active,playerSource,text);
        if(!Enabled&&BattleMonitor.Active&&!playerSource&&BattleChantText.IsChant(text)&&unknownSources.Add(text))
            Monitor.Record("battle-chant-source-unclassified",new{text,note="preserved because player speaker was not confirmed"});
        if(skip){if(suppressedVisible.Count>64)suppressedVisible.Clear();suppressedVisible[text]=Environment.TickCount64;}
        else suppressedVisible.Remove(text); // A separately sourced story line remains readable.
        return skip;
    }
    public static bool SuppressVisible(string text)=>!Enabled&&BattleMonitor.Active&&suppressedVisible.TryGetValue(text,out long at)&&Environment.TickCount64-at<2500;
}

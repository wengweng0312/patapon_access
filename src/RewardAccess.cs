using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace PataponAccess;

internal static class RewardAccess
{
    private static readonly Dictionary<(string game,int id),(uint nameId,int count,long at)> gains=new();
    private static readonly Dictionary<(string game,int id),(uint nameId,int count)> battleGains=new();
    private static bool battleEnded;
        private static long battleChangedAt;
    private static long lastResultSeen;
    private static bool resultSeen;
    private static string miniRepeat="";
    public static string RepeatText=>IsResultActive ? Summary : miniRepeat;
    public static bool IsResultActive=>NativeResultAccess.State.Active(Environment.TickCount64)||(resultSeen&&Environment.TickCount64-lastResultSeen<500);
    private static string Summary=>NativeResultAccess.State.Active(Environment.TickCount64)?NativeResultAccess.State.Describe():RewardResultState.Unavailable;
    public static void BeginMission()
    {
        battleGains.Clear();battleEnded=false;miniRepeat="";lastResultSeen=0;resultSeen=false;
        NativeResultAccess.Reset();battleChangedAt=Environment.TickCount64;
    }
    public static void EndMission(){battleEnded=true;battleChangedAt=Environment.TickCount64;}
    private static void BattleGain(string game,int id,uint nameId,int count)
    {
        var key=(game,id);int old=battleGains.TryGetValue(key,out var prior)?prior.count:0;
        battleGains[key]=(nameId,old+count);battleChangedAt=Environment.TickCount64;
    }
    public static void Install()
    {
        NativeResultAccess.Install();
        MiningRewardAccess.Install(new Harmony("org.patapon.access.mining-rewards"));
        MiniGameRewardAccess.Install(new Harmony("org.patapon.access.mini-visible-rewards"));
        try {
            new Harmony("org.patapon.access.rewards").Patch(typeof(P1.GameSystem.Item.Operator).GetMethod("addItem"),
                postfix:new HarmonyMethod(typeof(RewardAccess).GetMethod(nameof(Added),BindingFlags.Static|BindingFlags.NonPublic)));
            new Harmony("org.patapon.access.rewards.p2").Patch(typeof(P2.GameSystem.Item.Operator).GetMethod("addItem"),
                postfix:new HarmonyMethod(typeof(RewardAccess).GetMethod(nameof(AddedP2),BindingFlags.Static|BindingFlags.NonPublic)));
            Monitor.Record("reward-hook",new{game="P1",method="Item.Operator.addItem",installed=true});
        }catch(Exception e){Monitor.Record("reward-error",new{error=e.Message});}
    }
    private static void Added(P1.GameSystem.Item.Operator __instance,int __0,int __1)
    {
        try {
            if(__1<=0)return;
            // Read the item through its original accessor, not native object graph traversal.
            var param=__instance.getItemParam(__0);
            if(param==null)return;
            uint nameId=Convert.ToUInt32(param.nameId);
            Monitor.Record("item-added",new{game="P1",itemId=__0,count=__1,nameId,battle=BattleMonitor.Active});
            if(BattleMonitor.Active){BattleGain("P1",__0,nameId,__1);return;}
            var old=gains.TryGetValue(("P1",__0),out var value)?value.count:0;
            gains[("P1",__0)]=(nameId,old+__1,Environment.TickCount64);
        }catch(Exception e){Monitor.Record("reward-error",new{error=e.Message});}
    }
    private static void AddedP2(P2.GameSystem.Item.Operator __instance,int __0,int __1)
    {
        try {
            if(__1<=0)return;
            var param=__instance.getItemParam(__0);if(param==null)return;
            uint nameId=Convert.ToUInt32(param.nameId);
            Monitor.Record("item-added",new{game="P2",itemId=__0,count=__1,nameId,battle=BattleMonitor.Active});
            MiningRewardAccess.Added(nameId,__1);
            MiniGameRewardAccess.Added(nameId,__1);
            if(BattleMonitor.Active){BattleGain("P2",__0,nameId,__1);return;}
            var key=("P2",__0);int old=gains.TryGetValue(key,out var value)?value.count:0;
            gains[key]=(nameId,old+__1,Environment.TickCount64);
        }catch(Exception e){Monitor.Record("reward-error",new{game="P2",error=e.Message});}
    }
    public static bool Scan()
    {
        MiningRewardAccess.Scan();
        MiniGameRewardAccess.Scan();
        long now=Environment.TickCount64;
        // P2 keeps the result panel alive behind treasure selection/confirmation.
        // Background result text must not consume the entire speech update.
        if(P2TreasureAccess.IsActive||CommonUi.CommandWindowActive)return false;
        // Let simultaneous grants settle before producing one utterance.
        if(gains.Values.Any(g=>now-g.at<500))return false;
        // Inventory mutation remains diagnostic evidence, never a replacement for on-screen results.
        if(battleEnded&&now-battleChangedAt>=700){battleGains.Clear();battleEnded=false;}
        bool nativeActive=NativeResultAccess.State.Active(now);
        // Retain visible-panel detection for F3/F4 if a native reader is unavailable.
        // Never infer loot by collecting unrelated or recycled TMP labels.
        bool visible=UnityEngine.Object.FindObjectsOfType<TMP_Text>().Any(t=>t&&t.isActiveAndEnabled&&t.color.a>=0.05f&&t.maxVisibleCharacters>0
            &&!string.IsNullOrWhiteSpace(t.text)&&Monitor.PathOf(t.transform).Contains("Result/ResultMask/Drum2DText_"));
        if(nativeActive||visible) {
            lastResultSeen=now;resultSeen=true;
            string displayText=NativeResultAccess.State.Take(now);
            if(displayText.Length>0){
                Speech.Speak(displayText,interrupt:false);
                Monitor.Record("native-result-reward-speech",new{items=NativeResultAccess.State.Names.ToArray(),countsKnown=false});
            }
            string guide=NativeResultGuideAccess.Take();if(guide.Length>0)Speech.Speak(guide,interrupt:false);
            return true;
        }
        // Outside missions, wait for a batch of grants without hiding an active result.
        if(gains.Values.Any(g=>now-g.at<500))return false;
        foreach(var id in gains.Keys.ToArray()) {
            var gain=gains[id];if(now-gain.at<500)continue;
            string? name=UnitNames.FetchLocalized(id.game,gain.nameId);
            if(name==null && now-gain.at<5000)continue;
            gains.Remove(id);
            if(name==null){Monitor.Record("reward-name-missing",new{itemId=id,gain.nameId,gain.count});continue;}
            Monitor.Record("inventory-grant-observed",new{game=id.game,itemId=id.id,name,count=gain.count,source="inventory-add-outside-mission",spoken=false,reason="native visible dialogue owns reward wording"});
        }
        return false;
    }
}


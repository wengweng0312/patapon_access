using HarmonyLib;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace PataponAccess;

internal static class RewardAccess
{
    private static readonly HashSet<string> collected=new();
    private static readonly List<string> order=new();
    private static readonly Dictionary<(string game,int id),(uint nameId,int count,long at)> gains=new();
    private static readonly Dictionary<(string game,int id),(uint nameId,int count)> battleGains=new();
    private static bool battleEnded;
    private static bool inventoryResultUsed;
    private static string inventorySummary="";
    private static long battleChangedAt;
    private static long changedAt,lastResultSeen;
    private static string spoken="",miniRepeat="";
    private static bool collecting;
    public static string RepeatText=>IsResultActive ? Summary : miniRepeat;
    public static bool IsResultActive=>Environment.TickCount64-lastResultSeen<500;
    private static string Summary=>inventorySummary.Length>0?inventorySummary:I18n.T("ui.aac9c0e4dc1b","戰鬥結算，獲得物品：")+(order.Count>0?string.Join("、",order):I18n.T("ui.067fa1026fae","正在讀取戰利品"));
    public static void BeginMission(){battleGains.Clear();battleEnded=false;inventoryResultUsed=false;inventorySummary="";battleChangedAt=Environment.TickCount64;}
    public static void EndMission(){battleEnded=true;battleChangedAt=Environment.TickCount64;}
    private static void BattleGain(string game,int id,uint nameId,int count)
    {
        var key=(game,id);int old=battleGains.TryGetValue(key,out var prior)?prior.count:0;
        battleGains[key]=(nameId,old+count);inventoryResultUsed=true;battleChangedAt=Environment.TickCount64;
    }
    public static void Install()
    {
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
            if(BattleMonitor.Active){BattleGain("P2",__0,nameId,__1);return;}
            var key=("P2",__0);int old=gains.TryGetValue(key,out var value)?value.count:0;
            gains[key]=(nameId,old+__1,Environment.TickCount64);
        }catch(Exception e){Monitor.Record("reward-error",new{game="P2",error=e.Message});}
    }
    public static bool Scan()
    {
        long now=Environment.TickCount64;
        // P2 keeps the result panel alive behind treasure selection/confirmation.
        // Background result text must not consume the entire speech update.
        if(P2TreasureAccess.IsActive||CommonUi.CommandWindowActive)return false;
        // Let simultaneous grants settle before producing one utterance.
        if(gains.Values.Any(g=>now-g.at<500))return false;
        string battleText="";
        if(battleEnded&&battleGains.Count>0&&now-battleChangedAt>=700){
            var rewards=new List<string>();var entries=new List<object>();bool waiting=false;
            foreach(var entry in battleGains){
                var key=entry.Key;var gain=entry.Value;
                string? name=UnitNames.FetchLocalized(key.game,gain.nameId);
                if(name==null){waiting=true;continue;}
                rewards.Add(name+"，"+gain.count+I18n.T("ui.c61cc5c219ba"," 個"));entries.Add(new{game=key.game,itemId=key.id,name,count=gain.count});
            }
            if(!waiting||now-battleChangedAt>=5000){
                if(rewards.Count>0){inventorySummary=battleText=I18n.T("ui.aac9c0e4dc1b","戰鬥結算，獲得物品：")+string.Join("；",rewards);Monitor.Record("inventory-result-reward-speech",new{items=entries.ToArray(),countsKnown=true,unresolved=battleGains.Count-rewards.Count});}
                battleGains.Clear();battleEnded=false;
            }
        }
        var texts=UnityEngine.Object.FindObjectsOfType<TMP_Text>()
            .Where(t=>t && t.isActiveAndEnabled && t.color.a>=0.05f && t.maxVisibleCharacters>0
                && Monitor.PathOf(t.transform).Contains("Result/ResultMask/Drum2DText_"))
            .Select(t=>Speech.Clean(t.text)).Where(t=>t.Length>0).ToArray();
        if(texts.Length>0) {
            if(now-lastResultSeen>1000) {collected.Clear();order.Clear();collecting=false;spoken="";}
            lastResultSeen=now;
            if(texts.Contains("戰利品"))collecting=true;
            if(collecting)foreach(string text in texts) {
                if(text is "戰利品" or "響聲" or "過關時間" or "撿到的頭盔" or "喪失的頭盔"
                    || Regex.IsMatch(text,@"^[\d\s:.,%×xX+\-]+$") || text.Contains('～'))continue;
                if(collected.Add(text)){order.Add(text);changedAt=now;}
            }
            // Native grants arrive as the result closes. Visible labels alone can
            // contain enemy names and cannot reliably pair names with quantities.
            if(!BattleMonitor.Active&&!battleEnded&&!inventoryResultUsed&&order.Count>0 && now-changedAt>=1300 && spoken!=Summary && battleGains.Count==0) {
                spoken=Summary;Speech.Speak(spoken,interrupt:false);
                Monitor.Record("result-reward-speech",new{items=order.ToArray(),countsKnown=false});
            }
            if(battleText.Length>0){miniRepeat=battleText;CommonUi.QueueSelection(miniRepeat,interrupt:false);}
            return true;
        }
        var announcements=new List<string>();
        foreach(var id in gains.Keys.ToArray()) {
            var gain=gains[id];if(now-gain.at<500)continue;
            string? name=UnitNames.FetchLocalized(id.game,gain.nameId);
            if(name==null && now-gain.at<5000)continue;
            gains.Remove(id);
            if(name==null){Monitor.Record("reward-name-missing",new{itemId=id,gain.nameId,gain.count});continue;}
            announcements.Add(name+"，"+gain.count+I18n.T("ui.c61cc5c219ba"," 個"));
            Monitor.Record("minigame-reward-speech",new{game=id.game,itemId=id.id,name,count=gain.count,source="inventory-add-outside-mission"});
        }
        if(announcements.Count>0||battleText.Length>0) {miniRepeat=string.Join("。",new[]{battleText,announcements.Count>0?I18n.T("ui.3bdb812d7a8a","獲得物品，")+string.Join("；",announcements):""}.Where(s=>s.Length>0));CommonUi.QueueSelection(miniRepeat,interrupt:false);}
        return false;
    }
}


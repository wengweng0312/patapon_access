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
    private static readonly Dictionary<int,(uint nameId,int count,long at)> gains=new();
    private static long changedAt,lastResultSeen;
    private static string spoken="",miniRepeat="";
    private static bool collecting;
    public static string RepeatText=>IsResultActive ? Summary : miniRepeat;
    public static bool IsResultActive=>Environment.TickCount64-lastResultSeen<500;
    private static string Summary=>"戰鬥結算，獲得物品："+(order.Count>0?string.Join("、",order):"正在讀取戰利品");
    public static void Install()
    {
        try {
            new Harmony("org.patapon.access.rewards").Patch(typeof(P1.GameSystem.Item.Operator).GetMethod("addItem"),
                postfix:new HarmonyMethod(typeof(RewardAccess).GetMethod(nameof(Added),BindingFlags.Static|BindingFlags.NonPublic)));
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
            if(BattleMonitor.Active)return;
            var old=gains.TryGetValue(__0,out var value)?value.count:0;
            gains[__0]=(nameId,old+__1,Environment.TickCount64);
        }catch(Exception e){Monitor.Record("reward-error",new{error=e.Message});}
    }
    public static bool Scan()
    {
        long now=Environment.TickCount64;
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
            if(order.Count>0 && now-changedAt>=1300 && spoken!=Summary) {
                spoken=Summary;Speech.Speak(spoken);
                Monitor.Record("result-reward-speech",new{items=order.ToArray(),countsKnown=false});
            }
            return true;
        }
        var announcements=new List<string>();
        foreach(int id in gains.Keys.ToArray()) {
            var gain=gains[id];if(now-gain.at<500)continue;
            string? name=UnitNames.FetchLocalized("P1",gain.nameId);
            if(name==null && now-gain.at<5000)continue;
            gains.Remove(id);
            if(name==null){Monitor.Record("reward-name-missing",new{itemId=id,gain.nameId,gain.count});continue;}
            miniRepeat="獲得物品，"+name+"，"+gain.count+" 個";
            CommonUi.QueueSelection(miniRepeat);
            Monitor.Record("minigame-reward-speech",new{itemId=id,name,count=gain.count,source="inventory-add-outside-mission"});
        }
        if(announcements.Count>0) {miniRepeat="獲得物品，"+string.Join("；",announcements);CommonUi.QueueSelection(miniRepeat);}
        return false;
    }
}


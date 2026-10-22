using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class P2TreasureAccess
{
    private static long seen,lastError;
    private static string current="",previous="";
    public static bool IsActive=>Environment.TickCount64-seen<500;
    public static string RepeatText=>IsActive?current:"";
    public static void Install()
    {
        try {new Harmony("org.patapon.access.p2-treasure").Patch(typeof(P2.Game.Result.TreasureBox).GetMethod("update"),postfix:new HarmonyMethod(typeof(P2TreasureAccess).GetMethod(nameof(Observe),BindingFlags.NonPublic|BindingFlags.Static)));}
        catch(Exception e){Reader.Log.LogWarning("P2 treasure hook: "+e.Message);}
    }
    private static void Observe(P2.Game.Result.TreasureBox __instance)
    {
        try {
            var phase=__instance.phase_;
            if(phase is P2.Game.Result.TreasureBox.Phase.Phase_None or P2.Game.Result.TreasureBox.Phase.Phase_Exit){current=previous="";seen=0;return;}
            seen=Environment.TickCount64;
            // Native confirmation windows have their own text. Only describe the cursor here.
            if(phase!=P2.Game.Result.TreasureBox.Phase.Phase_Select)return;
            var window=__instance.window_;int index=__instance.selectItem_;
            var boxes=window?.boxes_;if(boxes==null||index<0||index>=boxes.Count)return;
            var box=boxes[index];if(box==null)return;
            uint cost=box.getCoinCount(),available=window!.getCoinCount();bool closed=box.isClose();
            // Do not expose hidden contents of unopened boxes.
            string contents=closed?I18n.T("ui.5011481529bc","尚未開啟"):I18n.T("ui.450857204c77","已開啟，")+box.getItemName()+"，"+box.getItemCount()+I18n.T("ui.c61cc5c219ba"," 個");
            // Window.render passes these exact two strings to Font.Text.output.
            // Keep cursor/closed-box semantics as accessibility metadata, never replace native currency text.
            string nativeCoin=DialogueText.Clean(window.coinMessage_??"");
            string nativeCharin=DialogueText.Clean(window.charinMessage_??"");
            current=string.Join("。",new[]{
                I18n.T("ui.73f1855f451e","第 {0} 項，共 {1} 項",index+1,boxes.Count),contents,
                I18n.T("treasure.cost","開啟所需數量 {0}",cost),nativeCoin,nativeCharin}.Where(t=>t.Length>0));
            if(current==previous)return;previous=current;
            CommonUi.QueueSelection(current);
            Monitor.Record("p2-treasure-selection",new{index,total=boxes.Count,cost,available,closed,text=current});
        }catch(Exception e){long now=Environment.TickCount64;if(now-lastError>5000){lastError=now;Monitor.Record("p2-treasure-error",new{error=e.Message});}}
    }
}

namespace PataponAccess;
internal static class P2EvolutionAccess
{
    private static readonly EvolutionWindowTracker tracker=new();
    private static long seen,nextError;
    private static string current="";
    public static bool IsActive=>Environment.TickCount64-seen<500;
    public static string RepeatText=>IsActive&&!CommonUi.CommandWindowActive?current:"";
    private static readonly Dictionary<IntPtr,(string role,long at)> windows=new();
    public static void Install()
    {
        try{new HarmonyLib.Harmony("org.patapon.access.evolution-owner").Patch(typeof(P2.Bases.Camp.EvolutionMap.Controller).GetMethod("update"),postfix:new HarmonyLib.HarmonyMethod(typeof(P2EvolutionAccess).GetMethod(nameof(Owner),System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)));}
        catch(Exception e){Reader.Log.LogWarning("Evolution owner hook: "+e.Message);}
    }
    private static void Owner(P2.Bases.Camp.EvolutionMap.Controller __instance)
    {
        try{
            if(!__instance.isReady_||__instance.isEnd_)return;
            void Remember(P2.GameSystem.MessageBox? box,string role){var window=box?.dialog_?.cmdRenderer_;if(box?.isShow()==true&&window!=null){if(windows.Count>16)windows.Clear();windows[window.Pointer]=(role,Environment.TickCount64);}}
            Remember(__instance.popupMsgBox_,"description");Remember(__instance.itemMsgBox_,"requirements");Remember(__instance.yesNoMsgBox_,"confirmation");
        }catch(Exception e){if(Environment.TickCount64>=nextError){nextError=Environment.TickCount64+30000;Monitor.Record("evolution-owner-error",new{error=e.Message});}}
    }
    public static void Navigation(string text,bool newNode=false)
    {if(newNode)tracker.InvalidateSelection();seen=Environment.TickCount64;current=text;CommonUi.QueueSelection(text);}
    public static bool ObserveWindow(string title,string label,IntPtr owner=default)
    {
        long now=Environment.TickCount64;
        string role=windows.TryGetValue(owner,out var entry)&&now-entry.at<500?entry.role:"";
        var result=tracker.Observe(title,label,now,role);
        if(result.handled)seen=now;
        if(result.text.Length>0){
            current=result.text;CommonUi.QueueSelection(current);
            Monitor.Record("p2-evolution-selection",new{title,label,text=current,role,source=role.Length>0?"native-controller-window-role":"legacy-title-fallback"});
        }
        return result.handled;
    }
}

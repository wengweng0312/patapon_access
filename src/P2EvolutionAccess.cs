namespace PataponAccess;
internal static class P2EvolutionAccess
{
    private static readonly EvolutionWindowTracker tracker=new();
    private static long seen;
    private static string current="";
    public static bool IsActive=>Environment.TickCount64-seen<500;
    public static string RepeatText=>IsActive&&!CommonUi.CommandWindowActive?current:"";
    public static void Install()=>Monitor.Record("evolution-observer",new{mode="paired native command windows",game="P2"});
    public static void Navigation(string text,bool newNode=false)
    {if(newNode)tracker.InvalidateSelection();seen=Environment.TickCount64;current=text;CommonUi.QueueSelection(text);}
    public static bool ObserveWindow(string title,string label)
    {
        long now=Environment.TickCount64;
        var result=tracker.Observe(title,label,now);
        if(result.handled)seen=now;
        if(result.text.Length>0){
            current=result.text;CommonUi.QueueSelection(current);
            Monitor.Record("p2-evolution-selection",new{title,label,text=current,source="paired command windows"});
        }
        return result.handled;
    }
}

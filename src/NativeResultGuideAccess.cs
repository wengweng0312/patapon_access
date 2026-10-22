using HarmonyLib;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
namespace PataponAccess;

// Observe only guides registered by the currently active result display and actually rendered.
internal static class NativeResultGuideAccess
{
    private static readonly Dictionary<IntPtr,long> owners=new();
    private static string current="",spoken="";
    private static long seen;
    public static string RepeatText=>Environment.TickCount64-seen<500?current:"";
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.result-guides");
        foreach(string game in new[]{"P1","P2"})try{
            var type=typeof(LauncherMain).Assembly.GetType(game+".GameSystem.KeyGuide")!;
            foreach(var method in type.GetMethods().Where(m=>m.Name=="render"))
                h.Patch(method,postfix:new HarmonyMethod(typeof(NativeResultGuideAccess).GetMethod(nameof(Rendered),BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("native-result-guide-hook",new{game,installed=true});
        }catch(Exception e){Reader.Log.LogWarning("Result guide hook: "+e.Message);}
    }
    public static void Observe(Il2CppObjectBase? guide)
    {
        if(guide==null)return;
        if(owners.Count>16)owners.Clear();owners[guide.Pointer]=Environment.TickCount64;
    }
    private static void Rendered(Il2CppObjectBase __instance)
    {
        try{
            long now=Environment.TickCount64;
            if(!owners.TryGetValue(__instance.Pointer,out long registered)||now-registered>500)return;
            string raw=__instance is P1.GameSystem.KeyGuide p1?p1.keyGuideString_:__instance is P2.GameSystem.KeyGuide p2?p2.keyGuideString_:"";
            string text=DialogueText.Clean(raw??"");
            if(text.Length==0||text=="%"||DialogueText.HasVariableToken(text))return;
            if(now-seen>700)spoken="";
            current=text;seen=now;
        }catch(Exception e){Reader.Log.LogWarning("Result guide read: "+e.Message);}
    }
    public static string Take()
    {
        string text=RepeatText;if(text.Length==0||text==spoken)return "";
        spoken=text;Monitor.Record("native-result-guide",new{text,source="registered Result keyGuide actually rendered; native keyGuideString"});return text;
    }
}

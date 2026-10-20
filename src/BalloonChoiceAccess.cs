using HarmonyLib;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
namespace PataponAccess;
internal static class BalloonChoiceAccess
{
    private sealed record Entry(string Raw,BalloonChoicePrompt Prompt,BalloonChoiceState State){public string Signature="";}
    private static readonly Dictionary<IntPtr,Entry> entries=new();
    private static long seen,nextError;
    private static string repeat="";
    public static bool Active=>Environment.TickCount64-seen<500;
    public static string RepeatText=>Active?repeat:"";
    public static void Install(Harmony hooks)
    {
        foreach(string ns in new[]{"P1","P2","P1S","P2S"})try {
            var type=typeof(LauncherMain).Assembly.GetType(ns+".Game.Window.Message");
            var method=type?.GetMethod("update",new[]{typeof(uint)});
            if(method==null)continue;
            // Observe the substantial update routine; never detour tiny cursor setters.
            hooks.Patch(method,postfix:new HarmonyMethod(typeof(BalloonChoiceAccess).GetMethod(nameof(Update),BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("balloon-choice-hook",new{game=ns,method=method.ToString()});
        }catch(Exception e){Reader.Log.LogWarning("Balloon choice hook: "+e.Message);}
    }
    public static void Offer(object owner,string raw)
    {
        if(!owner.GetType().FullName!.EndsWith(".Game.Window.Message",StringComparison.Ordinal))return;
        IntPtr key=((Il2CppObjectBase)owner).Pointer;
        var prompt=BalloonChoiceText.Parse(raw);
        if(prompt==null){entries.Remove(key);return;}
        if(entries.TryGetValue(key,out var old)&&old.Raw==raw)return;
        if(entries.Count>32)entries.Clear();
        entries[key]=new(raw,prompt,new());
    }
    private static object? Get(object owner,string property)=>owner.GetType().GetProperty(property)?.GetValue(owner);
    private static void Update(object __instance)
    {
        try {
            IntPtr key=((Il2CppObjectBase)__instance).Pointer;
            if(!entries.TryGetValue(key,out var entry))return;
            bool use=Get(__instance,"isUse_") is true,render=Get(__instance,"isRender_") is true,updating=Get(__instance,"isUpdate_") is true;
            string phase=Get(__instance,"phase_")?.ToString()??"";
            int top=Convert.ToInt32(Get(__instance,"selectTop_")),index=Convert.ToInt32(Get(__instance,"selectLine_"));
            // Same gates as native MessageWindowBase.changeSelect; selectLine is
            // a relative option index, not an absolute text-line number.
            bool ready=use&&render&&updating&&top>=0&&phase=="Phase_Draw"&&index>=0&&index<entry.Prompt.Options.Length;
            string signature=$"{use}/{render}/{updating}/{phase}/{top}/{index}";
            if(signature!=entry.Signature){entry.Signature=signature;Monitor.Record("balloon-choice-state",new{type=__instance.GetType().FullName,prompt=entry.Prompt.Question,options=entry.Prompt.Options,use,render,updating,phase,top,index,ready});}
            var result=entry.State.Observe(entry.Prompt,ready,index);
            if(ready){seen=Environment.TickCount64;repeat=entry.Prompt.Question+"，"+entry.Prompt.Options.ElementAtOrDefault(index);}
            if(result.selected.Length>0) {
                string text=result.first?I18n.T("dialog.choiceReady","現在可以選擇。")+result.selected:result.selected;
                CommonUi.QueueSelection(text,interrupt:!result.first);
                Monitor.Record("balloon-choice-speech",new{text,index,first=result.first});
            }
            if(!use||phase is "Phase_Close" or "Phase_None")entries.Remove(key);
        }catch(Exception e){if(Environment.TickCount64>=nextError){nextError=Environment.TickCount64+30000;Monitor.Record("balloon-choice-error",new{error=e.Message});}}
    }
}

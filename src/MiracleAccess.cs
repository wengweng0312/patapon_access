using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using TMPro;
namespace PataponAccess;

internal static class MiracleAccess
{
    private static bool quiet,enabled;
    private static long pending;
    private static readonly Dictionary<string,MiracleThreshold> timers=new();
    private static readonly Dictionary<int,long> digits=new();
    [ThreadStatic] private static bool drawing;
    public static void Install(ConfigFile config)
    {
        quiet=config.Bind("Speech","QuietMiracleCountdown",true,"Hide only native P2 miracle HUD countdown digits from automatic speech.").Value;
        enabled=config.Bind("Audio","MiracleEndingCue",true,"Play three tones once when a P1 or P2 miracle crosses ten seconds remaining.").Value;
        var h=new Harmony("org.patapon.access.miracle");
        foreach(var type in new[]{typeof(P1.Game.Miracle.Controller),typeof(P2.Game.Miracle.Controller)}){
            Patch(h,type,"update",nameof(Update));
            Patch(h,type,"activate",nameof(ResetTimer));
            Patch(h,type,"endMiracle",nameof(ResetTimer));
        }
        // afterRender's only system-font output is printNumber for the miracle.
        // The separate mission clock uses timeLimitList_ (Message), not this pool.
        h.Patch(typeof(P2.Game.Unit.HpGauge).GetMethod("afterRender"),prefix:new HarmonyMethod(typeof(MiracleAccess).GetMethod(nameof(BeginDraw),BindingFlags.NonPublic|BindingFlags.Static)),finalizer:new HarmonyMethod(typeof(MiracleAccess).GetMethod(nameof(EndDraw),BindingFlags.NonPublic|BindingFlags.Static)));
        Patch(h,typeof(P2.System.Font.TextManager),"getDrumSystemText",nameof(SystemText));
    }
    private static void Patch(Harmony h,Type t,string name,string callback)=>h.Patch(t.GetMethod(name),postfix:new HarmonyMethod(typeof(MiracleAccess).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
    private static string Game(object o)=>o is P1.Game.Miracle.Controller?"P1":"P2";
    private static void ResetTimer(object __instance){if(timers.TryGetValue(Game(__instance),out var s))s.Reset();pending=0;}
    private static void Update(object __instance)
    {
        try{
            string game=Game(__instance);bool active;float remaining;
            if(__instance is P1.Game.Miracle.Controller p1){var t=p1.timer_;if(t==null)return;active=t.isActive_;remaining=t.waitTime_-t.currentTime_;}
            else if(__instance is P2.Game.Miracle.Controller p2){var t=p2.timer_;if(t==null)return;active=t.isActive_;remaining=t.waitTime_-t.currentTime_;}
            else return;
            if(!timers.TryGetValue(game,out var state))timers[game]=state=new();
            if(state.Observe(active,remaining)){
                Monitor.Record("miracle-ten-seconds",new{game,remaining,audio=enabled});
                if(enabled)pending=Environment.TickCount64;
            }
        }catch(Exception e){Reader.Log.LogWarning("Miracle timer: "+e.Message);}
    }
    private static void BeginDraw(P2.Game.Unit.HpGauge __instance){drawing=quiet&&__instance.haveMiracleAdvantage_;}
    private static void EndDraw(){drawing=false;}
    private static void SystemText(P2.System.Font.DrumText __result)
    {
        try{var text=__result?.Text;if(text==null||!text)return;int id=text.GetInstanceID();if(drawing)digits[id]=Environment.TickCount64;else digits.Remove(id);}catch{}
    }
    public static bool Suppress(TMP_Text text)=>quiet&&digits.TryGetValue(text.GetInstanceID(),out long seen)&&Environment.TickCount64-seen<400;
    public static void Clear(){timers.Clear();digits.Clear();pending=0;drawing=false;}
    public static void Tick(){long queued=pending;pending=0;if(queued!=0&&Environment.TickCount64-queued<1000)CueAudio.Play(Path.Combine(Path.GetDirectoryName(typeof(MiracleAccess).Assembly.Location)!,"miracle-ending.wav"));}
}

using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PataponAccess;

internal static class VegetableCue
{
    private static readonly Dictionary<IntPtr,(int frame,int state,bool fired)> tracked=new();
    private static bool enabled;
    private static int leadFrames;
    private static long lastCue;
    private static string sound="";
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)]
    private static extern bool PlaySound(string file,IntPtr module,uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    public static void Install(bool value,int lead)
    {
        enabled=value;leadFrames=Math.Clamp(lead,1,35);
        sound=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"vegetable-catch.wav");
        var h=new Harmony("org.patapon.access.vegetable");
        Patch(h,typeof(P1.Bases.Camp.SoundGame.Bean.Item),"update",nameof(ItemUpdate));
        Patch(h,typeof(P1.Sound.SubGame.Bean.Command),"hitKey",nameof(Input));
        Patch(h,typeof(P1.Bases.Camp.SoundGame.Bean.ItemManager),"catchItem",nameof(Caught));
        Patch(h,typeof(P2.Bases.Camp.SoundGame.Bean.Item),"update",nameof(ItemUpdateP2));
        // P2 hitKey has an extra rate argument; the callback observes only shared scalar arguments.
        Patch(h,typeof(P2.Sound.SubGame.Bean.Command),"hitKey",nameof(Input));
        Patch(h,typeof(P2.Bases.Camp.SoundGame.Bean.ItemManager),"catchItem",nameof(Caught));
        Monitor.Record("vegetable-cue-installed",new{game="P1",enabled,leadFrames,experimental=true,soundPresent=File.Exists(sound)});
    }
    private static void Patch(Harmony h,Type type,string method,string callback)
    {
        try {h.Patch(type.GetMethod(method),postfix:new HarmonyMethod(typeof(VegetableCue).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));}
        catch(Exception e){Monitor.Record("vegetable-hook-error",new{method,error=e.Message});}
    }
    private static void ItemUpdate(P1.Bases.Camp.SoundGame.Bean.Item __instance)
    {
        try { Observe(__instance.Pointer,(int)__instance.state_,__instance.fallFrame_,__instance.isCacthEnd_,__instance.catchNum_,
            __instance.state_==P1.Bases.Camp.SoundGame.Bean.Item.State.State_SkyWait||__instance.state_==P1.Bases.Camp.SoundGame.Bean.Item.State.State_Fall,
            __instance.state_==P1.Bases.Camp.SoundGame.Bean.Item.State.State_Shoot,"P1"); }
        catch(Exception e){Monitor.Record("vegetable-timing-error",new{game="P1",error=e.Message});}
    }
    private static void ItemUpdateP2(P2.Bases.Camp.SoundGame.Bean.Item __instance)
    {
        try { Observe(__instance.Pointer,(int)__instance.state_,__instance.fallFrame_,__instance.isCacthEnd_,__instance.catchNum_,
            __instance.state_==P2.Bases.Camp.SoundGame.Bean.Item.State.State_SkyWait||__instance.state_==P2.Bases.Camp.SoundGame.Bean.Item.State.State_Fall,
            __instance.state_==P2.Bases.Camp.SoundGame.Bean.Item.State.State_Shoot,"P2"); }
        catch(Exception e){Monitor.Record("vegetable-timing-error",new{game="P2",error=e.Message});}
    }
    private static void Observe(IntPtr key,int state,int remaining,bool ended,uint catches,bool falling,bool shoot,string game)
    {
        try {
            var old=tracked.TryGetValue(key,out var prior)?prior:(frame:9999,state:-1,fired:false);
            bool fired=old.fired;
            if(remaining>36 || shoot)fired=false;
            if(state!=old.state || (falling && remaining/6!=old.frame/6))
                Monitor.Record("vegetable-item-timing",new{game,item=key.ToInt64(),state,remaining,ended,catches});
            if(falling && !ended && catches==0 && !fired && remaining>0 && remaining<=leadFrames && old.frame>leadFrames) {
                fired=true;long now=Environment.TickCount64;
                GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);
                bool played=false;
                if(enabled && pid==Environment.ProcessId && now-lastCue>=100 && File.Exists(sound)) {
                    lastCue=now;played=CueAudio.Play(sound);
                }
                Monitor.Record("vegetable-catch-cue",new{game,item=key.ToInt64(),remaining,leadFrames,played,experimental=true});
            }
            if(tracked.Count>128)tracked.Clear();
            tracked[key]=(remaining,state,fired);
        }catch(Exception e){Monitor.Record("vegetable-timing-error",new{error=e.Message});}
    }
    private static void Input(int __0,uint __1,bool __2,bool __result)
        =>Monitor.Record("vegetable-input",new{percussion=__0,halfCount=__1,advance=__2,accepted=__result});
    private static void Caught()=>Monitor.Record("vegetable-caught",new{success=true});
}

using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

namespace PataponAccess;

internal static class AttackCue
{
    private static bool enabled,visible;
    private static long lastSeen,lastSound;
    private static string file="";
    private static int requested;
    [DllImport("winmm.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string sound,IntPtr module,uint flags);
    public static void Initialize(bool value)
    {
        enabled=value;
        file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"attack-ready.wav");
        if(!File.Exists(file)) Reader.Log.LogWarning("Attack-ready sound file missing: "+file);
    }
    public static bool Matches(string text)=>AttackCueText.Matches(text);
    public static bool Observe(string text)
    {
        if(!enabled || !Matches(text)) return false;
        System.Threading.Interlocked.Exchange(ref requested,1);
        return true;
    }
    public static void Scan()
    {
        if(!enabled) return;
        bool found=System.Threading.Interlocked.Exchange(ref requested,0)!=0;
        foreach(var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) {
            if(t && t.isActiveAndEnabled && t.color.a>0.05f && t.maxVisibleCharacters>0 && Matches(t.text)) {found=true;break;}
        }
        if(!found) foreach(var t in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>()) {
            if(t && t.isActiveAndEnabled && t.color.a>0.05f && Matches(t.text)) {found=true;break;}
        }
        long now=Environment.TickCount64;
        if(found) {
            lastSeen=now;
            if(!visible && now-lastSound>1200) {visible=true;lastSound=now;Play(false);}
        } else if(now-lastSeen>500) visible=false;
    }
    public static void Test()=>Play(true);
    private static void Play(bool test)
    {
        if(!File.Exists(file)) {Reader.Log.LogWarning("Attack-ready cue cannot play: WAV missing.");return;}
        // Async playback keeps the rhythm/input thread unblocked.
        bool success=CueAudio.Play(file);
        Reader.Log.LogInfo("Attack-ready cue: "+(test?"test":"visible text")+"; playback="+success);
        Monitor.Record("attack-ready-cue",new{test,success});
    }
}

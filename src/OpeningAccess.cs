using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PataponAccess;

internal static class OpeningAccess
{
    private static long seen,nextGuide;
    private static bool wasSigning,tabDown,announced;
    private static string zone="";
    public static bool IsActive=>Environment.TickCount64-seen<500;
    private const string Contract="致以世界盡頭為目標之者。只要汝，手中持有此太鼓，就必須做為 PATAPON 的神，為其繁盛鞠躬盡瘁。即便，感受律動之力不足，也絕對不要放棄，學習神之技。無止盡的努力的成果，必定會閃耀光芒的盛世造訪人間。汝，要是同意此約定的話，就做為 PATAPON 之神，拿起此太鼓吧。";
    private const string Instructions="方向鍵移動羽毛；Tab 將羽毛移到同意；移到後按遊戲的確認鍵，預設 Enter。F2 重讀誓約與操作說明。";
    public static string RepeatText=>Contract+Instructions+zone;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    public static void Install()
    {
        try {
            var h=new Harmony("org.patapon.access.opening");
            h.Patch(typeof(P1.Title.Opening.Controller).GetMethod("updateSignIn"),postfix:new HarmonyMethod(typeof(OpeningAccess).GetMethod(nameof(Observe),BindingFlags.NonPublic|BindingFlags.Static)));
            Reader.Log.LogInfo("Opening feather accessibility hook installed (P1).");
        }catch(Exception e){Reader.Log.LogWarning("Opening feather hook: "+e.Message);}
    }
    private static void Observe(P1.Title.Opening.Controller __instance)
    {
        try {
            bool signing=__instance.getState()==P1.Title.Opening.Controller.State.State_SignIn;
            if(!signing) {
                if(wasSigning){wasSigning=false;Monitor.Record("opening-contract-left",new{state=__instance.getState().ToString()});}
                tabDown=false;announced=false;return;
            }
            long now=Environment.TickCount64;bool first=!announced||!wasSigning||now-seen>1000;
            seen=now;wasSigning=true;
            GetWindowThreadProcessId(GetForegroundWindow(),out var pid);
            if(pid!=Environment.ProcessId)return;
            bool down=(GetAsyncKeyState(0x09)&0x8000)!=0;
            bool moved=down&&!tabDown;tabDown=down;
            if(moved){
                // Native isInRange checks x=200..280, y=220..243 in its 480x272 coordinate space.
                // Only move the cursor; the game's confirmation input stays untouched.
                __instance.penPosition_=new Vector2(240f,231.5f);
                Monitor.Record("opening-pen-focus",new{x=240,y=231.5,action="focus-only; not agreement confirmation"});
            }
            var p=__instance.penPosition_;
            bool inside=p.x>=200&&p.x<=280&&p.y>=220&&p.y<=243;
            string horizontal=p.x<200?"右":p.x>280?"左":"";
            string vertical=p.y<220?"下":p.y>243?"上":"";
            string current=inside?"羽毛已選中同意，按確認鍵繼續。":"羽毛尚未選中同意，請往"+vertical+horizontal+"移動，或按 Tab 定位。";
            if(first||moved||(current!=zone&&now>=nextGuide)) {
                zone=current;nextGuide=now+600;announced=true;
                CommonUi.QueueSelection(first?"PATAPON 開場誓約。"+Contract+Instructions+current:current);
                Monitor.Record("opening-pen-state",new{x=p.x,y=p.y,inside,text=current,first});
            }
        }catch(Exception e){Reader.Log.LogWarning("Opening feather observation: "+e.Message);}
    }
}


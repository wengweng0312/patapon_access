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
    private const string ContractP2="神明的契約書。吾以抵達世界的盡頭為目標，立誓成為 PATAPON 的神明。即便對節奏沒有自信，或是在途中想要半途而廢時，亦會絕對堅持到底。吾保證遵守此契約，化身 PATAPON 的神明，手執勇氣之太鼓。特此立誓。";
    private static string currentContract="";
    private static long nextContractError;
    // Contract prose belongs to the game locale, never to the module catalog.
    private static string ReadContract(string game)
    {
        try {
            if(game=="P2") {
                var common=P2.LaboCommon.pLaboCommonInstance_g;
                var messages=common?.getLocalizeDataItem?.imageData?.titleString?.messages;
                if(messages!=null&&messages.Length>5&&!string.IsNullOrWhiteSpace(messages[5]))
                    return DialogueText.Clean(messages[5])+"。";
            } else {
                var common=P1.LaboCommon.pLaboCommonInstance_g;
                // P1 renders the contract as an image. Our existing transcription
                // is valid only for the game's Traditional Chinese setting.
                if(common!=null&&common.getLanguageDataType()==P1.Localize.LanguageType.LanguageType_tc)
                    return Contract;
            }
        } catch(Exception e) {
            if(Environment.TickCount64>=nextContractError) {
                nextContractError=Environment.TickCount64+30000;
                Reader.Log.LogWarning("Opening game-language contract: "+e.Message);
            }
        }
        return I18n.T("opening.unavailable","目前無法取得遊戲語言的誓約內文。");
    }
    private static string Instructions=>I18n.T("ui.1964fa654382","方向鍵移動羽毛；Tab 將羽毛移到同意；移到後按遊戲的確認鍵，預設 Enter。F3 重讀誓約與操作說明。");
    public static string RepeatText=>currentContract+Instructions+zone;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    public static void Install()
    {
        try {
            var h=new Harmony("org.patapon.access.opening");
            h.Patch(typeof(P1.Title.Opening.Controller).GetMethod("updateSignIn"),postfix:new HarmonyMethod(typeof(OpeningAccess).GetMethod(nameof(Observe),BindingFlags.NonPublic|BindingFlags.Static)));
            h.Patch(typeof(P2.Title.Opening.Controller).GetMethod("updateSignIn"),postfix:new HarmonyMethod(typeof(OpeningAccess).GetMethod(nameof(ObserveP2),BindingFlags.NonPublic|BindingFlags.Static)));
            Reader.Log.LogInfo("Opening feather accessibility hooks installed (P1/P2).");
        }catch(Exception e){Reader.Log.LogWarning("Opening feather hook: "+e.Message);}
    }
    private static void Observe(P1.Title.Opening.Controller __instance)
    {
        try { ObserveSigning(__instance.getState()==P1.Title.Opening.Controller.State.State_SignIn,
            __instance.penPosition_,p=>__instance.penPosition_=p,"P1",ReadContract("P1"),200,280,220,243); }
        catch(Exception e){Reader.Log.LogWarning("P1 opening feather observation: "+e.Message);}
    }
    private static void ObserveP2(P2.Title.Opening.Controller __instance)
    {
        try { ObserveSigning(__instance.getState()==P2.Title.Opening.Controller.State.State_SignIn,
            __instance.penPosition_,p=>__instance.penPosition_=p,"P2",ReadContract("P2"),330,393,169,224); }
        catch(Exception e){Reader.Log.LogWarning("P2 opening feather observation: "+e.Message);}
    }
    private static void ObserveSigning(bool signing,Vector2 position,Action<Vector2> move,string game,string contract,float left,float right,float top,float bottom)
    {
        try {
            if(!signing) {
                if(wasSigning){wasSigning=false;Monitor.Record("opening-contract-left",new{game});}
                tabDown=false;announced=false;return;
            }
            long now=Environment.TickCount64;bool first=!announced||!wasSigning||now-seen>1000;
            seen=now;wasSigning=true;currentContract=contract;
            GetWindowThreadProcessId(GetForegroundWindow(),out var pid);
            if(pid!=Environment.ProcessId)return;
            bool down=(GetAsyncKeyState(0x09)&0x8000)!=0;
            bool moved=down&&!tabDown;tabDown=down;
            if(moved){
                // Bounds verified separately in each game's native isInRange.
                // Move only; never synthesize confirmation or advance the scene.
                position=new Vector2((left+right)/2,(top+bottom)/2);
                move(position);
                Monitor.Record("opening-pen-focus",new{game,x=position.x,y=position.y,action="focus-only; not agreement confirmation"});
            }
            var p=position;
            bool inside=p.x>=left&&p.x<=right&&p.y>=top&&p.y<=bottom;
            string horizontal=p.x<left?I18n.T("ui.14ab1f2bce0a","右"):p.x>right?I18n.T("ui.e2e0c454e5d5","左"):"";
            string vertical=p.y<top?I18n.T("ui.13c12b064b63","下"):p.y>bottom?I18n.T("ui.db5ba671d244","上"):"";
            string current=inside?I18n.T("ui.9a80fb6dbf7d","羽毛已選中同意，按確認鍵繼續。"):I18n.T("ui.19ae9e48af53","羽毛尚未選中同意，請往")+vertical+horizontal+I18n.T("ui.f952865a5ac1","移動，或按 Tab 定位。");
            if(first||moved||(current!=zone&&now>=nextGuide)) {
                zone=current;nextGuide=now+600;announced=true;
                CommonUi.QueueSelection(first?I18n.T("ui.6b4efca19b81","PATAPON 開場誓約。")+contract+Instructions+current:current,interrupt:!first);
                Monitor.Record("opening-pen-state",new{game,x=p.x,y=p.y,inside,text=current,first});
            }
        }catch(Exception e){Reader.Log.LogWarning("Opening feather observation: "+e.Message);}
    }
}


using HarmonyLib;
using TMPro;
using System.Reflection;

namespace PataponAccess;

internal static class NameInputAccess
{
    private static MultiPlatformInputManager? owner;
    private static bool active;
    private static string observed="", spoken="";
    private static long changedAt;
    public static bool IsActive=>active;
    public static string RepeatText=>"名稱輸入，目前內容："+(observed.Length==0?"空白":observed);
    public static void Install()
    {
        try {
            new Harmony("org.patapon.access.name").Patch(typeof(MultiPlatformInputManager).GetMethod("ShowTextInputField"),
                postfix:new HarmonyMethod(typeof(NameInputAccess).GetMethod(nameof(Opened),BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("name-input-hook",new{installed=true});
        } catch(Exception e) {Monitor.Record("name-input-error",new{error=e.Message});}
    }
    private static void Opened(MultiPlatformInputManager __instance)
    {
        owner=__instance;active=false;spoken="";
    }
    public static bool Scan()
    {
        try {
            var field=owner?owner!.textInputField:null;
            bool visible=field && field!.isActiveAndEnabled && field.gameObject.activeInHierarchy;
            if(!visible) {
                if(active)Monitor.Record("name-input-closed",new{closed=true});
                active=false;return false;
            }
            string value=field!.text??"";
            if(!active) {
                active=true;observed=spoken=value;changedAt=Environment.TickCount64;
                Speech.Speak("請輸入神明的名稱。"+RepeatText+"。可使用文字輸入與退格鍵，F2 重讀目前內容。輸入完成後使用遊戲原本的確認鍵。");
                Monitor.Record("name-input-opened",new{length=value.Length,limit=field.characterLimit});
            }
            if(value!=observed) {observed=value;changedAt=Environment.TickCount64;}
            if(observed!=spoken && Environment.TickCount64-changedAt>=200) {
                spoken=observed;Speech.Speak(RepeatText);
                Monitor.Record("name-input-edited",new{length=observed.Length});
            }
            return true;
        } catch(Exception e) {active=false;owner=null;Monitor.Record("name-input-error",new{error=e.Message});return false;}
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
namespace PataponAccess;
internal static class SpecialTitleHintAccess
{
    private static SpecialTitle? Page()
    {
        var page=UnityEngine.Object.FindObjectOfType<SpecialTitle>();
        if(!page||!page!.gameObject.activeInHierarchy||!page.isOperable)return null;
        if(page.gameDescriptionWindow&&!page.gameDescriptionWindow.IsClosed())return null;
        var selected=EventSystem.current?.currentSelectedGameObject;
        if(!selected)return null;
        return (page.p1Button&&selected==page.p1Button.gameObject)||(page.p2Button&&selected==page.p2Button.gameObject)||(page.backButton&&selected==page.backButton.gameObject)?page:null;
    }
    public static bool Open()
    {
        var page=Page();if(!page)return false;
        // Follow the game's own route, including disabling the menu while the modal is open.
        page!.SetEnableTitleButton(false);
        page.OpenGameDescriptionWindow();
        Monitor.Record("boss-rush-description-open",new{source="module F2; native SpecialTitle.OpenGameDescriptionWindow",nativeTabUnchanged=true});
        return true;
    }
    public static string Read()
    {
        if(!Page())return "";
        foreach(var guide in UnityEngine.Object.FindObjectsOfType<KeyGuide>()){
            if(!guide||!guide.isActiveAndEnabled||guide.guideDatas==null)continue;
            foreach(var data in guide.guideDatas){
                if(data.NameType!=KeyGuide.NameType.AboutBossRush||!data.text)continue;
                string text=VisibleUiText.Read(data.text);if(text.Length==0)continue;
                // F2 is the explicit accessibility shortcut; the source caption remains native.
                return "F2，"+DialogueText.Clean(text).TrimStart('：',':',' ');
            }
        }
        return "";
    }
}

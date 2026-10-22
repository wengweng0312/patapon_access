using UnityEngine;
namespace PataponAccess;
internal static class BossRushDescriptionAccess
{
    private static string owner="",repeat="";
    private static bool active;
    public static string RepeatText=>active?repeat:"";
    public static bool Scan()
    {
        foreach(var window in UnityEngine.Object.FindObjectsOfType<GameDescriptionWindow>()){
            if(!window||!window.gameObject.activeInHierarchy||window.IsClosed()||!window.descriptionImage||!window.descriptionImage.sprite)continue;
            var manager=UnityEngine.Object.FindObjectOfType<Px.Media.Dat.LauncherLocalizeManager>();
            string language=manager?manager!.GetLanguageTypeKey():"";
            string sprite=window.descriptionImage.sprite.name;
            string body=BossRushManualText.Read(sprite,language);
            if(body.Length==0)continue;
            string back="";
            foreach(var guide in window.GetComponentsInChildren<KeyGuide>()){
                if(!guide||!guide.isActiveAndEnabled||guide.guideDatas==null)continue;
                foreach(var row in guide.guideDatas){
                    if(row.NameType!=KeyGuide.NameType.Return||!row.text)continue;
                    string caption=VisibleUiText.Read(row.text);if(caption.Length==0)continue;
                    string key=BindingNames.FromSprite(row.text.text,"Menu_East");
                    back=string.Join("，",new[]{key,DialogueText.Clean(caption).TrimStart('：',':',' ')}.Where(s=>s.Length>0));
                }
            }
            active=true;repeat=body+(back.Length>0?"\n"+back:"");
            string current=window.GetInstanceID()+":"+language+":"+sprite;
            if(current!=owner){owner=current;Speech.Speak(repeat,interrupt:false);Monitor.Record("boss-rush-description-text",new{language,sprite,source="visually checked original sp_manual image",returnHint=back});}
            return true;
        }
        active=false;owner=repeat="";return false;
    }
}

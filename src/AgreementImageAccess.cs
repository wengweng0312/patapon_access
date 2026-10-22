using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace PataponAccess;
internal static class AgreementImageAccess
{
    private static string owner="",candidate="",previous="",selected="",repeat="";
    private static long changedAt,seen,lastError;
    private static HashSet<string> previousLines=new();
    private static readonly HashSet<string> unknownImages=new();
    public static string RepeatText=>Environment.TickCount64-seen<500?repeat:"";
    public static string ButtonImage(Image image)
    {
        if(!image||!image.isActiveAndEnabled||image.color.a<.05f||!image.sprite)return "";
        foreach(var canvas in image.GetComponentsInParent<Canvas>())if(!canvas.isActiveAndEnabled)return "";
        var renderer=image.canvasRenderer;
        if(renderer.cull||renderer.GetAlpha()*renderer.GetInheritedAlpha()<.05f)return "";
        var sprite=image.overrideSprite?image.overrideSprite:image.sprite;
        var rect=sprite.rect;var artwork=rect;
        try{artwork=sprite.textureRect;}catch{ /* Tight-packed sprites may not expose a rectangular texture region. */ }
        return ImageTextCatalog.ButtonText(sprite.name,(int)rect.width,(int)rect.height,(int)artwork.width,(int)artwork.height);
    }
    public static void Clear(){previous=candidate;}
    public static bool Scan()
    {
        try{
            foreach(var window in UnityEngine.Object.FindObjectsOfType<Px.AgreementWindowBase>()){
                if(!window||!window.gameObject.activeInHierarchy||window.IsClosed())continue;
                var languageWindow=window.languageSelectionWindow;
                if(languageWindow&&languageWindow.gameObject.activeInHierarchy&&!languageWindow.IsClosed()){
                    string native=string.Join("。",new[]{VisibleUiText.Read(languageWindow.descriptionText),VisibleUiText.Read(languageWindow.contentText)}.Where(s=>s.Length>0));
                    return Observe("language:"+languageWindow.GetInstanceID(),new[]{("native:"+native,native)},"");
                }
                var viewport=window.scrollRectInput?.scrollRect?.viewport;
                if(!viewport||window.scrollImages==null)continue;
                var lines=new List<(string key,string text)>();
                foreach(var image in window.scrollImages){
                    if(!image||!image.gameObject.activeInHierarchy||!image.isActiveAndEnabled||!image.sprite||image.color.a<.05f)continue;
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewport!,image.rectTransform);
                    var rect=viewport!.rect;
                    if(bounds.size.y<=0||bounds.max.y<=rect.yMin||bounds.min.y>=rect.yMax||bounds.max.x<=rect.xMin||bounds.min.x>=rect.xMax)continue;
                    var sprite=image.sprite;var page=ImageTextCatalog.Find(sprite.name,(int)sprite.rect.width,(int)sprite.rect.height);
                    if(page==null){
                        string key=sprite.name+":"+sprite.rect.width+":"+sprite.rect.height;
                        if(unknownImages.Count>64)unknownImages.Clear();
                        if(unknownImages.Add(key))Monitor.Record("agreement-image-unmatched",new{sprite=sprite.name,width=sprite.rect.width,height=sprite.rect.height,window=window.GetType().FullName});
                        continue;
                    }
                    double top=Math.Clamp((bounds.max.y-rect.yMax)/bounds.size.y,0,1),bottom=Math.Clamp((bounds.max.y-rect.yMin)/bounds.size.y,0,1);
                    lines.AddRange(ImageTextCatalog.VisibleLines(page,top,bottom));
                }
                string focus="";var go=EventSystem.current?.currentSelectedGameObject;
                if(go&&go!.transform.IsChildOf(window.transform))focus=string.Join("，",go.GetComponentsInChildren<TMP_Text>().Select(t=>VisibleUiText.Read(t)).Where(t=>t.Length>0).Distinct());
                if(lines.Count==0)return false; // No guessed text, another reader may own native UI.
                return Observe("agreement:"+window.GetInstanceID(),lines,focus);
            }
            if(Environment.TickCount64-seen>500){owner=candidate=previous=selected=repeat="";previousLines.Clear();}
        }catch(Exception e){if(Environment.TickCount64-lastError>5000){lastError=Environment.TickCount64;Monitor.Record("agreement-image-error",new{error=e.Message});}}
        return false;
    }
    private static bool Observe(string id,IEnumerable<(string key,string text)> source,string focus)
    {
        long now=Environment.TickCount64;var lines=source.Where(x=>x.text.Length>0).ToArray();
        if(lines.Length==0)return false;
        if(owner!=id||now-seen>500){owner=id;candidate=previous=selected="";previousLines.Clear();}
        seen=now;repeat=string.Join("\n",lines.Select(l=>l.text).Concat(new[]{focus}).Where(t=>t.Length>0));
        string signature=string.Join("|",lines.Select(l=>l.key+":"+l.text));
        if(signature!=candidate){candidate=signature;changedAt=now;}
        bool bodyReady=now-changedAt>=250&&signature!=previous;
        if(bodyReady){
            string text=string.Join("\n",lines.Where(l=>!previousLines.Contains(l.key)).Select(l=>l.text));
            previous=signature;previousLines=lines.Select(l=>l.key).ToHashSet();
            if(text.Length>0){if(selected.Length==0&&focus.Length>0)text+="。"+focus;Speech.Speak(text,interrupt:false);Monitor.Record("agreement-image-readout",new{owner=id,text,source="loaded-sprite-transcription-visible-line-range"});}
        }
        if(focus.Length>0&&focus!=selected){Speech.Speak(focus);selected=focus;}
        return true;
    }
}

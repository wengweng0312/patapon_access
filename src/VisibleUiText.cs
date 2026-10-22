using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace PataponAccess;

internal static class VisibleUiText
{
    private static bool Readable(Component text,bool active,int characters,float alpha)
    {
        if(!text||!active||characters<=0)return false;
        var renderer=text.GetComponent<CanvasRenderer>();
        if(renderer)alpha*=renderer.GetAlpha()*renderer.GetInheritedAlpha();
        bool canvasEnabled=true;
        foreach(var canvas in text.GetComponentsInParent<Canvas>()){
            if(!canvas.isActiveAndEnabled)canvasEnabled=false;
        }
        return VisibleTextPolicy.Readable(active,characters,alpha,renderer&&renderer.cull,canvasEnabled,false);
    }
    // These two TMP components generate Tips images; they are not foreground UI
    // merely because the capture canvas is active. Expanded Tips has a native reader.
    public static HashSet<int> TipsCaptureTextIds()
    {
        var ids=new HashSet<int>();
        void Add(TMP_Text? t){if(t)ids.Add(t!.GetInstanceID());}
        foreach(var image in UnityEngine.Object.FindObjectsOfType<P1.GameSystem.Tips.TipsImage>()){Add(image.titleText_);Add(image.descText_);}
        foreach(var image in UnityEngine.Object.FindObjectsOfType<P2.GameSystem.Tips.TipsImage>()){Add(image.titleText_);Add(image.descText_);}
        return ids;
    }
    public static string Read(TMP_Text? text,HashSet<int>? captureIds=null)
    {
        if(!text||captureIds?.Contains(text!.GetInstanceID())==true||!Readable(text!,text!.isActiveAndEnabled,text.maxVisibleCharacters,text.color.a))return "";
        return VisibleTextPolicy.Text(text.text,text.firstVisibleCharacter,text.maxVisibleCharacters);
    }
    public static string Read(Text? text)
    {
        if(!text||!Readable(text!,text!.isActiveAndEnabled,int.MaxValue,text.color.a))return "";
        return VisibleTextPolicy.Text(text.text,0,int.MaxValue);
    }
}

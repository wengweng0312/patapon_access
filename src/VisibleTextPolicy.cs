using System.Text;
using System.Text.RegularExpressions;
namespace PataponAccess;

internal static class VisibleTextPolicy
{
    public static bool Readable(bool active,int visibleCharacters,float alpha,bool culled,bool canvasEnabled,bool textureCapture)
        =>active&&visibleCharacters>0&&float.IsFinite(alpha)&&alpha>=0.05f&&!culled&&canvasEnabled&&!textureCapture;
    public static string Text(string raw,int first,int maximum)
    {
        if(maximum<=first||first<0)return "";
        string plain=Regex.Replace(raw??"",@"<[^>]*>","");
        var text=new StringBuilder();int index=0;
        foreach(var rune in plain.EnumerateRunes()){
            if(index>=maximum)break;
            if(index++>=first)text.Append(rune.ToString());
        }
        return Regex.Replace(text.ToString(),@"\s+"," ").Trim();
    }
}

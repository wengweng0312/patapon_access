using System.Text.RegularExpressions;

namespace PataponAccess;

internal static class AttackCueText
{
    private static readonly HashSet<string> nativeLines=new(StringComparer.Ordinal);
    public static void RegisterNative(IEnumerable<string> lines)
    {
        nativeLines.Clear();foreach(var line in lines)if(!string.IsNullOrWhiteSpace(line))nativeLines.Add(Normalize(line));
    }
    private static string Normalize(string text)=>Regex.Replace(Regex.Replace(text??"",@"<[^>]*>",""),@"[\s/ー～〜\-—－]","");
    public static bool Matches(string text)
    {
        string value=Regex.Replace(text??"",@"<[^>]*>","");
        value=Regex.Replace(value,@"[\s/ー～〜\-—－]","");
        return nativeLines.Contains(value)||value.Contains("攻擊預備",System.StringComparison.Ordinal);
    }
}

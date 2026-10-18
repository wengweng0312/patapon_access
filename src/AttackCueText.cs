using System.Text.RegularExpressions;

namespace PataponAccess;

internal static class AttackCueText
{
    public static bool Matches(string text)
    {
        string value=Regex.Replace(text??"",@"<[^>]*>","");
        value=Regex.Replace(value,@"[\s/ー～〜\-—－]","");
        return value.Contains("攻擊預備",System.StringComparison.Ordinal);
    }
}

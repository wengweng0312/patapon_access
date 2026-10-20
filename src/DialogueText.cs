namespace PataponAccess;

internal static class DialogueText
{
    public const int LevelResolutionTimeoutMs=350;
    public static bool HasLevelToken(string text)=>System.Text.RegularExpressions.Regex.IsMatch(text??"",@"Lv\.\s*<N\d+>",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public const int NameResolutionTimeoutMs=200;
    public static bool HasVariableToken(string text)=>System.Text.RegularExpressions.Regex.IsMatch(text??"",@"<N\d+>",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public static string ResolveLevel(string template,string rendered)=>HasLevelToken(template)?ResolveTemplate(template,rendered):"";
    public static string ResolveTemplate(string template,string rendered)
    {
        if(!HasVariableToken(template)||HasVariableToken(rendered))return "";
        string literal=Clean(System.Text.RegularExpressions.Regex.Replace(template,@"<N\d+>",""));
        if(literal.Count(char.IsLetterOrDigit)<3)return "";
        var tokens=System.Text.RegularExpressions.Regex.Matches(template,@"Lv\.\s*(<N\d+>)",System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(m=>m.Groups[1].Index).ToHashSet();
        string marked=System.Text.RegularExpressions.Regex.Replace(template,@"<N\d+>",m=>tokens.Contains(m.Index)?"\uE000":"\uE001");
        string pattern="^"+System.Text.RegularExpressions.Regex.Escape(Clean(marked)).Replace("\uE000",@"\d+").Replace("\uE001",@".{1,128}?")+"$";
        string value=Clean(rendered);
        return System.Text.RegularExpressions.Regex.IsMatch(value,pattern)?value:"";
    }
    public static string Clean(string raw)
    {
        string text=System.Text.RegularExpressions.Regex.Replace(raw??"",@"&[A-Za-z][A-Za-z0-9.,+\-]*#","");
        text=text.Replace('/', '\n');
        return System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(text,@"<[^>]*>"," "),@"\s+"," ").Trim();
    }
}


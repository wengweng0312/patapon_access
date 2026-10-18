namespace PataponAccess;

internal static class DialogueText
{
    public static bool HasLevelToken(string text)=>System.Text.RegularExpressions.Regex.IsMatch(text??"",@"Lv\.\s*<N\d+>",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public static string ResolveLevel(string template,string rendered)
    {
        if(!HasLevelToken(template))return "";
        string marked=System.Text.RegularExpressions.Regex.Replace(template,@"<N\d+>","\uE000");
        string pattern="^"+System.Text.RegularExpressions.Regex.Escape(Clean(marked)).Replace("\uE000",@"\d+")+"$";
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


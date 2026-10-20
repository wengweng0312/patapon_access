namespace PataponAccess;
internal static class VariableTemplates
{
    private static readonly Dictionary<string,(string raw,long at)> recent=new();
    public static void Observe(string raw,long now)
    {
        if(!DialogueText.HasVariableToken(raw))return;
        if(recent.Count>256)recent.Clear();recent[DialogueText.Clean(raw)]=(raw,now);
    }
    public static string? Find(string text,long now)=>recent.TryGetValue(DialogueText.Clean(text),out var value)&&now-value.at<2000?value.raw:null;
}

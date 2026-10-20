namespace PataponAccess;
// Shared by P1/P2/P1S/P2S command windows. Commit the introduction only when
// speech is taken, so rapid cursor moves during debounce cannot swallow it.
internal sealed class CommandDialogSpeech
{
    private string key="",prompt="",selection="";
    private long seen;
    private bool introduced;
    public static bool IsChoice(string value)=>value.Trim().ToLowerInvariant() is
        "是" or "否" or "確定" or "确定" or "取消" or "はい" or "いいえ" or
        "yes" or "no" or "ok" or "cancel";
    public bool Observe(string owner,string title,string body,string selected,string commands,long now)
    {
        string identity=owner+"|"+title+"|"+body+"|"+commands;
        bool entered=key!=identity||now-seen>500;
        if(entered)introduced=false;
        key=identity;seen=now;
        prompt=Join(title,body);selection=selected;
        return entered;
    }
    public string Repeat=>Join(prompt,selection);
    public string Take(){if(selection.Length==0)return "";string text=introduced?selection:Repeat;introduced=true;return text;}
    public void Reset(){key=prompt=selection="";seen=0;introduced=false;}
    private static string Join(params string[] values)=>string.Join("，",values.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
}

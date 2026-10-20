namespace PataponAccess;
internal sealed class CampHintMemory
{
    private readonly Dictionary<string,string> templates=new(StringComparer.Ordinal);
    private readonly HashSet<string> spoken=new(StringComparer.Ordinal);
    private IntPtr owner;
    private (string key,string text,long at) offered;
    public bool Record(IntPtr current,uint id,string raw)
    {
        if(current!=owner){owner=current;templates.Clear();NewVisit();}
        if(!CampHintRules.IsProtected("P2",id)||string.IsNullOrWhiteSpace(raw))return false;
        string text=DialogueText.Clean(raw),key="P2:colony:"+id;
        if(templates.TryGetValue(text,out var old)&&old==key)return false;
        templates[text]=key;return true;
    }
    public bool Identify(string game,string raw,out string key)
    {key="";return game.StartsWith("P2",StringComparison.Ordinal)&&templates.TryGetValue(DialogueText.Clean(raw),out key!);}
    public bool WasSpoken(string key)=>spoken.Contains(key);
    public void Offer(string key,string text,long now){if(key.Length>0)offered=(key,DialogueText.Clean(text),now);}
    public void Dispatched(string text,bool accepted,long now)
    {
        if(offered.key==null||DialogueText.Clean(text)!=offered.text)return;
        if(accepted&&now-offered.at is >=0 and <1000)spoken.Add(offered.key);
        offered=default;
    }
    public void NewVisit(){spoken.Clear();offered=default;}
}

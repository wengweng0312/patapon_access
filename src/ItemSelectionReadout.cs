namespace PataponAccess;
// Per native window visit: deduplicate content independently of the spoken header.
internal sealed class ItemSelectionReadout
{
    private string owner="",last="";
    private bool introduced;
    public void Close(string key){if(key==owner){owner=last="";introduced=false;}}
    public string? Observe(string key,string header,string content)
    {
        if(string.IsNullOrWhiteSpace(content))return null;
        if(key!=owner){owner=key;last="";introduced=false;}
        if(content==last)return null;
        last=content;bool first=!introduced;introduced=true;
        return first?header+"，"+content:content;
    }
}

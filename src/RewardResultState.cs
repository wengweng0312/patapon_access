namespace PataponAccess;

// Managed snapshots only. A displayed row is a name, not proof of a quantity.
internal sealed class RewardResultState
{
    private string owner="",signature="",spoken="";
    private string[] names=Array.Empty<string>();
    private HashSet<int> itemIndexes=new();
    private long seen,changed;
    private bool observed,ready;
    public bool Active(long now)=>observed&&now>=seen&&now-seen<500;
    public IReadOnlyList<string> Names=>names;
    public void Reset(){owner=signature=spoken="";names=Array.Empty<string>();itemIndexes.Clear();seen=changed=0;observed=ready=false;}
    public void Observe(string source,IEnumerable<string> rows,bool controllable,long now,IEnumerable<int>? itemRows=null)
    {
        if(owner!=source){Reset();owner=source;}
        string[] snapshot=rows.Where(s=>!string.IsNullOrWhiteSpace(s)).ToArray();
        var indexes=new HashSet<int>(itemRows??Array.Empty<int>());
        string next=string.Join("\u001f",snapshot)+"|items:"+string.Join(",",indexes.OrderBy(i=>i));
        if(!observed||signature!=next){names=snapshot;itemIndexes=indexes;signature=next;changed=now;}
        observed=true;seen=now;ready=controllable;
    }
    public string Describe()
    {
        if(names.Length==0)return Unavailable;
        var counts=itemIndexes.Where(i=>i>=0&&i<names.Length).GroupBy(i=>names[i],StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
        var emitted=new HashSet<string>(StringComparer.Ordinal);var output=new List<string>();
        for(int i=0;i<names.Length;i++){
            string name=names[i];
            if(!itemIndexes.Contains(i)){output.Add(name);continue;}
            if(!emitted.Add(name))continue;
            output.Add(counts[name]>1?name+"，"+counts[name].ToString(System.Globalization.CultureInfo.InvariantCulture):name);
        }
        return string.Join("。",output);
    }
    public static string Unavailable=>I18n.T("reward.unavailable","戰鬥結算。目前沒有可朗讀的戰利品資料。");
    public string Take(long now)
    {
        if(!Active(now)||!ready||names.Length==0||now-changed<250||spoken==signature)return "";
        spoken=signature;return Describe();
    }
}

namespace PataponAccess;

// Managed snapshots only. A displayed row is a name, not proof of a quantity.
internal sealed class RewardResultState
{
    private string owner="",signature="",spoken="";
    private string[] names=Array.Empty<string>();
    private long seen,changed;
    private bool observed,ready;
    public bool Active(long now)=>observed&&now>=seen&&now-seen<500;
    public IReadOnlyList<string> Names=>names;
    public void Reset(){owner=signature=spoken="";names=Array.Empty<string>();seen=changed=0;observed=ready=false;}
    public void Observe(string source,IEnumerable<string> rows,bool controllable,long now)
    {
        if(owner!=source){Reset();owner=source;}
        string[] snapshot=rows.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToArray();
        string next=string.Join("\u001f",snapshot);
        if(!observed||signature!=next){names=snapshot;signature=next;changed=now;}
        observed=true;seen=now;ready=controllable;
    }
    public string Describe()=>names.Length>0
        ?I18n.T("ui.aac9c0e4dc1b","戰鬥結算，獲得物品：")+string.Join("、",names)
        :Unavailable;
    public static string Unavailable=>I18n.T("reward.unavailable","戰鬥結算。目前沒有可朗讀的戰利品資料。");
    public string Take(long now)
    {
        if(!Active(now)||!ready||names.Length==0||now-changed<250||spoken==signature)return "";
        spoken=signature;return Describe();
    }
}

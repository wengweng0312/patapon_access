namespace PataponAccess;
// Shared diagnostic transition state; never participates in speech decisions.
internal sealed class PageSnapshotState
{
    private string signature="";
    private readonly object gate=new();
    private readonly HashSet<string> reasons=new(StringComparer.Ordinal);
    public long Revision {get;private set;}
    public void Request(string reason){lock(gate){if(reasons.Count<8)reasons.Add(reason);}}
    public bool Pending {get{lock(gate)return reasons.Count>0;}}
    public string[] Reasons {get{lock(gate)return reasons.OrderBy(x=>x,StringComparer.Ordinal).ToArray();}}
    public bool Commit(string value)
    {
        lock(gate){reasons.Clear();if(value==signature)return false;
        signature=value;Revision++;return true;}
    }
}

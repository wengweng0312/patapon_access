namespace PataponAccess;
internal sealed class MiningRewardBatch
{
    private readonly Dictionary<uint,int> items=new();
    private long changed;
    public void Add(uint nameId,int count,long now){if(count<=0)return;items[nameId]=items.GetValueOrDefault(nameId)+count;changed=now;}
    public bool Ready(long now)=>items.Count>0&&now-changed>=500;
    public IReadOnlyDictionary<uint,int> Items=>items;
    public void Clear()=>items.Clear();
}

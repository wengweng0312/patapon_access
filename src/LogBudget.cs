namespace PataponAccess;
internal sealed class LogBudget
{
    private long used;
    public long Limit {get;}
    public LogBudget(long totalLimit,long existingBytes,long sessionLimit)
        =>Limit=Math.Max(0,Math.Min(sessionLimit,totalLimit-existingBytes));
    public bool Take(long bytes)
    {if(bytes<0||bytes>Limit-used)return false;used+=bytes;return true;}
}

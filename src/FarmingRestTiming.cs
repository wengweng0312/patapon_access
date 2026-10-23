namespace PataponAccess;
internal sealed class FarmingRestTiming
{
    private readonly Dictionary<long,(int rests,double previous,bool done)> gaps=new();
    public static int RestCount(double spacing)
    {
        if(!double.IsFinite(spacing))return 0;
        int beats=(int)Math.Round(Math.Clamp(spacing,0,10));
        return beats is 2 or 3 or 4 && Math.Abs(spacing-beats)<=0.12?beats-1:0;
    }
    public void Register(long id,double spacing)
    {int rests=RestCount(spacing);if(rests>0&&!gaps.ContainsKey(id))gaps[id]=(rests,double.PositiveInfinity,false);}
    public bool Poll(long id,double nextNoteBeats,out int rests)
    {
        rests=0;
        if(!gaps.TryGetValue(id,out var gap)||gap.done||!double.IsFinite(nextNoteBeats))return false;
        double remaining=nextNoteBeats-gap.rests;
        // Warn one whole beat before the first empty beat; stop on the next beat.
        bool backwards=remaining>gap.previous+0.25;
        bool due=remaining<=1;
        gaps[id]=(gap.rests,remaining,backwards||due);
        if(backwards||!due||remaining<0.88)return false;
        rests=gap.rests;return true;
    }
    public void Remove(long id)=>gaps.Remove(id);
    public void Clear()=>gaps.Clear();
}

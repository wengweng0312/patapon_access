namespace PataponAccess;
internal sealed class DanceNoteTiming
{
    private readonly Dictionary<long,(double previous,bool done)> notes=new();
    public void Register(long id)=>notes[id]=(double.PositiveInfinity,false);
    public void Remove(long id)=>notes.Remove(id);
    public void Clear()=>notes.Clear();
    public static double RemainingBeats(double x,double target,double halfBeatLength)
        =>double.IsFinite(x)&&double.IsFinite(target)&&double.IsFinite(halfBeatLength)&&Math.Abs(halfBeatLength)>0.0001?(target-x)/(2*halfBeatLength):double.NaN;
    public bool Poll(long id,double remaining,double frameAdvance=0)
    {
        if(!double.IsFinite(remaining)||!notes.TryGetValue(id,out var note)||note.done)return false;
        if(remaining>note.previous+0.25){notes[id]=(remaining,true);return false;}
        double threshold=1+(double.IsFinite(frameAdvance)?Math.Clamp(frameAdvance,0,0.08):0);
        bool due=remaining<=threshold;
        notes[id]=(remaining,due);
        // Never emit a delayed catch-up click after a stall/seek.
        return due&&remaining>=threshold-0.12;
    }
}

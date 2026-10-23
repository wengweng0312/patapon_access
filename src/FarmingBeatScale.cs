namespace PataponAccess;
// Calibrate against the actual movement on screen. MusicScore's named
// half-beat unit is not necessarily half a musical quarter note in Bean.
internal sealed class FarmingBeatScale
{
    private long key,at;
    private double x,speed=double.NaN;
    private readonly Queue<double> windows=new();
    public void Clear(){key=at=0;speed=double.NaN;windows.Clear();}
    public void Observe(long id,double position,long milliseconds)
    {
        if(!double.IsFinite(position))return;
        if(key!=id||milliseconds-at>450||milliseconds<at){key=id;x=position;at=milliseconds;return;}
        long elapsed=milliseconds-at;if(elapsed<200)return;
        double next=(position-x)/elapsed;
        if(next>=0||Math.Abs(next)>10){Clear();return;}
        windows.Enqueue(next);while(windows.Count>7)windows.Dequeue();
        // Reject short-lived scheduler jitter without changing the song tempo.
        var sorted=windows.OrderBy(v=>v).ToArray();speed=sorted[sorted.Length/2];
        x=position;at=milliseconds;
    }
    public double BeatLength(uint tempo)=>tempo is >=40 and <=300&&double.IsFinite(speed)?speed*60000/tempo:double.NaN;
}

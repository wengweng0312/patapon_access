namespace PataponAccess;
// Diagnostic only. Never returns a timing decision to the cue algorithms.
internal static class MinigameCueTrace
{
    private sealed class Note
    {
        public long Sequence;public uint Beat;public double Registered,Previous=double.NaN,PreviousAt,FirstTarget=double.NaN;
        public bool Cued,Crossed;
    }
    private static long sequence;
    private static readonly Dictionary<(string,long),Note> notes=new();
    public static void Register(string game,long key,uint beat)
    {
        if(notes.Count>512){notes.Clear();Monitor.Record("minigame-timing-reset",new{reason="diagnostic-note-cap"});}
        notes[(game,key)]=new(){Sequence=++sequence,Beat=beat,Registered=MinigameClock.NowMs};
    }
    public static void Observe(string game,long key,double remaining,uint tempo,Func<object?> clock)
    {
        if(!notes.TryGetValue((game,key),out var note)||!double.IsFinite(remaining)||tempo is <40 or >300)return;
        double now=MinigameClock.NowMs,beatMs=60000.0/tempo;
        if(!double.IsFinite(note.FirstTarget))note.FirstTarget=now+remaining*beatMs;
        if(!note.Crossed&&note.Previous>0&&remaining<=0&&double.IsFinite(note.Previous)){
            note.Crossed=true;
            double crossing=note.PreviousAt+(now-note.PreviousAt)*note.Previous/(note.Previous-remaining);
            Monitor.Record("minigame-note-crossing",new{game,note_sequence=note.Sequence,beat_index=note.Beat,observed_time=now,visual_crossing_time=crossing,first_target_estimate=note.FirstTarget,estimate_error_ms=crossing-note.FirstTarget,cue_requested=note.Cued,native_clock=clock(),time_unit="milliseconds",clock_source="Stopwatch-monotonic",meaning="interpolated visual line crossing; not audio onset or native input judgement"});
        }
        note.Previous=remaining;note.PreviousAt=now;
    }
    public readonly record struct Pending(string Game,long Sequence,uint Beat,double Requested,double Registered,double Target,double FirstTarget,double Scheduled,object? NativeClock);
    public static Pending? Begin(string game,long key,double remaining,uint tempo,object? nativeClock)
    {
        if(!notes.TryGetValue((game,key),out var note)||tempo is <40 or >300||!double.IsFinite(remaining))return null;
        note.Cued=true;double now=MinigameClock.NowMs,target=now+remaining*60000.0/tempo;
        if(!double.IsFinite(note.FirstTarget))note.FirstTarget=target;
        return new(game,note.Sequence,note.Beat,now,note.Registered,target,note.FirstTarget,target-60000.0/tempo,nativeClock);
    }
    public static void End(Pending? pending,bool success)
    {
        if(pending is not {} p)return;
        Monitor.Record("minigame-cue-timing",new{game=p.Game,note_sequence=p.Sequence,beat_index=p.Beat,registered_time=p.Registered,target_time=p.Target,first_target_estimate=p.FirstTarget,scheduled_time=p.Scheduled,requested_time=p.Requested,api_call_time=CueAudio.LastApiCallMs,api_return_time=CueAudio.LastApiReturnMs,actual_play_time=(double?)null,nominal_request_error_ms=p.Requested-p.Scheduled,success,native_clock=p.NativeClock,time_unit="milliseconds",clock_source="Stopwatch-monotonic",target_source="visual-prediction-not-native-input-judgement",scheduling_mode="visual-threshold-polled-not-an-OS-audio-schedule",meaning="actual audible onset is unknown; API call time is recorded separately"});
    }
    public static void Remove(string game,long key)=>notes.Remove((game,key));
    public static void Clear(string game){foreach(var key in notes.Keys.Where(k=>k.Item1==game).ToArray())notes.Remove(key);}
}

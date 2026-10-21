namespace PataponAccess;
internal static class ObjectRangeCue
{
    private static readonly RangeTransition state=new();
    private static bool enabled;
    private static int pending;
    private static long requestedAt;
    private static string game="";
    public static void Initialize(bool value)
    {
        enabled=value;
        try{
            string folder=Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!;
            foreach(bool enter in new[]{true,false}){
                string file=Path.Combine(folder,enter?"object-range-enter.wav":"object-range-leave.wav");
                if(!File.Exists(file))File.WriteAllBytes(file,ObjectRangeWave.Build(enter));
                CueAudio.Prepare(file);
            }
        }catch(Exception e){Monitor.Record("object-range-audio-error",new{error=e.Message});}
    }
    public static void Reset(){state.Reset();pending=0;game="";}
    public static void Observe(string currentGame,int longCount,int shortCount)
    {
        int max=currentGame=="P1"?3:currentGame=="P2"?4:-1;
        if(longCount<0||longCount>max||shortCount<0||shortCount>max)return;
        long now=Environment.TickCount64;
        int change=state.Observe(longCount>0,now);if(change==0)return;
        Monitor.Record(change>0?"object-range-enter":"object-range-leave",new{game=currentGame,longCount,shortCount,meaning="Native attackable scenery sight flag; no target identity, hit or reward guarantee."});
        if(enabled){pending=change;requestedAt=now;game=currentGame;}
    }
    public static void Scan()
    {
        if(pending==0)return;
        long now=Environment.TickCount64;
        if(now-requestedAt>1500){pending=0;Monitor.Record("object-range-cue-expired",new{game,reason="deferred behind another cue"});return;}
        // Shared WinMM output: avoid replacing a just-started higher-priority cue.
        if(now-CueAudio.LastPlaybackAt<450)return;
        int direction=pending;pending=0;
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,direction>0?"object-range-enter.wav":"object-range-leave.wav");
        bool success=File.Exists(file)&&CueAudio.Play(file);
        Monitor.Record("object-range-cue",new{game,direction=direction>0?"enter":"leave",success});
    }
}

namespace PataponAccess;
internal static class MinigameClock
{
    private static readonly long origin=System.Diagnostics.Stopwatch.GetTimestamp();
    public static double NowMs=>(System.Diagnostics.Stopwatch.GetTimestamp()-origin)*1000.0/System.Diagnostics.Stopwatch.Frequency;
    public static object? Snapshot(P2.System.Sound.BeatTimer? timer)
    {
        if(timer==null)return null;
        static float? Finite(float value)=>float.IsFinite(value)?value:null;
        try{return new{observed_time=NowMs,pointer=timer.Pointer.ToInt64(),tick=timer.tick_,pulseTick=timer.pulseTick_,halfCount=timer.halfBeatCount_,tempo=timer.tempo_,userTempo=Finite(timer.userTempo_),beatPhase=Finite(timer.rateToBeatLine_),halfPhase=Finite(timer.rateToHalfLine_),manualCompensation=timer.manualCompensationCount_,compensated=timer.isCompensationed_,paused=timer.isPause_,native_audio=AudioSnapshot(timer)};}
        catch(Exception e){return new{unavailable=true,error=e.Message};}
    }
    // Read existing player/mini-game pool sources only. Do not create/play
    // sources or mistake a CRI playback position for physical speaker onset.
    private static object? AudioSnapshot(P2.System.Sound.BeatTimer timer)
    {
        try {
            var player=timer.metronome_;if(player==null)return null;
            var sources=player.soundSources_;var observations=new List<object>();
            int playerSources=sources?.Count??0;
            // The BeatTimer metronome is a silent timing player in observed
            // Bell/Bean runs. Actual CRI mini-game voices live in the pool.
            bool fromPool=playerSources==0;
            if(fromPool)sources=CriSoundManager.poolManager?.PoolSoundSource;
            int scanned=0;
            if(sources!=null)for(int i=0;i<Math.Min(sources.Count,256)&&observations.Count<8;i++) {
                scanned++;
                var source=sources[i];if(source==null||source.returned||source.criAtomSource==null)continue;
                if(fromPool&&(source.fileType!=CriSoundManager.SoundGameFileType.P2||source.category!=CriSoundManager.MixingCategory.MixingCategory_MiniGame))continue;
                try {
                    double before=NowMs;long position=source.getCriTime();double after=NowMs;
                    observations.Add(new{pointer=source.Pointer.ToInt64(),source.key,source.acbFilePath,source.prog,source.cueId,source.globalCounter,source.loop,position_ms=position,observed_before_ms=before,observed_after_ms=after,position_source="CRI-source-GetTime-not-audible-onset"});
                }catch(Exception e){observations.Add(new{unavailable=true,error=e.Message});}
            }
            return new{player=player.Pointer.ToInt64(),player.playId_,player.resourceId_,player.isPause_,player_sources=playerSources,route=fromPool?"P2-mini-game-CRI-pool":"metronome-player",listed_sources=sources?.Count??0,scanned,scan_limit=256,sampled_limit=8,sources=observations};
        }catch(Exception e){return new{unavailable=true,error=e.Message};}
    }
}

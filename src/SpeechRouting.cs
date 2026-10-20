namespace PataponAccess;
internal record SpeechDispatch(bool Accepted,string Backend,string Reason,int? Result=null);
internal interface ISpeechDriver : IDisposable
{
    SpeechDispatch Speak(string text,bool interrupt=true);
    bool Stop();
}
internal sealed class SpeechRouter : IDisposable
{
    private readonly ISpeechDriver? primary;
    private readonly ISpeechDriver fallback;
    private ISpeechDriver? last;
    private bool closed;
    public SpeechRouter(ISpeechDriver? primary,ISpeechDriver fallback){this.primary=primary;this.fallback=fallback;}
    public SpeechDispatch Speak(string text,bool interrupt=true)
    {
        if(closed)return new(false,"none","speech-closed");
        // Keep queued speech on its active backend until an explicit interaction.
        // Recovering Tolk must not cancel speech already queued on direct NVDA.
        if(!interrupt&&last!=null) {
            var queued=TrySpeak(last,text,false);
            if(queued.Accepted)return queued;
        }
        string primaryFailure="disabled";
        if(primary!=null){
            if(last!=null&&last!=primary)TryStop(last);
            var result=TrySpeak(primary,text,interrupt);
            if(result.Accepted){last=primary;return result;}
            primaryFailure=result.Reason;
        }
        var backup=TrySpeak(fallback,text,interrupt);
        if(backup.Accepted)last=fallback;
        return backup with{Reason=backup.Reason+";tolk="+primaryFailure};
    }
    private static SpeechDispatch TrySpeak(ISpeechDriver driver,string text,bool interrupt){try{return driver.Speak(text,interrupt);}catch(Exception e){return new(false,"unavailable",e.GetType().Name+": "+e.Message);}}
    private static bool TryStop(ISpeechDriver driver){try{return driver.Stop();}catch{return false;}}
    public bool Stop(){if(closed)return false;return last!=null?TryStop(last):primary!=null&&TryStop(primary)||TryStop(fallback);}
    public void Dispose(){if(closed)return;Stop();closed=true;try{primary?.Dispose();}finally{fallback.Dispose();}last=null;}
}

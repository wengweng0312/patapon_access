using System.Runtime.InteropServices;
namespace PataponAccess;
internal sealed class TolkSpeechDriver : ISpeechDriver
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void VoidCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] private delegate bool BoolCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetBool([MarshalAs(UnmanagedType.I1)] bool value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr PointerCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.I1)] private delegate bool OutputCall([MarshalAs(UnmanagedType.LPWStr)] string text,[MarshalAs(UnmanagedType.I1)] bool interrupt);
    private IntPtr handle;
    private VoidCall? unload;
    private BoolCall? loaded,hasSpeech,silence;
    private PointerCall? detect;
    private OutputCall? output;
    private readonly int thread=Environment.CurrentManagedThreadId;
    private bool initialized,closed;
    public string Initialization {get;private set;}="not-loaded";
    private T Bind<T>(string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(handle,name));
    public TolkSpeechDriver(string path,bool windowsFallback)
    {
        try{
            handle=NativeLibrary.Load(Path.GetFullPath(path));
            var load=Bind<VoidCall>("Tolk_Load");unload=Bind<VoidCall>("Tolk_Unload");loaded=Bind<BoolCall>("Tolk_IsLoaded");hasSpeech=Bind<BoolCall>("Tolk_HasSpeech");silence=Bind<BoolCall>("Tolk_Silence");detect=Bind<PointerCall>("Tolk_DetectScreenReader");output=Bind<OutputCall>("Tolk_Output");
            Bind<SetBool>("Tolk_TrySAPI")(windowsFallback);
            initialized=true;load();Initialization=loaded()?"loaded":"load-reported-failure";
        }catch(Exception e){Initialization=e.GetType().Name+": "+e.Message;Dispose();}
    }
    public string Detect()=>!closed&&Environment.CurrentManagedThreadId==thread&&detect!=null?Marshal.PtrToStringUni(detect())??"none":"unavailable";
    public SpeechDispatch Speak(string text,bool interrupt=true)
    {
        if(closed||!initialized)return new(false,"Tolk","tolk-unavailable: "+Initialization);
        if(Environment.CurrentManagedThreadId!=thread)return new(false,"Tolk","tolk-thread-mismatch");
        string name=Detect();
        if(loaded?.Invoke()!=true||hasSpeech?.Invoke()!=true)return new(false,"Tolk/"+name,"no-speech-backend");
        bool accepted=output?.Invoke(text,interrupt)==true;
        return new(accepted,"Tolk/"+name,accepted?"backend-accepted":"backend-rejected");
    }
    public bool Stop()=>!closed&&initialized&&Environment.CurrentManagedThreadId==thread&&silence?.Invoke()==true;
    public void Dispose()
    {
        if(closed)return;closed=true;
        // COM-backed drivers must be unloaded on the initialization thread.
        // On an unexpected thread, retain the module until process termination.
        if(Environment.CurrentManagedThreadId!=thread)return;
        if(initialized){unload?.Invoke();initialized=false;}
        if(handle!=IntPtr.Zero){NativeLibrary.Free(handle);handle=IntPtr.Zero;}
        output=null;detect=null;loaded=hasSpeech=silence=null;unload=null;
    }
}
internal sealed class NvdaSpeechDriver : ISpeechDriver
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Unicode)] private delegate int SayCall([MarshalAs(UnmanagedType.LPWStr)] string text);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StatusCall();
    private IntPtr handle;
    private SayCall? say;
    private StatusCall? cancel,running;
    private bool closed;
    public string Initialization {get;private set;}="not-loaded";
    public NvdaSpeechDriver(string path)
    {
        try{handle=NativeLibrary.Load(Path.GetFullPath(path));say=Marshal.GetDelegateForFunctionPointer<SayCall>(NativeLibrary.GetExport(handle,"nvdaController_speakText"));cancel=Marshal.GetDelegateForFunctionPointer<StatusCall>(NativeLibrary.GetExport(handle,"nvdaController_cancelSpeech"));running=Marshal.GetDelegateForFunctionPointer<StatusCall>(NativeLibrary.GetExport(handle,"nvdaController_testIfRunning"));Initialization="loaded";}
        catch(Exception e){Initialization=e.GetType().Name+": "+e.Message;Dispose();}
    }
    public SpeechDispatch Speak(string text,bool interrupt=true)
    {
        if(closed||running?.Invoke()!=0)return new(false,"NVDA-direct","controller-unavailable");
        if(interrupt)cancel?.Invoke();int? result=say?.Invoke(text);return new(result==0,"NVDA-direct",result==0?"controller-accepted":"controller-error",result);
    }
    public bool Stop()=>!closed&&cancel?.Invoke()==0;
    public void Dispose(){if(closed)return;closed=true;say=null;cancel=running=null;if(handle!=IntPtr.Zero){NativeLibrary.Free(handle);handle=IntPtr.Zero;}}
}

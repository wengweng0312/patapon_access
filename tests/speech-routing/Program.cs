using PataponAccess;
int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var primary=new Fake("Tolk/JAWS",true);var fallback=new Fake("NVDA-direct",true);using(var router=new SpeechRouter(primary,fallback)){
 var result=router.Speak("神槍PON，日本語，English");Check(result.Accepted&&result.Backend=="Tolk/JAWS","Tolk selected");Check(fallback.Spoken.Count==0,"no duplicate NVDA speech");Check(primary.Spoken.Single()=="神槍PON，日本語，English","unicode passed unchanged");Check(router.Stop()&&primary.Stops==1&&fallback.Stops==0,"stop active Tolk only");
 primary.Accept=false;result=router.Speak("備援");Check(result.Accepted&&result.Backend=="NVDA-direct"&&fallback.Spoken.Count==1,"Tolk rejection falls back");Check(router.Stop()&&fallback.Stops==1,"stop follows fallback");
 primary.Accept=true;result=router.Speak("恢復");Check(result.Backend=="Tolk/JAWS"&&fallback.Stops==2,"stop prior fallback before returning to Tolk");
 primary.Throws=true;result=router.Speak("例外備援");Check(result.Accepted&&result.Backend=="NVDA-direct","managed Tolk failure falls back");
 router.Dispose();router.Dispose();Check(primary.Disposals==1&&fallback.Disposals==1,"both drivers disposed exactly once");Check(!router.Speak("已關閉").Accepted,"shutdown rejects later speech");Check(!router.Stop(),"shutdown rejects later stop");
}
var none=new Fake("NVDA-direct",false);using(var router=new SpeechRouter(null,none)){Check(!router.Speak("沒有報讀軟體").Accepted,"no screen reader reports failure");}
var queuePrimary=new Fake("Tolk/NVDA",true);var queueFallback=new Fake("NVDA-direct",true);
using(var queuedRouter=new SpeechRouter(queuePrimary,queueFallback)) {
 queuedRouter.Speak("劇情第一句",false);queuedRouter.Speak("劇情第二句",false);
 Check(queuePrimary.Interrupts.SequenceEqual(new[]{false,false}),"automatic dialogue requests native queue without interrupts");
 Check(queuePrimary.Stops==0,"queued dialogue does not silence its active reader");
 queuedRouter.Speak("選單操作");Check(queuePrimary.Interrupts.Last(),"explicit operation still interrupts immediately");
 Check(queuedRouter.Stop()&&queuePrimary.Stops==1,"stop clears the active backend queue");
 queuePrimary.Accept=false;queuedRouter.Speak("備援劇情",false);
 Check(queueFallback.Interrupts.Last()==false,"fallback preserves queued request");
 queuePrimary.Accept=true;int fallbackStops=queueFallback.Stops;
 var stillQueued=queuedRouter.Speak("備援劇情第二句",false);
 Check(stillQueued.Backend=="NVDA-direct"&&queueFallback.Stops==fallbackStops,"backend recovery does not cancel fallback dialogue queue");
 queuedRouter.Speak("F2引導",true);
 Check(queuePrimary.Interrupts.Last()&&queueFallback.Stops==fallbackStops+1,"explicit guidance clears fallback queue and restores primary backend");
}
void Field(object owner,string field,object value)=>owner.GetType().GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(owner,value);
void Callback(object owner,string field,string method){var f=owner.GetType().GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;f.SetValue(owner,Delegate.CreateDelegate(f.FieldType,typeof(NativeDoubles).GetMethod(method)!));}
using(var nvda=new NvdaSpeechDriver(Path.GetFullPath("work/speech-routing-tests/no-native-test.dll"))) {
 Field(nvda,"closed",false);Callback(nvda,"running",nameof(NativeDoubles.Running));Callback(nvda,"cancel",nameof(NativeDoubles.Cancel));Callback(nvda,"say",nameof(NativeDoubles.Say));
 NativeDoubles.Cancels=0;
 Check(nvda.Speak("queued",false).Accepted&&NativeDoubles.Cancels==0,"direct NVDA queue never calls cancelSpeech");
 Check(nvda.Speak("immediate",true).Accepted&&NativeDoubles.Cancels==1,"direct NVDA interaction calls cancelSpeech");
 Check(nvda.Stop()&&NativeDoubles.Cancels==2,"direct NVDA stop cancels queued speech");
}
using(var tolk=new TolkSpeechDriver(Path.GetFullPath("work/speech-routing-tests/no-native-test.dll"),false)) {
 Field(tolk,"closed",false);Field(tolk,"initialized",true);Callback(tolk,"loaded",nameof(NativeDoubles.Yes));Callback(tolk,"hasSpeech",nameof(NativeDoubles.Yes));Callback(tolk,"output",nameof(NativeDoubles.Output));Callback(tolk,"silence",nameof(NativeDoubles.Yes));
 Check(tolk.Speak("queued",false).Accepted&&!NativeDoubles.Interrupt,"Tolk_Output receives false for queued dialogue");
 Check(tolk.Speak("immediate",true).Accepted&&NativeDoubles.Interrupt,"Tolk_Output receives true for explicit interaction");
}
Console.WriteLine(checks+" routing checks passed; fake readers do not prove vendor compatibility.");
sealed class Fake(string name,bool accepted):ISpeechDriver{
 public bool Accept=accepted,Throws;public List<string> Spoken=new();public int Stops,Disposals;
 public List<bool> Interrupts=new();
 public SpeechDispatch Speak(string text,bool interrupt=true){Spoken.Add(text);Interrupts.Add(interrupt);if(Throws)throw new InvalidOperationException("simulated");return new(Accept,name,Accept?"accepted":"unavailable");}
 public bool Stop(){Stops++;return true;}
 public void Dispose()=>Disposals++;
}

public static class NativeDoubles {
 public static int Cancels;public static bool Interrupt;
 public static int Running()=>0;
 public static int Cancel(){Cancels++;return 0;}
 public static int Say(string text)=>0;
 public static bool Yes()=>true;
 public static bool Output(string text,bool interrupt){Interrupt=interrupt;return true;}
}

using PataponAccess;
int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
Check(Math.Abs(60000.0/146-410.958904)<0.001,"146 BPM one beat is 411ms");
Check(DanceNoteTiming.RemainingBeats(50,-150,-50)==2,"native left-moving geometry");
Check(DanceNoteTiming.RemainingBeats(-50,150,50)==2,"right-moving geometry is also valid");
Check(double.IsNaN(DanceNoteTiming.RemainingBeats(1,2,0)),"zero movement does not generate a cue");
var timing=new DanceNoteTiming();timing.Register(1);
Check(!timing.Poll(1,1.1),"no early cue");Check(timing.Poll(1,1),"one beat ahead");Check(!timing.Poll(1,0.99),"exactly once per note");
timing.Register(2);Check(!timing.Poll(2,-0.1),"late missed note is not replayed");
timing.Register(3);timing.Poll(3,2);Check(!timing.Poll(3,3),"seek invalidates old note");
timing.Register(1);Check(timing.Poll(1,1),"reused native key registers a new note");
foreach(int fps in new[]{30,60,120}){
 var simulated=new DanceNoteTiming();simulated.Register(1);int cues=0;double at=0;
 for(double t=0;t<2;t+=1.0/fps){double beats=(1.5-t)*146/60;if(simulated.Poll(1,beats)){cues++;at=t;}}
 Check(cues==1,"one cue across frame rates");Check(Math.Abs((1.5-at)-60.0/146)<=1.0/fps+0.00001,"one-beat lead within a frame");
}
foreach(uint tempo in new uint[]{100,146,200}){
 byte[] wav=StickCueWave.Build(tempo);Check(System.Text.Encoding.ASCII.GetString(wav,0,4)=="RIFF","valid wave header");Check(BitConverter.ToInt32(wav,24)==48000,"48k sample rate");
 int[] starts={0,(int)(48000*30.0/tempo),(int)(48000*60.0/tempo)};
 foreach(int start in starts){int peak=0;for(int i=start;i<Math.Min(start+1440,(wav.Length-44)/2);i++)peak=Math.Max(peak,Math.Abs((int)BitConverter.ToInt16(wav,44+i*2)));Check(peak>2000&&peak<=32768,"audible stick pulse within PCM range");}
}
var single=StickCueWave.Build(146,1);Check(single.Length==44+1440*2,"single note is a short 30ms cue");
Directory.CreateDirectory("outputs/dance-cues");File.WriteAllBytes("outputs/dance-cues/dance-note.wav",single);File.WriteAllBytes("outputs/dance-cues/dance-rapid.wav",StickCueWave.Build(rapid:true));
Console.WriteLine($"{checks} timing and audio checks passed. Native gameplay still needs a listening test.");

var baseWave=StickCueWave.Build(146,1,1);var boostedWave=StickCueWave.Build(146,1,3);
Check(baseWave.Length==boostedWave.Length,"threefold boost preserves cue duration");
for(int i=44;i<baseWave.Length;i+=2){int original=BitConverter.ToInt16(baseWave,i);int boosted=BitConverter.ToInt16(boostedWave,i);int expected=Math.Clamp(original*3,short.MinValue,short.MaxValue);Check(Math.Abs(boosted-expected)<=3,"threefold PCM boost clamps without overflow");}
Console.WriteLine($"{checks} timing/audio checks including threefold boost passed.");

var rapid=StickCueWave.Build(146,rapid:true);
Check(!rapid.SequenceEqual(StickCueWave.Build(146)),"rapid sound differs from ordinary note");
for(int pulse=1;pulse<3;pulse++){int expectedStart=(int)Math.Ceiling(pulse*48000*30.0/146);int first=Enumerable.Range(expectedStart,100).First(i=>Math.Abs((int)BitConverter.ToInt16(rapid,44+i*2))>100);Check(first-expectedStart<24,"rapid pulse starts at eighth-note spacing, within 0.5ms");}
Console.WriteLine($"{checks} checks passed, including rapid timbre and eighth-note spacing.");

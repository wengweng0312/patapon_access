using PataponAccess;
using System.Text.Json;
int count=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
foreach(var p in Directory.GetFiles("work/totem-investigation","missionid_*.pac")){
 var b=File.ReadAllBytes(p);var profiles=P2TotemProfiles.Read(b);
 Check(profiles.Count==1,Path.GetFileName(p)+" native profile");
 Check(profiles[0].Minimum==150&&profiles[0].Maximum==300&&profiles[0].StateVariable==0xc8&&profiles[0].BodyVariable==0xce,"native state variable and bounds");
 Check(P2TotemProfiles.Read(b[..128]).Count==0,"truncated buffer rejected");
 var changed=(byte[])b.Clone();
 // Change only the native distance limit; the parser must read it, not hardcode 300.
 for(int i=0;i<changed.Length-104;i+=4)if(BitConverter.ToUInt32(changed,i)==0x00130125&&BitConverter.ToUInt32(changed,i+28)==0x00a00425&&BitConverter.ToUInt32(changed,i+84)==0x001a0025&&BitConverter.ToSingle(changed,i+100)==300)BitConverter.GetBytes(320f).CopyTo(changed,i+100);
 Check(P2TotemProfiles.Read(changed).Single().Maximum==320,"modified native range read dynamically");
}
Check(P2TotemProfiles.Read(new byte[1000]).Count==0,"unrelated script rejected");
P2TotemCueState New()=>new();
void Sample(P2TotemCueState s,long time,float distance,int? stage=1,bool enabled=true){
 s.Observe("OEP_0_BODY",enabled,2160,-112,17,time);
 string[] drums={"PON","PON","CHAKA","PATA"};for(int i=0;i<4;i++)s.Observe($"OEP_{i+1}_{drums[i]}",true,2160,-112,99,time);
 s.ObservePlayer(2160-distance,2257-distance,time);
 if(stage.HasValue)s.ObserveNative(17,stage.Value,150,300,time);
}
var s=New();Sample(s,1000,600);Check(s.Take(1000)==TotemCue.None,"far glyphs silent");
Sample(s,1100,400);Check(s.Take(1100)==TotemCue.Ahead&&s.CurrentAt(1100)=="","ahead does not claim input readiness");
Check(s.Take(1200)==TotemCue.None,"ahead not repeated");
Sample(s,2000,300);Check(s.Take(2000)==TotemCue.Ready&&s.CurrentAt(2000)=="PON PON CHAKA PATA","ready at native inclusive far boundary");
Check(s.Take(2100)==TotemCue.None,"ready not repeated");
Sample(s,2200,200,3);Check(s.Take(2200)==TotemCue.None,"do not distract later pattern hits");
Sample(s,2300,200,7);Check(s.CurrentAt(2300)==""&&s.Take(2300)==TotemCue.None,"native waiting phase cannot claim readiness");
Sample(s,2400,200,2);Check(s.Take(2400)==TotemCue.None,"next round does not replay initial cue");
Sample(s,2500,150);Check(s.CurrentAt(2500)!="","inclusive near boundary follows native script");
Sample(s,2600,149);Check(s.CurrentAt(2600)==""&&s.Take(2600)==TotemCue.None,"too near is not ready before passing pillar");
Sample(s,2700,200,null);Check(s.CurrentAt(3401)=="","stale native state cannot enable readiness");
s=New();Sample(s,4000,250,null);Check(s.Take(4000)==TotemCue.None,"unknown native profile never fabricates readiness");
Check(!s.ObserveNative(18,1,150,300,4001),"another layout cannot supply state");
Sample(s,4500,250);Check(s.Take(4500)==TotemCue.Ahead,"late first encounter still introduces ahead once");
Sample(s,4800,250);Check(s.Take(4800)==TotemCue.None,"different cue waits for first audio tail");
Sample(s,5200,250);Check(s.Take(5200)==TotemCue.Ready,"ready after first audio finishes");
Sample(s,5300,250,1,false);Check(s.CurrentAt(5300)=="","disabled body not repeatable");
s.Clear();Check(!s.HasEncounter&&s.CurrentAt(5400)=="","mission reset removes all old state");
// Actual user report: old code warned at 229.88, then marching crossed 150.
// Replay preceding recorded positions: new ahead and ready must precede old warning.
s=New();using(var doc=JsonDocument.Parse(File.ReadAllText("work/totem-investigation/current-run-range-evidence.json"))){
 long ahead=0,ready=0;foreach(var e in doc.RootElement.GetProperty("events").EnumerateArray()){
  string kind=e.GetProperty("kind").GetString()!;var d=e.GetProperty("data");long t=e.GetProperty("elapsedMs").GetInt64();
  if(kind!="drum-puzzle-proximity")continue;float distance=d.GetProperty("flagDistance").GetSingle();
  // Native stage was absent in old logs: stage1 here is an explicit simulation,
  // not a claim that this historical run recorded stage1.
  Sample(s,t,distance);var cue=s.Take(t);if(cue==TotemCue.Ahead)ahead=t;if(cue==TotemCue.Ready)ready=t;
  if(distance<150)Check(s.CurrentAt(t)=="","actual run too-near frame blocked");
 }
 Check(ahead>0&&ahead<ready&&ready<1081442,"same position timeline announces ahead and readiness before old cue, with simulated native stage");
}
var wave=MechanismReadyWave.Build();Check(System.Text.Encoding.ASCII.GetString(wave,0,4)=="RIFF"&&BitConverter.ToInt32(wave,24)==48000,"new cue PCM format");
Check(wave.Length<48000,"new cue under half a second");
Console.WriteLine($"PASS {count} two-stage and original-script checks. Native-stage replay is simulated; new hooks require gameplay validation.");

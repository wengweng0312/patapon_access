using System.Reflection;
using PataponAccess;
int checks=0;
void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
object? Invoke(Type type,string name,params object?[] args)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
var batch=new MiningRewardBatch();batch.Add(1025,1,0);batch.Add(1025,3,10);batch.Add(1026,1,10);
Check(!batch.Ready(509),"do not read partially arriving reward");Check(batch.Ready(510),"batch settles");Check(batch.Items[1025]==4&&batch.Items[1026]==1,"real mining reward counts4stone1iron");batch.Clear();Check(!batch.Ready(1000),"prior play cleared");
MiningRewardAccess.Added(1025,99);Check(!((MiningRewardBatch)typeof(MiningRewardAccess).GetField("batch",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!).Items.Any(),"unrelated inventory grants excluded");
object?[] enter={false};Invoke(typeof(MiningRewardAccess),"Enter",enter);
MiningRewardAccess.Added(1025,1);MiningRewardAccess.Added(1025,3);MiningRewardAccess.Added(1026,1);Invoke(typeof(MiningRewardAccess),"Exit",enter);
var actual=(MiningRewardBatch)typeof(MiningRewardAccess).GetField("batch",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
typeof(MiningRewardBatch).GetField("changed",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(actual,Environment.TickCount64-600);
MiningRewardAccess.Scan();Check(Speech.Texts.Single()=="石塊，4。叮叮鐵，1","actual output from scoped native-grant flow; game names only");MiningRewardAccess.Scan();Check(Speech.Texts.Count==1,"no repeat reward output");
var analyzer=new P2.Sound.SubGame.Script.Analyzer{Pointer=(IntPtr)42,userId_=0,cursor=new(){curCount=0,count=0,commandList=new(){command=new[]{new P2.Sound.SubGame.Script.Command{hit=new short[]{17,0,0,0}}}}}};
var command=new P2.Sound.SubGame.Bean.Command{key_=analyzer};object?[] scope={command,new P2.System.Sound.BeatTimer(),null};Invoke(typeof(FarmingInputLogger),"Enter",scope);
object?[] hit={analyzer,null};Invoke(typeof(FarmingInputLogger),"BeforeHit",hit);Check(hit[1]!=null,"native inlined input path captured within Bean scope");Invoke(typeof(FarmingInputLogger),"AfterHit",1,P2.Sound.SubGame.Script.Analyzer.HitResult.OK,hit[1]);Check(PataponAccess.Monitor.Events.Any(x=>x.kind=="farming-input-result"),"real input result reaches logger");
object?[] other={new P2.Sound.SubGame.Script.Analyzer{Pointer=(IntPtr)43},null};Invoke(typeof(FarmingInputLogger),"BeforeHit",other);Check(other[1]==null,"shared analyzer body from other game excluded");
Invoke(typeof(FarmingInputLogger),"Exit",scope[2]);hit[1]=null;Invoke(typeof(FarmingInputLogger),"BeforeHit",hit);Check(hit[1]==null,"scope restored after native call");
Console.WriteLine($"{checks} reward/output and native input scope checks passed with wrappers; native gameplay remains untested.");

var danceCommand=new P2.Sound.SubGame.Bell.Command{key_=analyzer};object?[] danceScope={danceCommand,new P2.System.Sound.BeatTimer(),null};Invoke(typeof(FarmingInputLogger),"EnterDance",danceScope);
hit[1]=null;Invoke(typeof(FarmingInputLogger),"BeforeHit",hit);Invoke(typeof(FarmingInputLogger),"AfterHit",1,P2.Sound.SubGame.Script.Analyzer.HitResult.OK,hit[1]);Check(PataponAccess.Monitor.Events.Any(x=>x.kind=="dance-input-result"),"dance native input uses separate game identity");Invoke(typeof(FarmingInputLogger),"Exit",danceScope[2]);
MinigameCueTrace.Register("dance",5,0);MinigameCueTrace.Observe("dance",5,2,146,()=>null);
var pending=MinigameCueTrace.Begin("dance",5,1,146,null);CueAudio.LastApiCallMs=MinigameClock.NowMs;CueAudio.LastApiReturnMs=MinigameClock.NowMs;MinigameCueTrace.End(pending,true);
var timingJson=System.Text.Json.JsonSerializer.Serialize(PataponAccess.Monitor.Events.Last().data);using var timing=System.Text.Json.JsonDocument.Parse(timingJson);
Check(timing.RootElement.GetProperty("actual_play_time").ValueKind==System.Text.Json.JsonValueKind.Null,"never claim API timestamp is audible onset");
Check(timing.RootElement.GetProperty("api_call_time").ValueKind==System.Text.Json.JsonValueKind.Number,"API call has its own timestamp");
Check(timing.RootElement.GetProperty("target_source").GetString()=="visual-prediction-not-native-input-judgement","target estimate is honestly labelled");
Check(Math.Abs(timing.RootElement.GetProperty("target_time").GetDouble()-timing.RootElement.GetProperty("scheduled_time").GetDouble()-60000.0/146)<0.001,"one-beat target/deadline use common millisecond units");
MinigameCueTrace.Observe("dance",5,-0.05,146,()=>null);Check(PataponAccess.Monitor.Events.Last().kind=="minigame-note-crossing","observed line crossing logged separately from prediction");
MinigameCueTrace.Register("farming",5,0);MinigameCueTrace.Clear("farming");Check(MinigameCueTrace.Begin("dance",5,1,146,null)!=null,"separate game trace ownership");
var invalidClock=MinigameClock.Snapshot(new P2.System.Sound.BeatTimer{userTempo_=float.NaN,rateToBeatLine_=float.PositiveInfinity});Check(!System.Text.Json.JsonSerializer.Serialize(invalidClock).Contains("NaN"),"uninitialized native phases cannot disable JSON logger");
var nativeSource=new CriSoundManager.SoundSource{Pointer=(IntPtr)88,criAtomSource=new object(),Position=123};
var player=new P2.System.Sound.BgmPlayer{soundSources_=new(){nativeSource,new(){returned=true,criAtomSource=new object()}}};
var audioClock=System.Text.Json.JsonSerializer.Serialize(MinigameClock.Snapshot(new(){metronome_=player}));
using(var audio=System.Text.Json.JsonDocument.Parse(audioClock)) {
    var sources=audio.RootElement.GetProperty("native_audio").GetProperty("sources");
    Check(sources.GetArrayLength()==1,"returned native sources excluded");
    Check(sources[0].GetProperty("position_ms").GetInt64()==123,"native playback position retained without invented latency");
    Check(sources[0].GetProperty("position_source").GetString()=="CRI-source-GetTime-not-audible-onset","native position is not claimed to be audible onset");
}
nativeSource.Fail=true;Check(System.Text.Json.JsonSerializer.Serialize(MinigameClock.Snapshot(new(){metronome_=player})).Contains("unavailable"),"audio observation failure is isolated from clock diagnostics");
nativeSource.Fail=false;player.soundSources_=Enumerable.Range(0,20).Select(_=>nativeSource).ToList();nativeSource.Reads=0;MinigameClock.Snapshot(new(){metronome_=player});Check(nativeSource.Reads==8,"source sampling bounded");

player.soundSources_=new();CriSoundManager.poolManager=new(){PoolSoundSource=new(){new(){fileType=CriSoundManager.SoundGameFileType.P1,category=CriSoundManager.MixingCategory.MixingCategory_MiniGame,criAtomSource=new object()},new(){fileType=CriSoundManager.SoundGameFileType.P2,category=CriSoundManager.MixingCategory.MixingCategory_SE,criAtomSource=new object()},new(){fileType=CriSoundManager.SoundGameFileType.P2,category=CriSoundManager.MixingCategory.MixingCategory_MiniGame,criAtomSource=new object(),Position=456}}};
using(var pool=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(MinigameClock.Snapshot(new(){metronome_=player})))) {
 var a=pool.RootElement.GetProperty("native_audio");Check(a.GetProperty("route").GetString()=="P2-mini-game-CRI-pool","real empty-metronome regression falls back to native mini-game pool");
 Check(a.GetProperty("sources").GetArrayLength()==1&&a.GetProperty("sources")[0].GetProperty("position_ms").GetInt64()==456,"exclude P1 voices and unrelated P2 effects");
}

MiniGameRewardAccess.Added(1036,99);
var miniBatch=(MiningRewardBatch)typeof(MiniGameRewardAccess).GetField("batch",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
Check(!miniBatch.Items.Any(),"unrelated inventory is not a visible mini reward");
object?[] beanScope={typeof(P2.Bases.Camp.SoundGame.Bean.ItemManager).GetMethod("catchItem"),null};Invoke(typeof(MiniGameRewardAccess),"Enter",beanScope);
MiniGameRewardAccess.Added(1036,1);Invoke(typeof(MiniGameRewardAccess),"Exit",beanScope[1]);
typeof(MiningRewardBatch).GetField("changed",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(miniBatch,Environment.TickCount64-600);
MiniGameRewardAccess.Scan();Check(Speech.Texts.Last()=="大眼捲心菜，1","real reported Bean catch grant is spoken with original name and actual count");
int spokenCount=Speech.Texts.Count;MiniGameRewardAccess.Scan();MiniGameRewardAccess.Added(1036,9);Check(Speech.Texts.Count==spokenCount&&!miniBatch.Items.Any(),"read once and restore scoped grant filter");
Console.WriteLine($"{checks} total input/trace/reward checks; no gameplay or actual audio-onset claim.");
namespace HarmonyLib{public class Harmony{public void Patch(MethodInfo? method,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null,HarmonyMethod? finalizer=null){}}public class HarmonyMethod{public HarmonyMethod(MethodInfo? m){}}}
namespace PataponAccess{
static class Monitor{public static List<(string kind,object data)> Events=new();public static void Record(string kind,object data)=>Events.Add((kind,data));}
static class UnitNames{public static string? FetchLocalized(string game,uint id)=>id==1025?"石塊":id==1026?"叮叮鐵":id==1036?"大眼捲心菜":null;}
static class Speech{public static List<string> Texts=new();public static void Speak(string text,bool interrupt)=>Texts.Add(text);}}
namespace P2.Bases.Camp.SoundGame.Rock{public class ItemManager{public void generateItem(){}}}
namespace P2.Sound.SubGame{
public class Scorer{public uint okCount_,ngCount_,allCount_;}}
namespace P2.Sound.SubGame.Bean{public class Command{public IntPtr Pointer;public P2.Sound.SubGame.Script.Analyzer? key_;public P2.Sound.SubGame.Scorer? mainScorer_,subScorer_;public P2.System.Sound.BeatTimer? commandBeatTimer_;public P2.Sound.Director? director_;public bool isEnd_;public void update(){}}}
namespace P2.Sound.SubGame.Script{
public class Command{public short[]? hit;}
public class CommandList{public Command[]? command;}
public class Analyzer{public IntPtr Pointer;public uint userId_;public Cursor? cursor;public void hitCheck(){}public enum HitResult{OK=1,NG=2}public class Cursor{public CommandList? commandList;public uint count,curCount,groupCount,listCount;}}}


namespace P2.System.Sound{public class BeatTimer{public BgmPlayer? metronome_;public IntPtr Pointer;public uint tick_,halfBeatCount_,tempo_=146;public int pulseTick_,manualCompensationCount_;public float userTempo_=146,rateToBeatLine_,rateToHalfLine_;public bool isCompensationed_,isPause_;}public class BgmPlayer{public IntPtr Pointer;public int playId_,resourceId_;public bool isPause_;public List<CriSoundManager.SoundSource>? soundSources_;}}
public class CriSoundManager{public enum SoundGameFileType{P1=1,P2=2}public enum MixingCategory{MixingCategory_SE=2,MixingCategory_MiniGame=7}public class PoolManager{public List<SoundSource>? PoolSoundSource;}public static PoolManager? poolManager;public class SoundSource{public SoundGameFileType fileType;public MixingCategory category;public string acbFilePath="";public IntPtr Pointer;public string key="";public int prog,cueId,Reads;public uint globalCounter;public bool loop,returned,Fail;public long Position;public object? criAtomSource;public long getCriTime(){Reads++;if(Fail)throw new Exception("test source unavailable");return Position;}}}
namespace P2.Sound{public class Director{public P2.System.Sound.BeatTimer? beatTimer_;}}
namespace P2.Sound.SubGame.Bell{public class Command{public IntPtr Pointer;public P2.Sound.SubGame.Script.Analyzer? key_;public P2.Sound.SubGame.Scorer? mainScorer_,subScorer_;public P2.Sound.Director? director_;public void update(){}}}
namespace PataponAccess{static class CueAudio{public static double? LastApiCallMs,LastApiReturnMs;}}

namespace P2.Bases.Camp.SoundGame.Bean{public class ItemManager{public void catchItem(){}}}
namespace P2.Bases.Camp.SoundGame.Cook{public class ItemManager{public void endItemOut(){}}}
namespace P2.Bases.Camp.SoundGame.Trent{public class Game{}public class ItemManager{public void generateItem(Game game,bool last,float displace){}}}

using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;
namespace PataponAccess;
internal static class FarmingRestCue
{
    private static ConfigEntry<bool>? enabled;
    private static ConfigEntry<bool>? noteEnabled;
    private static readonly DanceNoteTiming noteTiming=new();
    private static readonly HashSet<long> noteKeys=new();
    private static readonly FarmingRestTiming timing=new();
    private static readonly FarmingBeatScale scale=new();
    private static readonly HashSet<long> registered=new();
    private static IntPtr owner;
    private static long seen,nextError;
    private static string phaseSignature="";
    public static string NoteFile {get;private set;}="";
    private static long farmingSeen;
    public static bool Active=>Environment.TickCount64-farmingSeen<500;
    public static string File {get;private set;}="";
    public static string TwoFile {get;private set;}="";
    public static string ThreeFile {get;private set;}="";
    private static uint waveTempo=147;
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    public static void Initialize(ConfigFile config)
    {
        enabled=config.Bind("Audio","P2FarmingRestCue",true,"Experimental P2 farming stop cue for one/two/three empty beats bounded by native visible notes. Does not change inputs or game timing.");
        noteEnabled=config.Bind("Audio","P2FarmingNoteCue",true,"One stick tick one musical beat before each visible P2 farming note, including off-beat notes. Does not press keys.");
        try {
            string folder=Path.Combine(BepInEx.Paths.BepInExRootPath,"cache","PataponAccess-audio");Directory.CreateDirectory(folder);
            File=Path.Combine(folder,"vegetable-rest.wav");System.IO.File.WriteAllBytes(File,RestWave());CueAudio.Prepare(File);
            TwoFile=Path.Combine(folder,"vegetable-rest-two.wav");System.IO.File.WriteAllBytes(TwoFile,RestWave(2,waveTempo));CueAudio.Prepare(TwoFile);
            ThreeFile=Path.Combine(folder,"vegetable-rest-three.wav");System.IO.File.WriteAllBytes(ThreeFile,RestWave(3,waveTempo));CueAudio.Prepare(ThreeFile);
            NoteFile=Path.Combine(folder,"vegetable-note.wav");System.IO.File.WriteAllBytes(NoteFile,MinigameCueWave.Align(StickCueWave.Build(147,1)));CueAudio.Prepare(NoteFile);
            var h=new Harmony("org.patapon.access.farming-rest");
            foreach(var hook in new[]{("atAcrossCallBack",nameof(Note)),("update",nameof(Update))})
                h.Patch(typeof(P2.Bases.Camp.SoundGame.Bean.Game).GetMethod(hook.Item1),postfix:new HarmonyMethod(typeof(FarmingRestCue).GetMethod(hook.Item2,BindingFlags.NonPublic|BindingFlags.Static)));
            FarmingInputLogger.Install(h);
        }catch(Exception e){Error(e);}
    }
    private static void Reset(IntPtr pointer){owner=pointer;timing.Clear();registered.Clear();scale.Clear();noteKeys.Clear();noteTiming.Clear();MinigameCueTrace.Clear("farming");}
    private static void Note(P2.Bases.Camp.SoundGame.Bean.Game __instance,P2.Sound.SubGame.Script.Command __0,P2.Bases.Camp.SoundGame.MusicScore __1,uint __2)
    {
        try {
            if(__instance.gameIndex_!=P2.Bases.Camp.SoundGame.Bean.Game.GameIndex.GameIndex_Shineup)return;
            if(owner!=__instance.Pointer)Reset(__instance.Pointer);
            // Record the full chart for each play, independently of sound settings.
            // Monitor still enforces its global rate/size/retention limits.
            {
                var visible=__1.usingKeyList_;
                var snapshot=new List<object>();
                if(visible!=null)for(int i=Math.Max(0,visible.Count-8);i<visible.Count;i++){
                    var k=visible[i];if(k!=null)snapshot.Add(new{beat=k.beatNumber_,x=k.position_.x,hit=k.hitFlag_,state=(int)k.state_});
                }
                Monitor.Record("farming-score-command",new{beat=__2,tag=__0.tag?.type,hit=__0.hit==null?Array.Empty<short>():__0.hit.Take(8).ToArray(),duration=__0.beat,act1=__0.act1,act2=__0.act2,se1=__0.se1,se2=__0.se2,phase=(int)__instance.state_,halfLength=__1.getHalfBeatLength(),notes=snapshot});
            }
            if(__0.hit is not {Length:>0} hits||hits[0]!=0x11)return;
            var keys=__1.usingKeyList_;if(keys==null||keys.Count==0)return;
            var added=keys[keys.Count-1];if(added!=null&&added.beatNumber_==__2){long noteId=added.Pointer.ToInt64();noteKeys.Add(noteId);noteTiming.Register(noteId);MinigameCueTrace.Register("farming",noteId,__2);}
            if(keys.Count<2)return;
            var next=keys[keys.Count-1];var previous=keys[keys.Count-2];
            if(next==null||previous==null||next.beatNumber_!=__2)return;
            if(previous.hitFlag_){Monitor.Record("farming-rest-skipped",new{beat=__2,reason="previous-note-already-hit"});return;}
            uint tempo=__1.userBeatTimer_?.tempo_??0;
            double length=Math.Abs(scale.BeatLength(tempo));if(!double.IsFinite(length)||length<0.001)return;
            double spacing=Math.Abs(next.position_.x-previous.position_.x)/length;
            int rests=FarmingRestTiming.RestCount(spacing);if(rests==0){Monitor.Record("farming-rest-skipped",new{beat=__2,spacing,reason="outside-supported-spacing"});return;}
            long id=next.Pointer.ToInt64();timing.Register(id,spacing);registered.Add(id);
            Monitor.Record("farming-rest-registered",new{beat=__2,rests,spacing,tempo,beatLength=length,source="visible-note-motion-calibrated-to-song-tempo",experimental=true});
        }catch(Exception e){Error(e);}
    }
    private static void Update(P2.Bases.Camp.SoundGame.Bean.Game __instance)
    {
        try {
            string phase=$"{__instance.Pointer}:{(int)__instance.gameIndex_}:{(int)__instance.state_}:{__instance.successCount_}:{__instance.ngCount_}:{__instance.isFail_}";
            if(phase!=phaseSignature){phaseSignature=phase;Monitor.Record("farming-phase",new{game=(int)__instance.gameIndex_,phase=(int)__instance.state_,successes=__instance.successCount_,failures=__instance.ngCount_,failed=__instance.isFail_,ended=__instance.isEnd_});}
            if(__instance.gameIndex_==P2.Bases.Camp.SoundGame.Bean.Game.GameIndex.GameIndex_Shineup&&!__instance.isEnd_)farmingSeen=Environment.TickCount64;
            else farmingSeen=0;
            if(owner!=__instance.Pointer||Environment.TickCount64-seen>1500)Reset(__instance.Pointer);
            seen=Environment.TickCount64;
            if(__instance.gameIndex_!=P2.Bases.Camp.SoundGame.Bean.Game.GameIndex.GameIndex_Shineup||__instance.state_!=P2.Bases.Camp.SoundGame.Bean.Game.State.State_Play){Reset(__instance.Pointer);return;}
            var score=__instance.musicScore_;var timer=score?.userBeatTimer_;if(score==null||timer==null)return;
            if(timer.isPause_||!score.moveFlag_){Reset(__instance.Pointer);return;}
            GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);
            if((enabled?.Value!=true&&noteEnabled?.Value!=true)||pid!=Environment.ProcessId){Reset(__instance.Pointer);return;}
            var keys=score.usingKeyList_;if(keys==null)return;
            if(keys.Count>0){var sample=keys[keys.Count-1];if(sample!=null)scale.Observe(sample.Pointer.ToInt64(),sample.position_.x,Environment.TickCount64);}
            double beatLength=scale.BeatLength(timer.tempo_);
            if(!double.IsFinite(beatLength)||Math.Abs(beatLength)<0.001)return;
            var live=new HashSet<long>();bool played=false;
            for(int i=0;i<Math.Min(keys.Count,128);i++){
                var key=keys[i];if(key==null)continue;long id=key.Pointer.ToInt64();live.Add(id);
                if(key.state_==P2.Bases.Camp.SoundGame.MusicScoreKey.State.State_Dead)continue;
                float target=P2.Bases.Camp.SoundGame.MusicScore.linePosition_g.x;
                double remaining=(target-key.position_.x)/beatLength;
                if(noteKeys.Contains(id))MinigameCueTrace.Observe("farming",id,remaining,timer.tempo_,()=>new{score=MinigameClock.Snapshot(timer),music=MinigameClock.Snapshot(__instance.campController_?.soundDirector_?.beatTimer_)});
                double frameAdvance=Math.Abs(key.moveVelocity_/beatLength)*0.5;
                if(noteEnabled?.Value==true&&noteKeys.Contains(id)&&noteTiming.Poll(id,remaining,frameAdvance)){
                    var trace=MinigameCueTrace.Begin("farming",id,remaining,timer.tempo_,new{score=MinigameClock.Snapshot(timer),music=MinigameClock.Snapshot(__instance.campController_?.soundDirector_?.beatTimer_)});
                    bool noteSuccess=CueAudio.Play(NoteFile);
                    MinigameCueTrace.End(trace,noteSuccess);
                    Monitor.Record("farming-note-cue",new{beat=key.beatNumber_,remainingBeats=remaining,tempo=timer.tempo_,beatLength,frameAdvance,waveformDelayMs=MinigameCueWave.DelayMs,success=noteSuccess,source="native-visible-note-one-musical-beat-ahead"});
                }
                if(enabled?.Value!=true||!registered.Contains(id))continue;
                if(!timing.Poll(id,remaining,out int rests)||played)continue;
                played=true;
                if(rests>=2&&waveTempo!=timer.tempo_){waveTempo=timer.tempo_;System.IO.File.WriteAllBytes(TwoFile,RestWave(2,waveTempo));CueAudio.Prepare(TwoFile);System.IO.File.WriteAllBytes(ThreeFile,RestWave(3,waveTempo));CueAudio.Prepare(ThreeFile);}
                bool success=CueAudio.Play(rests switch{2=>TwoFile,3=>ThreeFile,_=>File});
                Monitor.Record("farming-rest-cue",new{beat=key.beatNumber_,rests,pulses=rests,nextNoteBeats=remaining,tempo=timer.tempo_,beatLength,targetX=target,endX=score.endPositionX_,leadBeats=1,waveformDelayMs=MinigameCueWave.DelayMs,success,source="calibrated-visual-note-gap-judgement-line",experimental=true});
            }
            foreach(long id in registered.Where(id=>!live.Contains(id)).ToArray()){registered.Remove(id);timing.Remove(id);}
            foreach(long id in noteKeys.Where(id=>!live.Contains(id)).ToArray()){noteKeys.Remove(id);noteTiming.Remove(id);MinigameCueTrace.Remove("farming",id);}
        }catch(Exception e){Error(e);}
    }
    private static byte[] RestWave(int pulses=1,uint tempo=147)
    {
        const int rate=48000;
        double spacing=60.0/Math.Clamp(tempo,40u,300u);
        int count=(int)Math.Ceiling(((pulses-1)*spacing+0.12)*rate);
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
        for(int i=0;i<count;i++){double absolute=(double)i/rate;double t=absolute-Math.Min(pulses-1,(int)(absolute/spacing))*spacing;double phase=2*Math.PI*(1400*t-2500*t*t);double envelope=t<0.12?Math.Min(1,t/0.004)*Math.Exp(-t*32):0;w.Write((short)(Math.Sin(phase)*envelope*22000));}
        return MinigameCueWave.Align(stream.ToArray());
    }
    public static string Guide()
    {
        string key=I18n.T("dance.unknownKey","遊戲指定的操作鍵");
        try {
            var map=P2.System.Pad.Pad.ingameKeyDic;
            if(map!=null&&Enum.TryParse<MultiPlatformInputManager.AllAction>(map[0x2000].ToString(),out var action)){
                var bindings=BindingNames.Read(action);if(bindings.Count>0)key=string.Join("、",bindings.Distinct());
            }
        }catch(Exception e){Error(e);}
        return I18n.T("farming.guide","二代蔬菜耕種。操作鍵：{0}。每顆畫面音符會提前一拍播放短敲棒音，聽到後等一拍按一下。後半段不一定每拍都按，請跟隨每聲提示。下滑音是停手預告，一聲表示停一拍，兩聲停兩拍，三聲停三拍；第一聲之後的下一拍開始停手。下一顆敲棒音仍是提前一拍提示，等一拍再恢復按鍵。接菜階段沿用原本接菜提示。F1 可試聽與調音量。這些提示不會暫停或代替操作。",key);
    }
    private static void Error(Exception e){if(Environment.TickCount64<nextError)return;nextError=Environment.TickCount64+10000;Monitor.Record("farming-rest-error",new{error=e.Message});Reader.Log.LogWarning("Farming rest cue: "+e.Message);}
}

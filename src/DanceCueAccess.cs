using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class DanceCueAccess
{
    private static ConfigEntry<bool>? enabled;
    private static IntPtr owner;
    private static long nextError,seen;
    private static bool announced;
    private static uint waveTempo;
    private static readonly DanceNoteTiming notes=new();
    private static readonly HashSet<long> shortKeys=new();
    public static string File {get;private set;}="";
    public static string NoteFile {get;private set;}="";
    public static bool Active=>Environment.TickCount64-seen<500;
    public static void Initialize(ConfigFile config)
    {
        enabled=config.Bind("Audio","P2DanceCues",true,"Read-only P2 dance cues: one stick tick one beat before each ordinary note; three eighth-note ticks at rapid-tapping start. Does not change gameplay.");
        try {
            string folder=Path.Combine(BepInEx.Paths.BepInExRootPath,"cache","PataponAccess-audio");Directory.CreateDirectory(folder);
            File=Path.Combine(folder,"dance-rapid.wav");
            NoteFile=Path.Combine(folder,"dance-note.wav");System.IO.File.WriteAllBytes(NoteFile,MinigameCueWave.Align(StickCueWave.Build(146,1)));CueAudio.Prepare(NoteFile);Prepare(146);
            var hooks=new Harmony("org.patapon.access.dance-cues");
            void Hook(string method,string callback)=>hooks.Patch(typeof(P2.Bases.Camp.SoundGame.Bell.Game).GetMethod(method),postfix:new HarmonyMethod(typeof(DanceCueAccess).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
            // Both routines have substantial native bodies. No tiny setters,
            // by-ref/struct arguments, timers or input functions are patched.
            Hook("atAcrossCallBack",nameof(Short));Hook("startAxel",nameof(Rapid));Hook("update",nameof(Update));
            FarmingInputLogger.InstallDance(hooks);
        }catch(Exception e){Error(e);}
    }
    private static uint Tempo(P2.Bases.Camp.SoundGame.Bell.Game game)
    {uint tempo=game.campController_?.soundDirector_?.beatTimer_?.tempo_??0;return tempo is >=40 and <=300?tempo:146;}
    private static void Prepare(uint tempo)
    {
        if(waveTempo==tempo)return;
        System.IO.File.WriteAllBytes(File,MinigameCueWave.Align(StickCueWave.Build(tempo,rapid:true)));waveTempo=tempo;CueAudio.Prepare(File);
    }
    private static void Short(P2.Bases.Camp.SoundGame.Bell.Game __instance,P2.Sound.SubGame.Script.Command __0,P2.Bases.Camp.SoundGame.MusicScore __1,uint __2)
    {
        if(enabled?.Value!=true)return;
        try {
            if(__0.hit is not { Length: >0 } hits||hits[0]!=0x11)return;
            // Original callback has just appended the native short-note model.
            var keys=__1.usingKeyList_;if(keys==null||keys.Count==0)return;
            var key=keys[keys.Count-1];if(key==null||key.beatNumber_!=__2)return;
            long id=key.Pointer.ToInt64();shortKeys.Add(id);notes.Register(id);
            MinigameCueTrace.Register("dance",id,__2);
            Monitor.Record("dance-note-registered",new{beatNumber=__2,lookaheadHalfBeats=__1.moveHalfBeatTime_,source="native-atAcrossCallBack-hit-0x11"});
        }catch(Exception e){Error(e);}
    }
    private static void Notes(P2.Bases.Camp.SoundGame.Bell.Game game)
    {
        if(enabled?.Value!=true)return;
        var score=game.musicScore_;var timer=score?.userBeatTimer_??game.campController_?.soundDirector_?.beatTimer_;
        if(score==null||timer==null||timer.isPause_||!score.moveFlag_)return;
        if(game.state_ is not (P2.Bases.Camp.SoundGame.Bell.Game.State.State_PlayDance or P2.Bases.Camp.SoundGame.Bell.Game.State.State_PrePlayAxel))return;
        var keys=score.usingKeyList_;if(keys==null)return;
        float halfLength=score.getHalfBeatLength();var live=new HashSet<long>();bool played=false;
        for(int i=0;i<Math.Min(keys.Count,128);i++) {
            var key=keys[i];if(key==null)continue;long id=key.Pointer.ToInt64();live.Add(id);
            if(!shortKeys.Contains(id)||key.state_==P2.Bases.Camp.SoundGame.MusicScoreKey.State.State_Dead)continue;
            float target=P2.Bases.Camp.SoundGame.MusicScore.linePosition_g.x;
            double remaining=DanceNoteTiming.RemainingBeats(key.position_.x,target,halfLength);
            MinigameCueTrace.Observe("dance",id,remaining,timer.tempo_,()=>new{score=MinigameClock.Snapshot(timer),music=MinigameClock.Snapshot(game.campController_?.soundDirector_?.beatTimer_)});
            if(key.hitFlag_)continue;
            // Choose the nearest rendering frame instead of always waiting until
            // the note has crossed the one-beat threshold (recorded ~13ms late).
            double frameAdvance=Math.Abs(halfLength)>0.001?Math.Abs(key.moveVelocity_/(4*halfLength)):0;
            if(!notes.Poll(id,remaining,frameAdvance)||played)continue;
            var trace=MinigameCueTrace.Begin("dance",id,remaining,timer.tempo_,new{score=MinigameClock.Snapshot(timer),music=MinigameClock.Snapshot(game.campController_?.soundDirector_?.beatTimer_)});
            played=true;bool success=CueAudio.Play(NoteFile);
            MinigameCueTrace.End(trace,success);
            Monitor.Record("dance-note-cue",new{beatNumber=key.beatNumber_,remainingBeats=remaining,tempo=timer.tempo_,frameAdvance,waveformDelayMs=MinigameCueWave.DelayMs,targetX=target,endX=score.endPositionX_,success,source="native-visual-note-nearest-frame",experimental=true});
        }
        foreach(long id in shortKeys.Where(id=>!live.Contains(id)).ToArray()){shortKeys.Remove(id);notes.Remove(id);MinigameCueTrace.Remove("dance",id);}
    }
    private static void Rapid(P2.Bases.Camp.SoundGame.Bell.Game __instance)
    {
        if(enabled?.Value!=true)return;
        try {
            if(__instance.state_!=P2.Bases.Camp.SoundGame.Bell.Game.State.State_PlayAxel)return;
            uint tempo=Tempo(__instance);Prepare(tempo);bool success=CueAudio.Play(File);
            Monitor.Record("dance-rapid-cue",new{tempo,spacingMs=30000.0/tempo,waveformDelayMs=MinigameCueWave.DelayMs,success,source="native-Bell.Game.startAxel",meaning="rapid tapping begins; three cue sounds are not three required button presses"});
        }catch(Exception e){Error(e);}
    }
    private static void Update(P2.Bases.Camp.SoundGame.Bell.Game __instance)
    {
        try {
            var phase=__instance.state_;
            if(phase is P2.Bases.Camp.SoundGame.Bell.Game.State.State_End or P2.Bases.Camp.SoundGame.Bell.Game.State.State_Event_End){announced=false;seen=0;shortKeys.Clear();notes.Clear();MinigameCueTrace.Clear("dance");return;}
            if(owner!=__instance.Pointer||Environment.TickCount64-seen>1500){owner=__instance.Pointer;announced=false;}
            seen=Environment.TickCount64;
            if(enabled?.Value==true)Prepare(Tempo(__instance));
            Notes(__instance);
            if(!announced){announced=true;Prepare(Tempo(__instance));Speech.Speak(I18n.T("dance.enter","舞蹈小遊戲。按 F2 查看玩法。"),interrupt:false);Monitor.Record("dance-help-offered",new{phase=phase.ToString()});}
        }catch(Exception e){Error(e);}
    }
    public static string Guide()
    {
        string key=I18n.T("dance.unknownKey","遊戲指定的操作鍵");
        try {
            var map=P2.System.Pad.Pad.ingameKeyDic;
            if(map!=null&&Enum.TryParse<MultiPlatformInputManager.AllAction>(map[0x2000].ToString(),out var action)) {
                var bindings=BindingNames.Read(action);if(bindings.Count>0)key=string.Join("、",bindings.Distinct());
            }
        }catch(Exception e){Error(e);}
        return I18n.T("dance.guide","舞蹈小遊戲。操作鍵：{0}。一般音符會提前一拍播放一聲短敲棒音，聽到後等一拍再按一下；146 BPM 時一拍約 411 毫秒。不要聽到就立刻按，也不是等待老師示範後再模仿。聽到三聲短敲棒音，表示連按段開始，請立即盡可能快速反覆按同一個鍵，不是只按三下，也不用跟著三聲的速度按。三聲使用八分音符間距。教學詢問可依選項提示選是或否。F3 重讀對話或選項。幫助不會暫停遊戲。",key);
    }
    private static void Error(Exception e){if(Environment.TickCount64>=nextError){nextError=Environment.TickCount64+10000;Reader.Log.LogWarning("Dance cue: "+e.Message);Monitor.Record("dance-cue-error",new{error=e.Message});}}
}

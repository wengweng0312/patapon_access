using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class DrumPuzzleAccess
{
    private static ConfigEntry<bool>? keys;
    private static readonly P2TotemCueState state=new();
    private static string lastPuzzleSpeech="";
    public static void Configure(ConfigEntry<bool> value)=>keys=value;
    public static string Mode=>keys?.Value==true?I18n.T("ui.cd89e50cc353","綁定按鍵"):I18n.T("ui.278b93b27c5f","鼓聲名稱");
    public static string Toggle(){if(keys!=null)keys.Value=!keys.Value;return I18n.T("ui.b87d9c50189d","機關鼓點報讀：")+Mode;}
    private static long nextPlayerSample,lastError,nextProximityLog;
    public static void Install()
    {
        try{string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"mechanism-ready.wav");if(!File.Exists(file))File.WriteAllBytes(file,MechanismReadyWave.Build());CueAudio.Prepare(file);}
        catch(Exception e){Reader.Log.LogWarning("Mechanism ready audio: "+e.Message);}
        P2TotemNative.Install();
        try{new Harmony("org.patapon.access.puzzle-position").Patch(typeof(P2.Game.Unit.FlagUnit).GetMethod("update"),postfix:new HarmonyMethod(typeof(DrumPuzzleAccess).GetMethod(nameof(Player),BindingFlags.NonPublic|BindingFlags.Static)));}
        catch(Exception e){Reader.Log.LogWarning("Puzzle position hook: "+e.Message);}
    }
    private static void Player(P2.Game.Unit.FlagUnit __instance)
    {
        if(!BattleMonitor.Active)return;
        long now=Environment.TickCount64;if(now<nextPlayerSample)return;nextPlayerSample=now+100;
        try{
            if(__instance.pUnitTroop_?.troopInfo_?.troopType!=P2.Game.Unit.TroopType.TroopType_Player)return;
            var model=__instance.flagUnitModel_;
            // Same native cache used by CommandGame.getTroopTopPos; read only,
            // without invoking the recalculation or any script/input command.
            var top=__instance.pUnitTroop_?.pUnitBaseTopCache_;
            if(model==null||top==null||!top.isActive_||top.isDead_||top.unitModel_==null)return;
            if(top.pUnitSquad_?.pUnitTroop_?.troopInfo_?.troopType!=P2.Game.Unit.TroopType.TroopType_Player)return;
            state.ObservePlayer(model.pos_.x,top.unitModel_.pos_.x,now);
        }catch(Exception e){if(now-lastError>=5000){lastError=now;Monitor.Record("drum-puzzle-position-error",new{error=e.Message});}}
    }
    public static void Clear(){state.Clear();P1TotemAccess.Clear();P2TotemNative.Clear();lastPuzzleSpeech="";nextPlayerSample=nextProximityLog=0;}

    public static void Observe(string name,bool enabled,float x,float y,uint layoutId)=>state.Observe(name,enabled,x,y,layoutId,Environment.TickCount64);
    public static bool ObserveNative(uint layoutId,int stage,float min,float max,long now)=>state.ObserveNative(layoutId,stage,min,max,now);
    private static string Format(string sequence)
    {
        if(keys?.Value!=true)return sequence;
        return string.Join(" ",sequence.Split(' ').Select(drum=>{
            var action=drum switch{"PATA"=>MultiPlatformInputManager.AllAction.InGame_Pata,"PON"=>MultiPlatformInputManager.AllAction.InGame_Pon,"CHAKA"=>MultiPlatformInputManager.AllAction.InGame_Chaka,_=>MultiPlatformInputManager.AllAction.InGame_Don};
            var names=BindingNames.Read(action);return names.Count>0?names[0].Replace(" 鍵",""):drum;
        }));
    }
    public static string RepeatText
    {
        get{if(!BattleMonitor.Active)return "";long now=Environment.TickCount64;
            string p1=P1TotemAccess.State.CurrentAt(now);if(p1.Length>0)return Format(p1);
            string current=state.CurrentAt(now);if(current.Length>0)return Format(current);
            return state.HasEncounter&&lastPuzzleSpeech.Length>0&&Speech.Last==lastPuzzleSpeech?I18n.T("puzzle.not-ready","目前無法確認機關可接受輸入。"):"";}
    }
    public static void Tick()
    {
        if(!BattleMonitor.Active)return;
        TickState(P1TotemAccess.State,"P1");TickP2();
    }
    private static void TickP2(){
        long now=Environment.TickCount64;var cue=state.Take(now);
        if(cue==TotemCue.None)return;
        bool ready=cue==TotemCue.Ready;
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,ready?"mechanism-ready.wav":"mechanism-near.wav");
        bool success=CueAudio.Play(file);
        string text=ready?Format(state.CurrentAt(now)):I18n.T("puzzle.ahead","前方有鼓點機關。");
        if(text.Length==0)return;
        lastPuzzleSpeech=text;
        Monitor.Record("drum-puzzle-two-stage",new{game="P2",stage=ready?"ready":"ahead",flagDistance=state.FlagDistance,success,text,experimental=true});
        Speech.Speak(text,interrupt:false);
    }
    private static void TickState(DrumPuzzleState current,string game)
    {
        var state=current;long now=Environment.TickCount64;
        if(state.Current.Length>0&&now>=nextProximityLog){nextProximityLog=now+1000;Monitor.Record("drum-puzzle-proximity",new{game,distance=state.Distance,flagDistance=state.FlagDistance,positionSource=game=="P1"?"native-getUnitTop":"native-troop-top-cache",dismissDistance=DrumPuzzleState.DismissDistance,near=state.CurrentAt(now).Length>0,threshold=DrumPuzzleState.ApproachDistance,experimental=true});}
        string sequence=state.Take(now);if(sequence.Length==0)return;
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"mechanism-near.wav");
        bool success=CueAudio.Play(file);
        Monitor.Record("drum-puzzle-near",new{game,distance=state.Distance,flagDistance=state.FlagDistance,positionSource=game=="P1"?"native-getUnitTop":"native-troop-top-cache",dismissDistance=DrumPuzzleState.DismissDistance,threshold=DrumPuzzleState.ApproachDistance,success,experimental=true});
        string text=Format(sequence);Monitor.Record("drum-puzzle-speech",new{game,sequence,text,mode=Mode});Speech.Speak(text,interrupt:false);
    }
}

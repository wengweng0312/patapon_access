using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class DrumPuzzleAccess
{
    private static ConfigEntry<bool>? keys;
    private static readonly DrumPuzzleState state=new();
    public static void Configure(ConfigEntry<bool> value)=>keys=value;
    public static string Mode=>keys?.Value==true?"綁定按鍵":"鼓聲名稱";
    public static string Toggle(){if(keys!=null)keys.Value=!keys.Value;return "機關鼓點報讀："+Mode;}
    private static long nextPlayerSample,lastError,nextProximityLog;
    public static void Install()
    {
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
    public static void Clear(){state.Clear();P1TotemAccess.Clear();nextPlayerSample=nextProximityLog=0;}

    public static void Observe(string name,bool enabled,float x,float y)=>state.Observe(name,enabled,x,y,Environment.TickCount64);
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
            string p1=P1TotemAccess.State.CurrentAt(now);return Format(p1.Length>0?p1:state.CurrentAt(now));}
    }
    public static void Tick()
    {
        if(!BattleMonitor.Active)return;
        TickState(P1TotemAccess.State,"P1");TickState(state,"P2");
    }
    private static void TickState(DrumPuzzleState current,string game)
    {
        var state=current;long now=Environment.TickCount64;
        if(state.Current.Length>0&&now>=nextProximityLog){nextProximityLog=now+1000;Monitor.Record("drum-puzzle-proximity",new{game,distance=state.Distance,flagDistance=state.FlagDistance,positionSource=game=="P1"?"native-getUnitTop":"native-troop-top-cache",dismissDistance=DrumPuzzleState.DismissDistance,near=state.CurrentAt(now).Length>0,threshold=DrumPuzzleState.ApproachDistance,experimental=true});}
        string sequence=state.Take(now);if(sequence.Length==0)return;
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"mechanism-near.wav");
        bool success=CueAudio.Play(file);
        Monitor.Record("drum-puzzle-near",new{game,distance=state.Distance,flagDistance=state.FlagDistance,positionSource=game=="P1"?"native-getUnitTop":"native-troop-top-cache",dismissDistance=DrumPuzzleState.DismissDistance,threshold=DrumPuzzleState.ApproachDistance,success,experimental=true});
        string text=Format(sequence);Monitor.Record("drum-puzzle-speech",new{game,sequence,text,mode=Mode});Speech.Speak(text);
    }
}

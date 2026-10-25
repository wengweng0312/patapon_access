using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class MiningRewardAccess
{
    [ThreadStatic] private static bool generating;
    private static readonly MiningRewardBatch batch=new();
    private static long lastAdded;
    public static bool HandlesCurrentGrant=>generating;
    public static void Install(Harmony h)
    {
        // Native generateItem registers the visible dropped item model and
        // tail-calls Item.Operator.addItem. Observe only that verified scope.
        HarmonyMethod Callback(string name)=>new(typeof(MiningRewardAccess).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static));
        try{h.Patch(typeof(P2.Bases.Camp.SoundGame.Rock.ItemManager).GetMethod("generateItem"),prefix:Callback(nameof(Enter)),finalizer:Callback(nameof(Exit)));}
        catch(Exception e){Monitor.Record("mining-reward-hook-error",new{error=e.Message});}
    }
    private static void Enter(out bool __state){__state=generating;generating=true;}
    private static void Exit(bool __state)=>generating=__state;
    public static void Added(uint nameId,int count)
    {
        if(!generating||count<=0)return;
        lastAdded=Environment.TickCount64;batch.Add(nameId,count,lastAdded);
        Monitor.Record("mining-visible-reward",new{nameId,count,source="native-Rock.ItemManager.generateItem-visible-model-and-inventory"});
    }
    public static void Scan()
    {
        long now=Environment.TickCount64;if(!batch.Ready(now))return;
        var names=new List<string>();
        foreach(var pair in batch.Items){
            string? name=UnitNames.FetchLocalized("P2",pair.Key);
            if(string.IsNullOrWhiteSpace(name)){
                if(now-lastAdded<5000)return;
                Monitor.Record("mining-reward-name-missing",new{nameId=pair.Key,count=pair.Value});continue;
            }
            // Accessible description of displayed item icons. Names follow the
            // game language; no invented result heading or victory wording.
            names.Add(name+"，"+pair.Value);
        }
        batch.Clear();if(names.Count==0)return;
        string text=string.Join("。",names);Speech.Speak(text,interrupt:false);
        Monitor.Record("mining-reward-speech",new{text,source="visible-drop-icons-confirmed-by-native-grant",queued=true});
    }
}

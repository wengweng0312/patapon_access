using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class MiniGameRewardAccess
{
    [ThreadStatic] private static string? source;
    private static readonly MiningRewardBatch batch=new();
    private static long lastAdded;
    public static void Install(Harmony h)
    {
        var targets=new[]{
            typeof(P2.Bases.Camp.SoundGame.Bean.ItemManager).GetMethod("catchItem"),
            typeof(P2.Bases.Camp.SoundGame.Cook.ItemManager).GetMethod("endItemOut"),
            typeof(P2.Bases.Camp.SoundGame.Trent.ItemManager).GetMethod("generateItem",new[]{typeof(P2.Bases.Camp.SoundGame.Trent.Game),typeof(bool),typeof(float)})};
        foreach(var method in targets)try {
            if(method==null)throw new MissingMethodException("Mini-game visible reward method");
            h.Patch(method,prefix:new HarmonyMethod(typeof(MiniGameRewardAccess).GetMethod(nameof(Enter),BindingFlags.Static|BindingFlags.NonPublic)),finalizer:new HarmonyMethod(typeof(MiniGameRewardAccess).GetMethod(nameof(Exit),BindingFlags.Static|BindingFlags.NonPublic)));
        }catch(Exception e){Monitor.Record("mini-reward-hook-error",new{method=method?.ToString(),error=e.Message});}
    }
    private static void Enter(MethodBase __originalMethod,out string? __state)
    {__state=source;source=__originalMethod.DeclaringType?.FullName+"."+__originalMethod.Name;}
    private static void Exit(string? __state)=>source=__state;
    public static void Added(uint nameId,int count)
    {
        if(source==null||count<=0)return;
        lastAdded=Environment.TickCount64;batch.Add(nameId,count,lastAdded);
        Monitor.Record("mini-visible-reward",new{nameId,count,source});
    }
    public static void Scan()
    {
        long now=Environment.TickCount64;if(!batch.Ready(now))return;
        var names=new List<string>();
        foreach(var pair in batch.Items){
            string? name=UnitNames.FetchLocalized("P2",pair.Key);
            if(string.IsNullOrWhiteSpace(name)){
                if(now-lastAdded<5000)return;
                Monitor.Record("mini-reward-name-missing",new{nameId=pair.Key,count=pair.Value});continue;
            }
            names.Add(name+"，"+pair.Value);
        }
        batch.Clear();if(names.Count==0)return;
        string text=string.Join("。",names);Speech.Speak(text,interrupt:false);
        Monitor.Record("mini-reward-speech",new{text,source="native-visible-item-confirmed-grant",queued=true});
    }
}

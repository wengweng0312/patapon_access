using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class P2TotemNative
{
    private static readonly Dictionary<IntPtr,List<P2TotemProfile>> profiles=new();
    private static readonly Dictionary<IntPtr,(int stage,long at)> snapshots=new();
    private static long lastError;
    public static void Clear(){profiles.Clear();snapshots.Clear();}
    public static void Install(){
        try{new Harmony("org.patapon.access.p2-totem-state").Patch(typeof(P2.Game.Talk.CommandGame).GetMethod("getFlagUnitPosX"),postfix:new HarmonyMethod(typeof(P2TotemNative).GetMethod(nameof(Observe),BindingFlags.NonPublic|BindingFlags.Static)));}
        catch(Exception e){Reader.Log.LogWarning("P2 totem state hook: "+e.Message);}
    }
    private static void Observe(P2.Pyd.Script.Talk.Controller __0){
        if(!BattleMonitor.Active)return;
        try{
            var contexts=__0.context_;int index=__0.contextIndex_;
            if(contexts==null||index<0||index>=contexts.Length)return;
            // The recognized profile explicitly uses kind 0x08, native manager
            // global integer storage. Kind 0x04 would be controller-local storage.
            var buffer=contexts[index]?.buffer;var vars=__0.manager_?.variable_?.integerArray_;
            if(buffer==null||buffer.Length<256||buffer.Length>2_000_000||vars==null)return;
            if(!profiles.TryGetValue(buffer.Pointer,out var found)){
                if(profiles.Count>=32)return;
                found=P2TotemProfiles.Read(buffer.ToArray());profiles[buffer.Pointer]=found;
                foreach(var p in found)Monitor.Record("p2-totem-native-profile",new{p.BodyVariable,p.StateVariable,p.Minimum,p.Maximum,source="native-script-structure"});
            }
            long now=Environment.TickCount64;
            foreach(var p in found){
                if(p.BodyVariable>=vars.Length||p.StateVariable>=vars.Length)continue;
                int body=vars[p.BodyVariable].i,stage=vars[p.StateVariable].i;
                if(body<0||!DrumPuzzleAccess.ObserveNative((uint)body,stage,p.Minimum,p.Maximum,now))continue;
                if(snapshots.TryGetValue(__0.Pointer,out var previous)&&previous.stage==stage&&now-previous.at<1000)continue;
                if(snapshots.Count>32)snapshots.Clear();snapshots[__0.Pointer]=(stage,now);
                Monitor.Record("p2-totem-native-stage",new{layoutId=body,stage,minimum=p.Minimum,maximum=p.Maximum,source="native-local-state-read-only",meaning="Stage and distance are prerequisites, not proof of accepted player rhythm."});
            }
        }catch(Exception e){long now=Environment.TickCount64;if(now-lastError>=5000){lastError=now;Monitor.Record("p2-totem-native-error",new{error=e.Message});}}
    }
}

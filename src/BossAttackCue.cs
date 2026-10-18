using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PataponAccess;

internal static class BossAttackCue
{
    private static bool enabled,failed;
    private static readonly Dictionary<IntPtr,(int motion,int kind,long time)> last=new();
    private static readonly Dictionary<IntPtr,long> cueTimes=new();
    private static readonly Dictionary<IntPtr,uint> scriptMotions=new();
    // Resource families present in the inspected P1 ActorUnitData. Not a guessed motion-ID table.
    private static readonly Regex Family=new(@"^unit2(?:0[1-9]|1[0-9]|2[0-2])_",RegexOptions.CultureInvariant);
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern bool PlaySound(string file,IntPtr module,uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    public static void Install(bool value)
    {
        enabled=value;
        try {
            var method=typeof(P1.Game.Unit.UnitModel).GetMethod("changeMotion")!;
            var harmony=new Harmony("org.patapon.access.boss-start");
            harmony.Patch(method,postfix:new HarmonyMethod(typeof(BossAttackCue).GetMethod(nameof(Changed),BindingFlags.NonPublic|BindingFlags.Static)));
            Patch(harmony,typeof(P1.GameSystem.Actor.Script.ScriptCommand_Actor),"resetAttackUniqueId",nameof(ActorAttack));
            Patch(harmony,typeof(P1.Game.Talk.CommandUnitBase),"resetAttackUniqueId",nameof(UnitAttack));
            Patch(harmony,typeof(P1.GameSystem.Actor.Script.ScriptCommand_Actor),"setMotion",nameof(ScriptMotion));
            Patch(harmony,typeof(P1.Game.Talk.CommandUnitBase),"setMotion",nameof(UnitMotion));
            Monitor.Record("boss-observer",new{game="P1",method=method.ToString(),audio=value,experimental=true,scope="unit201..unit222 shared Attack classification; script-only animations may bypass it"});
        }catch(Exception e){failed=true;Reader.Log.LogWarning("Boss cue unavailable: "+e.Message);}
    }
    private static void Patch(Harmony harmony,Type type,string method,string callback)
    {
        try{harmony.Patch(type.GetMethod(method),postfix:new HarmonyMethod(typeof(BossAttackCue).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));Monitor.Record("boss-script-hook",new{type=type.FullName,method});}
        catch(Exception e){Reader.Log.LogWarning("Boss script hook unavailable: "+type.FullName+"."+method+": "+e.Message);}
    }
    private static void ActorAttack(P1.GameSystem.Actor.Script.ScriptCommand_Actor __instance)=>Script(__instance.pActorObj_,true,"actor-script-reset");
    private static void UnitAttack(P1.Game.Talk.CommandUnitBase __instance)=>Script(__instance.pUnitBase_,true,"unit-script-reset");
    private static void ScriptMotion(P1.GameSystem.Actor.Script.ScriptCommand_Actor __instance)=>Script(__instance.pActorObj_,false,"actor-script-motion");
    private static void UnitMotion(P1.Game.Talk.CommandUnitBase __instance)=>Script(__instance.pUnitBase_,false,"unit-script-motion");
    private static void Script(P1.GameSystem.Actor.ActorObj? actor,bool attack,string source)
    {
        try {
            // These are current script owners, not native objects retained between frames.
            var param=actor?.info_?.pParam;
            if(param==null||!Family.IsMatch(param.name??""))return;
            var model=actor!.pActorModel_;if(model==null)return;
            uint motion=model.motionId_;var key=model.Pointer;
            if(!attack&&scriptMotions.TryGetValue(key,out uint old)&&old==motion)return;
            if(scriptMotions.Count>128)scriptMotions.Clear();scriptMotions[key]=motion;
            Monitor.Record(attack?"boss-attack-marker":"boss-script-motion",new{unit=param.name,motion,frame=model.motionFrame_,source,experimental=true});
            // Reset occurs at motion 8/frame18 in the verified report: too late for warning.
            // unit212: motion7 began 5s before motion8 and 5.6s before reset in that recording.
            if(!attack&&param.name=="unit212_01_01"&&motion==7)
                Emit(key,param.name,(int)motion,"observed-unit212-windup7");
            // 20261017-215707 mission2: unit216 motion7 -> 8 -> 9/frame23 reset,
            // 5383ms lead; repeated motion11 holds 4016ms before motion12 (candidate charge).
            if(!attack&&param.name=="unit216_01_01"&&(motion==7||motion==11))
                Emit(key,param.name,(int)motion,motion==7?"observed-unit216-windup7":"candidate-unit216-charge11");
            // 20261017-221933: 7->8->9/reset (6817ms), 13->14->15/reset (5334ms).
            if(!attack&&param.name=="unit221_01_01"&&(motion==7||motion==13))
                Emit(key,param.name,(int)motion,motion==7?"observed-unit221-windup7":"observed-unit221-windup13");
        }catch(Exception e){Monitor.Record("boss-script-error",new{source,error=e.Message});}
    }
    private static void Emit(IntPtr key,string unit,int motion,string source)
    {
        if(!enabled)return;
        long now=Environment.TickCount64;
        if(cueTimes.TryGetValue(key,out long old)&&now-old<400)return;
        if(cueTimes.Count>128)cueTimes.Clear();cueTimes[key]=now;
        GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"boss-attack-start.wav");
        bool played=pid==Environment.ProcessId&&File.Exists(file)&&CueAudio.Play(file);
        Monitor.Record("boss-attack-cue",new{game="P1",unit,motion,source,played,meaning="attack cycle marker, not hit prediction",experimental=true});
    }
    private static void Changed(P1.Game.Unit.UnitModel __instance,int __1,P1.Game.Unit.MotionBaseTypeE __2,bool __4,bool __result)
    {
        if(failed||!__result)return;
        try {
            var unit=__instance.pUnitBase_;
            if(unit==null||!unit.isActive())return;
            var param=unit.getCharaParam();
            if(param==null||param.troopType==0||!Family.IsMatch(param.name??""))return;
            long now=Environment.TickCount64;var key=__instance.Pointer;
            int kind=(int)__2;
            if(last.TryGetValue(key,out var old)&&old.motion==__1&&old.kind==kind&&(!__4||now-old.time<400))return;
            if(last.Count>128)last.Clear();
            last[key]=(__1,kind,now);
            bool attack=__2==P1.Game.Unit.MotionBaseTypeE.MotionBaseTypeE_Attack;
            Monitor.Record("boss-motion",new{game="P1",unit=param.name,model=param.modelFilename,motion=__1,kind=__2.ToString(),forced=__4,attack,experimental=true});
            if(!attack||!enabled)return;
            Emit(key,param.name??"",__1,"shared-attack-motion");
        }catch(Exception e){failed=true;Reader.Log.LogWarning("Boss observer stopped: "+e.Message);}
    }
}

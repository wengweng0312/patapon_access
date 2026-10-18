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
    private static readonly HashSet<IntPtr> commonCycles=new();
    private static readonly Dictionary<IntPtr,Queue<object>> history=new();
    private static readonly Dictionary<IntPtr,long> traceTimes=new();
    // Resource families present in the inspected P1 ActorUnitData. Not a guessed motion-ID table.
    private static readonly Regex Family=new(@"^unit2(?:0[1-9]|1[0-9]|2[0-2])_",RegexOptions.CultureInvariant);
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
            Monitor.Record("boss-observer",new{game="P1",method=method.ToString(),audio=value,experimental=true,scope="unit201..unit222: script attack action 10 / phase 0, checked phase advance; verified startup fallbacks; attack classification logged only"});
        }catch(Exception e){failed=true;Reader.Log.LogWarning("Boss cue unavailable: "+e.Message);}
    }
    private static void Patch(Harmony harmony,Type type,string method,string callback)
    {
        try{harmony.Patch(type.GetMethod(method),postfix:new HarmonyMethod(typeof(BossAttackCue).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));Monitor.Record("boss-script-hook",new{type=type.FullName,method});}
        catch(Exception e){Reader.Log.LogWarning("Boss script hook unavailable: "+type.FullName+"."+method+": "+e.Message);}
    }
    private static void ActorAttack(P1.GameSystem.Actor.Script.ScriptCommand_Actor __instance,P1.Pyd.Script.Talk.Controller __0)=>Script(__instance.pActorObj_,__0,true,"actor-script-reset");
    private static void UnitAttack(P1.Game.Talk.CommandUnitBase __instance,P1.Pyd.Script.Talk.Controller __0)=>Script(__instance.pUnitBase_,__0,true,"unit-script-reset");
    private static void ScriptMotion(P1.GameSystem.Actor.Script.ScriptCommand_Actor __instance,P1.Pyd.Script.Talk.Controller __0)=>Script(__instance.pActorObj_,__0,false,"actor-script-motion");
    private static void UnitMotion(P1.Game.Talk.CommandUnitBase __instance,P1.Pyd.Script.Talk.Controller __0)=>Script(__instance.pUnitBase_,__0,false,"unit-script-motion");
    private static void Script(P1.GameSystem.Actor.ActorObj? actor,P1.Pyd.Script.Talk.Controller controller,bool attack,string source)
    {
        try {
            // These are current script owners, not native objects retained between frames.
            var param=actor?.info_?.pParam;
            if(param==null||!Family.IsMatch(param.name??""))return;
            string unitName=param.name??"";
            var model=actor!.pActorModel_;if(model==null)return;
            uint motion=model.motionId_;var key=model.Pointer;
            var state=ReadState(controller);
            bool entry=!attack&&state.entry;
            if(state.action.HasValue&&state.action!=10)commonCycles.Remove(key);
            if(!attack&&!entry&&scriptMotions.TryGetValue(key,out uint old)&&old==motion)return;
            if(scriptMotions.Count>128)scriptMotions.Clear();scriptMotions[key]=motion;
            long now=Environment.TickCount64;
            var snapshot=new{unit=param.name,motion,frame=model.motionFrame_,source,action=state.action,phase=state.phase,packAddress=state.address,attackEntry=entry,time=now,experimental=true};
            Monitor.Record(attack?"boss-attack-marker":"boss-script-motion",snapshot);
            if(history.Count>128){history.Clear();traceTimes.Clear();commonCycles.Clear();}
            if(!history.TryGetValue(key,out var trail))history[key]=trail=new Queue<object>();
            if(attack) {
                if(!traceTimes.TryGetValue(key,out long previous)||now-previous>=1500){traceTimes[key]=now;Monitor.Record("boss-attack-trace",new{unit=param.name,motion,attackTime=now,preceding=trail.ToArray()});}
                return; // Never turn an attack-frame event into a warning.
            }
            trail.Enqueue(snapshot);while(trail.Count>16)trail.Dequeue();
            if(entry) {
                commonCycles.Add(key);
                Emit(key,unitName,(int)motion,"script-attack-entry-phase0");
            } else if(!commonCycles.Contains(key)&&BossWindupState.IsVerifiedFallback(unitName,(int)motion))
                Emit(key,unitName,(int)motion,"verified-startup-motion");
        }catch(Exception e){Monitor.Record("boss-script-error",new{source,error=e.Message});}
    }
    private static (float? action,float? phase,int? address,bool entry) ReadState(P1.Pyd.Script.Talk.Controller controller)
    {
        try {
            // Read fields from the current command owner only. Never invoke the argument
            // parser: doing so would advance the game's bytecode cursor a second time.
            var vars=controller.variable_?.integerArray_;
            var contexts=controller.context_;int index=controller.contextIndex_;
            if(vars==null||vars.Length<=0x21||contexts==null||index<0||index>=contexts.Length)return(null,null,null,false);
            float action=vars[0x20].i,phase=vars[0x21].i;
            if(!float.IsFinite(action)||!float.IsFinite(phase))return(null,null,null,false);
            var buffer=contexts[index]?.buffer;var pack=controller.pack_;
            if(buffer==null||pack==null)return(action,phase,null,false);
            int address=pack.adr;
            if(buffer.Length<8||address<0||address>buffer.Length)return(action,phase,address,false);
            if(action!=10||phase!=0)return(action,phase,address,false);
            var header=new byte[8];for(int i=0;i<header.Length;i++)header[i]=buffer[i];
            var continuation=new byte[Math.Min(128,buffer.Length-address)];
            for(int i=0;i<continuation.Length;i++)continuation[i]=buffer[address+i];
            return(action,phase,address,BossWindupState.IsEntry(action,phase,header,continuation));
        } catch {return(null,null,null,false);}
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
            // Attack classification can begin at the damaging motion itself. It is
            // evidence for tracing, not a safe warning source.
        }catch(Exception e){failed=true;Reader.Log.LogWarning("Boss observer stopped: "+e.Message);}
    }
}

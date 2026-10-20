using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;
namespace PataponAccess;
internal static class P2NavigationLogger
{
    private static readonly Dictionary<string,(string signature,long at,int keys)> previous=new();
    private static long lastError;
    private static bool installed;
    private static readonly int[] keyCodes={0x25,0x26,0x27,0x28,0x0d,0x1b};
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    public static void Install()
    {
        installed=true;
        var h=new Harmony("org.patapon.access.p2-navigation-log");
        Patch(h,typeof(P2.Bases.Camp.EvolutionMap.IconSelector),"selected",nameof(Grid));
        Patch(h,typeof(P2.Bases.Camp.EvolutionMap.Controller),"execSelect",nameof(EvolutionRoster));
        Patch(h,typeof(P2.Bases.Camp.Controller),"setSuccessUnitAdd",nameof(BirthResult));
    }
    private static void Patch(Harmony h,Type type,string method,string callback)
    {
        try {
            var m=type.GetMethod(method)??throw new MissingMethodException(method);
            if(m.GetParameters().Any(p=>p.ParameterType.IsByRef))throw new InvalidOperationException("By-ref navigation hook rejected");
            h.Patch(m,postfix:new HarmonyMethod(typeof(P2NavigationLogger).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("p2-navigation-hook",new{type=type.FullName,method});
        }catch(Exception e){Reader.Log.LogWarning("P2 navigation logger: "+e.Message);}
    }
    private static int Keys()
    {
        GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);if(pid!=Environment.ProcessId)return 0;
        int value=0;
        for(int i=0;i<keyCodes.Length;i++)if((GetAsyncKeyState(keyCodes[i])&0x8000)!=0)value|=1<<i;
        return value;
    }
    private static (bool write,bool changed,int keys,int pressed) Gate(string key,string signature)
    {
        long now=Environment.TickCount64;int keys=Keys();
        bool found=previous.TryGetValue(key,out var old);bool changed=!found||old.signature!=signature;
        int pressed=keys&~old.keys;
        bool write=changed||pressed!=0||now-old.at>=3000;
        if(previous.Count>128)previous.Clear();
        previous[key]=(signature,write?now:old.at,keys);
        return(write,changed,keys,pressed);
    }
    private static void Error(Exception e)
    {long now=Environment.TickCount64;if(now-lastError>=5000){lastError=now;Monitor.Record("p2-navigation-error",new{error=e.Message});}}
    // Called only for a NewUnit row already matched to the active command-window model.
    public static void LifeTree(P2.Bases.Camp.SelectItem.Behavior.NewUnit row)
    {
        if(!installed)return;
        try {
            int slot=unchecked((int)row.selectId_),count=row.unitCount_,max=row.maxUnitCount_;
            var camp=row.controller_;int? unique=camp?.lifeTreeSelectUnitUniqueId_;
            string signature=$"{row.help_}:{slot}:{count}:{max}:{unique}:{row.capId_}:{row.isEnableSelect_}";
            var gate=Gate("tree:"+row.Pointer,signature);if(!gate.write)return;
            Monitor.Record("p2-life-tree-slot",new{model=row.help_,name=UnitNames.Localized("P2",LocalizeIds.Unit(unchecked((uint)row.messageId_))),slotIndex=slot,reportedUnitCount=count,maximum=max,selectedUniqueId=unique,capId=row.capId_,enabled=row.isEnableSelect_,changed=gate.changed,keys=gate.keys,pressed=gate.pressed,keyMask="1=left,2=up,4=right,8=down,16=confirm,32=cancel",classification="raw fields; do not infer empty slot from index alone"});
        }catch(Exception e){Error(e);}
    }
    private static void Grid(P2.Bases.Camp.EvolutionMap.IconSelector __instance,P2.Bases.Camp.EvolutionMap.PhyleticIcon? __result)
    {
        try {
            int x=__instance.posX_,y=__instance.posY_;
            int? node=__result?.nodeIdx_,level=__result?.getLevel();
            var cls=__result?.classTypeParam_;var param=__result?.param_;
            string signature=$"{x}:{y}:{node}:{level}:{__result?.isUnlock_}";
            var gate=Gate("grid:"+__instance.Pointer,signature);if(!gate.write)return;
            var ids=__instance.menuSelectMatrix_?.id;var matrix=new List<int>();
            if(ids!=null)for(int i=0;i<Math.Min(ids.Count,512);i++)matrix.Add(ids[i]);
            Monitor.Record("p2-evolution-cursor",new{x,y,node,level,hasNode=__result!=null,unlocked=__result?.isUnlock_,classId=cls?.charaClassId,classMessageId=cls?.msgId,parameterId=param?.id,changed=gate.changed,keys=gate.keys,pressed=gate.pressed,keyMask="1=left,2=up,4=right,8=down,16=confirm,32=cancel",outcome=gate.changed?"cursor-state-changed":"cursor-state-unchanged",matrixIds=matrix.ToArray()});
        }catch(Exception e){Error(e);}
    }
    private static void EvolutionRoster(P2.Bases.Camp.EvolutionMap.Controller __instance)
    {
        try {
            var units=__instance.unitAddingParam_;if(units==null)return;
            var records=new List<object>();var signature=new List<string>();
            for(int i=0;i<Math.Min(units.Length,16);i++){
                var unit=units[i];if(unit==null){records.Add(new{index=i,present=false});signature.Add("null");continue;}
                records.Add(new{index=i,present=true,unit.uniqueId,unit.id,unit.level,unit.isLive,unit.cap,unit.evolutionClass,unit.evolutionClassLevel,validLevel=unit.isValidLevel(),evolutionLevels=unit.evolutionLevel?.Take(64).ToArray()});
                signature.Add($"{unit.uniqueId}:{unit.level}:{unit.isLive}:{unit.evolutionClass}:{unit.evolutionClassLevel}");
            }
            int? selected=__instance.campController_?.lifeTreeSelectUnitUniqueId_;
            var gate=Gate("roster:"+__instance.Pointer,selected+"|"+string.Join("|",signature));if(!gate.write)return;
            Monitor.Record("p2-life-tree-roster",new{selectedUniqueId=selected,records=records.ToArray(),changed=gate.changed});
        }catch(Exception e){Error(e);}
    }
    private static void BirthResult(int __0)=>Monitor.Record("p2-unit-birth-result",new{flag=__0});
}

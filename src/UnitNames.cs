using HarmonyLib;
using System.Reflection;

namespace PataponAccess;

internal static class UnitNames
{
    private static readonly Dictionary<string,string> localized=new();
    private static readonly Dictionary<string,object> managers=new();
    private static readonly Dictionary<string,uint> units=new();
    public static void Install(Harmony h)
    {
        foreach(string ns in new[]{"P1","P2","P1S","P2S"}) {
            var asm=typeof(LauncherMain).Assembly;
            var manager=asm.GetType(ns+".Localize.Manager");
            if(manager!=null) foreach(var m in manager.GetMethods().Where(m=>m.Name=="getStringFromIndex")) {
                try {h.Patch(m,postfix:new HarmonyMethod(typeof(UnitNames).GetMethod(nameof(Localize),BindingFlags.Static|BindingFlags.NonPublic)));}
                catch(Exception e) {Reader.Log.LogWarning("Unit name localization hook: "+e.Message);}
            }
            // Do not detour renderActive: its by-ref native Param causes an
            // IL2CPP marshalling access violation before managed callbacks run.
        }
    }
    private static void Localize(object __instance,MethodBase __originalMethod,object[] __args,string __result)
    {
        if(string.IsNullOrWhiteSpace(__result) || __args.Length==0) return;
        VariableTemplates.Observe(__result,Environment.TickCount64);
        string ns=__originalMethod.DeclaringType!.Namespace!.Split('.')[0];
        managers[ns]=__instance;
        uint id=__args.Length>=2?LocalizeIds.Pack(Convert.ToUInt32(__args[0]),Convert.ToUInt32(__args[1])):Convert.ToUInt32(__args[0]);
        localized[ns+":"+id]=Speech.Clean(__result);
    }
    public static string? FetchLocalized(string ns,uint id)
    {
        var cached=Localized(ns,id);if(cached!=null)return cached;
        if(!managers.TryGetValue(ns,out var manager))return null;
        try {
            var method=manager.GetType().GetMethod("getStringFromIndex",new[]{typeof(uint)});
            var value=method?.Invoke(manager,new object[]{id}) as string;
            return string.IsNullOrWhiteSpace(value)?null:Speech.Clean(value);
        }catch{return null;}
    }    private static void ModelName(object __instance,string __0)
    {
        try {
            string ns=__instance.GetType().Namespace!.Split('.')[0];
            var id=__instance.GetType().GetProperty("messageId_")?.GetValue(__instance);
            if(id==null || string.IsNullOrEmpty(__0)) return;
            string model=__0;
            string key=ns+":"+model;
            uint messageId=Convert.ToUInt32(id);
            if(units.TryGetValue(key,out var old) && old==messageId) return;
            units[key]=messageId;
            Monitor.Record("unit-model-message",new{ns,model,messageId,name=Localized(ns,messageId)});
        } catch(Exception e) {Reader.Log.LogWarning("Selected unit name: "+e.Message);}
    }
    public static string? Resolve(string ns,string model)
    {
        try {
            int? id=null;
            if(ns=="P1") {
                var list=P1.Bases.Camp.SelectItem.Behavior.EnagyValueDisp.noticeObjectList_;
                if(list!=null) for(int i=0;i<list.Count;i++) {
                    var u=list[i]?.TryCast<P1.Bases.Camp.SelectItem.Behavior.NewUnit>();
                    if(u!=null && u.help_==model) {id=u.messageId_;break;}
                }
            } else if(ns=="P2") {
                var list=P2.Bases.Camp.SelectItem.Behavior.EnagyValueDisp.noticeObjectList_;
                if(list!=null) for(int i=0;i<list.Count;i++) {
                    var u=list[i]?.TryCast<P2.Bases.Camp.SelectItem.Behavior.NewUnit>();
                    if(u!=null && u.help_==model) {P2CampNavigation.ObserveTreeModel(u);P2NavigationLogger.LifeTree(u);id=u.messageId_;break;}
                }
            }
            if(id==null) return null;
            var name=Localized(ns,LocalizeIds.Unit(unchecked((uint)id.Value)));
            if(name!=null && (!units.TryGetValue(ns+":"+model,out var old)||old!=Convert.ToUInt32(id))) {
                units[ns+":"+model]=Convert.ToUInt32(id);
                Monitor.Record("unit-name-resolved",new{ns,model,messageId=id,name});
            }
            return name;
        } catch(Exception e) {Reader.Log.LogWarning("Read selected unit: "+e.Message);return null;}
    }
    public static string? Localized(string ns,object? id)=>id!=null && Convert.ToInt64(id)>=0 && localized.TryGetValue(ns+":"+Convert.ToUInt32(id),out var text)?text:null;
    private static readonly HashSet<string> observed=new();
    public static string? FromCallback(string ns,object? callback,string model)
    {
        try {
            var target=callback?.GetType().GetProperty("Target")?.GetValue(callback) as Il2CppSystem.Object;
            int? id=null;
            if(ns=="P1") id=target?.TryCast<P1.Bases.Camp.SelectItem.Behavior.NewUnit>()?.messageId_;
            else if(ns=="P2") id=target?.TryCast<P2.Bases.Camp.SelectItem.Behavior.NewUnit>()?.messageId_;
            string? name=id==null?null:Localized(ns,LocalizeIds.Unit(unchecked((uint)id.Value)));
            if(observed.Add(ns+":"+model+":"+id+":"+name))
                Monitor.Record("unit-callback-data",new{ns,model,messageId=id,name,target=target?.GetIl2CppType().FullName,callback=callback?.GetType().FullName});
            return name ?? Resolve(ns,model);
        } catch(Exception e) {Reader.Log.LogWarning("Unit callback data: "+e.Message);return null;}
    }
}


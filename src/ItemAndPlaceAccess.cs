using HarmonyLib;
using System.Reflection;

namespace PataponAccess;

internal static class ItemAndPlaceAccess
{
    private static string lastItem="",lastPlace="";
    private static string lastEquipment="";
    private static long campSeen;
    public static bool CampActive=>Environment.TickCount64-campSeen<500;
    public static string CurrentItemName {get;private set;}="";
    private static long equipmentSeen;
    public static bool EquipmentActive=>Environment.TickCount64-equipmentSeen<500||P2CampNavigation.ItemMenuActive||P2CampNavigation.UnitPageActive;
    private static long itemSeen;
    public static bool ItemActive=>Environment.TickCount64-itemSeen<500;
    public static void Install(Harmony h)
    {
        foreach(string ns in new[]{"P1","P2","P1S","P2S"}) {
            Patch(h,ns+".Bases.Item.SlotSelector",nameof(Item));
            Patch(h,ns+".Bases.Organization.ItemSelectWindow",nameof(Equipment));
        }
        Patch(h,"P1.Bases.Camp.Parts.PartsSelector",nameof(Place));
        Patch(h,"P2.Bases.Camp.Parts.PartsSelector",nameof(Place));
    }
    private static void Patch(Harmony h,string type,string callback)
    {
        try {
            var t=typeof(LauncherMain).Assembly.GetType(type);
            if(t!=null) h.Patch(t.GetMethod("update"),postfix:new HarmonyMethod(typeof(ItemAndPlaceAccess).GetMethod(callback,BindingFlags.Static|BindingFlags.NonPublic)));
        } catch(Exception e) {Reader.Log.LogWarning("Item/place hook: "+e.Message);}
    }
    private static object? Get(object o,string name)=>o.GetType().GetProperty(name)?.GetValue(o);
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name)?.Invoke(o,args);
    private static void Equipment(object __instance)
    {
        try {
            if(__instance is P2.Bases.Organization.ItemSelectWindow)return; // P2 ItemMenu owns the accessor and selection mode.
            if(Call(__instance,"isActive") is not true)return;
            int index=Convert.ToInt32(Get(__instance,"selectIndex_"));
            var ids=Get(__instance,"belongingItemIdArray_");
            if(ids==null || index<0 || index>=Convert.ToInt32(Get(ids,"Count")))return;
            int id=Convert.ToInt32(Call(ids,"get_Item",index));
            if(id<0)return;
            var scene=Get(__instance,"scene_");
            var items=scene==null?null:Call(scene,"getItemOperator");
            var param=items==null?null:Call(items,"getItemParam",id);
            if(param==null)return;
            string ns=__instance.GetType().Namespace!.Split('.')[0];
            string? name=UnitNames.Localized(ns,Get(param,"nameId"));
            if(string.IsNullOrWhiteSpace(name))return;
            string? explain=UnitNames.Localized(ns,Get(param,"explainId"));
            int count=Convert.ToInt32(Call(items!,"getItemBelongNum",id));
            bool equipped=Convert.ToInt32(Get(__instance,"equipItemId_"))==id;
            string text="裝備選擇，"+name+"，持有 "+count+" 件"+(equipped?"，目前裝備":"")+(string.IsNullOrWhiteSpace(explain)?"":"。"+explain);
            long now=Environment.TickCount64;
            bool changed=now-equipmentSeen>500 || text!=lastEquipment;
            equipmentSeen=now;
            if(!changed)return;
            lastEquipment=text;CommonUi.QueueSelection(text);
            Monitor.Record("selected-equipment",new{ns,index,id,name,count,equipped,explain});
        }catch(Exception e){Reader.Log.LogWarning("Equipment selection: "+e.Message);}
    }
    private static void Item(object __instance)
    {
        try {
            var owner=Get(__instance,"owner_");
            if(owner==null || Call(owner,"isActive") is not true) return;
            string ns=__instance.GetType().Namespace!.Split('.')[0];
            int index=Convert.ToInt32(Call(__instance,"getSelectIndex"));
            var slots=Get(owner,"slotArray_");
            if(slots==null || index<0 || index>=Convert.ToInt32(Get(slots,"Count"))) return;
            var slot=Call(slots,"get_Item",index);
            if(slot==null) return;
            int itemId=Convert.ToInt32(Call(slot,"getItemId"));
            if(itemId<0) return;
            var table=Get(owner,"tableAccessor_");
            if(table==null) return;
            var param=Call(table,"getItemParam",itemId);
            if(param==null) return;
            string? name=UnitNames.Localized(ns,Get(param,"nameId"));
            if(string.IsNullOrWhiteSpace(name)) return;
            CurrentItemName=name;
            var belong=Call(slot,"getBelongItem");
            string count=belong==null?"":"，持有 "+Get(belong,"num")+" 個";
            string? explain=UnitNames.Localized(ns,Get(param,"explainId"));
            string type=owner.GetType().Name;
            string context=type.Contains("Stare")?"倉庫":"道具選擇";
            string text=context+"，"+name+count+(string.IsNullOrEmpty(explain)?"":"。"+explain);
            string key=ns+":"+itemId+":"+text;
            long now=Environment.TickCount64;
            bool reopened=now-itemSeen>500;
            itemSeen=now;
            if(key==lastItem && !reopened) return;
            lastItem=key;CommonUi.QueueSelection(text);
            Monitor.Record("selected-item",new{ns,index,itemId,name,count,explain,context});
        } catch(Exception e) {Reader.Log.LogWarning("Selected item: "+e.Message);}
    }
    private static void Place(object __instance)
    {
        try {
            string name="";uint id=0;
            if(__instance is P1.Bases.Camp.Parts.PartsSelector s1) {
                campSeen=s1.selectLock_?0:Environment.TickCount64;
                var facility=s1.getSelectParts()?.TryCast<P1.Bases.Camp.Facility.Facility>();
                if(facility==null) return;name=facility.getName();id=facility.getId();
            } else if(__instance is P2.Bases.Camp.Parts.PartsSelector s2) {
                campSeen=s2.selectLock_?0:Environment.TickCount64;
                var facility=s2.getSelectParts()?.TryCast<P2.Bases.Camp.Facility.Facility>();
                if(facility==null) return;name=facility.getName();id=facility.getId();
            }
            if(string.IsNullOrEmpty(name)) return;
            string key=__instance.GetType().FullName+":"+id;
            if(key==lastPlace) return;
            lastPlace=key;
            Monitor.Record("camp-place",new{id,name});
            string lower=name.ToLowerInvariant();
            if(lower.Contains("lifetree") || lower.Contains("life_tree")) name="生命之樹";
            else if(lower.Contains("warehouse") || lower.Contains("souko") || lower.Contains("storage")) name="倉庫";
            CommonUi.QueueSelection("目前選擇，"+name);
        } catch(Exception e) {Reader.Log.LogWarning("Camp place: "+e.Message);}
    }
}

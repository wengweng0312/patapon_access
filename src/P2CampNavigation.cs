using HarmonyLib;
using System.Reflection;
using UnityEngine;
namespace PataponAccess;

internal static class P2CampNavigation
{
    private static string treeKey="",heroKey="",menuKey="";
    private static readonly ItemSelectionReadout managedEquipmentIntro=new(),normalEquipmentIntro=new();
    private static long treeSeen,heroSeen,menuSeen,lastError;
    private static long normalMenuSeen;
    private static long unitSeen;
    private static string unitKey="",unitRepeat="";
    private static string normalMenuKey="",selectionRepeat="";
    public static bool ItemMenuActive=>Environment.TickCount64-Math.Max(normalMenuSeen,menuSeen)<500;
    public static string RepeatText=>ItemMenuActive?selectionRepeat:Environment.TickCount64-unitSeen<500?unitRepeat:"";
    public static bool UnitPageActive=>Environment.TickCount64-unitSeen<500;
    public static bool TreeActive=>Environment.TickCount64-activeTreeSeen<500;
    private static IntPtr activeTree;
    private static long activeTreeSeen;
    private static IntPtr gridOwner;
    private static int gridX,gridY;
    private static long gridSeen;
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.p2-camp-navigation");
        Hook(h,typeof(P2.Bases.Camp.SelectItem.Behavior.NewUnit),"updateUnitOpenFlag",nameof(Tree));
        Hook(h,typeof(P2.Bases.Camp.EvolutionMap.IconSelector),"selected",nameof(Grid));
        Hook(h,typeof(P2.Bases.HeadQuarters.HeroUI),"idle",nameof(Hero));
        Hook(h,typeof(P2.Bases.Organization.Managed.ItemMenu),"update",nameof(Menu));
        Hook(h,typeof(P2.Bases.Organization.ItemMenu),"update",nameof(NormalMenu));
        Hook(h,typeof(P2.Bases.HeadQuarters.NormalUnitUI),"idle",nameof(UnitPage));
    }
    public static void ObserveTreeModel(P2.Bases.Camp.SelectItem.Behavior.NewUnit row)
    {activeTree=row.Pointer;activeTreeSeen=Environment.TickCount64;}
    private static void Hook(Harmony h,Type type,string method,string callback)
    {
        try {
            var m=type.GetMethod(method)??throw new MissingMethodException(method);
            if(m.GetParameters().Any(p=>p.ParameterType.IsByRef))throw new InvalidOperationException("Unsafe navigation signature");
            h.Patch(m,postfix:new HarmonyMethod(typeof(P2CampNavigation).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));
            Monitor.Record("p2-camp-navigation-hook",new{type=type.FullName,method});
        }catch(Exception e){Reader.Log.LogWarning("P2 camp navigation hook: "+e.Message);}
    }
    private static void Error(Exception e)
    {if(Environment.TickCount64-lastError>5000){lastError=Environment.TickCount64;Monitor.Record("p2-camp-navigation-error",new{error=e.Message});}}
    private static string Key(MultiPlatformInputManager.AllAction action)
    {var names=BindingNames.Read(action);return names.Count>0?string.Join("、",names.Distinct()):I18n.T("ui.b112a6e60034","畫面所示的對應按鍵");}
    private static int Direction()
    {
        var manager=UnityEngine.Object.FindObjectOfType<MultiPlatformInputManager>();if(!manager)return 0;
        var actions=new[]{MultiPlatformInputManager.AllAction.Menu_SelectLeft,MultiPlatformInputManager.AllAction.Menu_SelectUp,MultiPlatformInputManager.AllAction.Menu_SelectRight,MultiPlatformInputManager.AllAction.Menu_SelectDown};
        for(int i=0;i<actions.Length;i++)if(manager.GetInputAction<MultiPlatformInputManager.AllAction>(actions[i])?.WasPressedThisFrame()==true)return i+1;
        return 0;
    }
    private static void Tree(P2.Bases.Camp.SelectItem.Behavior.NewUnit __instance)
    {
        try {
            var row=__instance;
            // updateUnitOpenFlag is also called for idle rows and during construction.
            // Only read the row matched by the currently focused command window.
            if(row.Pointer!=activeTree||Environment.TickCount64-activeTreeSeen>250)return;
            P2NavigationLogger.LifeTree(row);
            // This is the same map and CRC used by native NewUnit.updateActive/renderSelectItem.
            var map=P2.LaboCommon.pLaboCommonInstance_g?.addingParamBuilder_?.unitAddingParamMap_;
            if(map==null||string.IsNullOrEmpty(row.help_))return;
            uint crc=P2.System.Util.BNDFile.getCrc32(row.help_);
            if(!map.ContainsKey(crc))return;
            var units=map[crc];int index=unchecked((int)row.selectId_);
            if(units==null||index<0||index>=units.Count)return;
            var unit=units[index];if(unit==null)return;
            string name=UnitNames.FetchLocalized("P2",LocalizeIds.Unit(unchecked((uint)row.messageId_)))??I18n.T("ui.eaa8dd04fd68","士兵");
            bool empty=unit.evolutionClass<0;
            string status=P2NavigationText.Slot(name,index,units.Count,unit.evolutionClass,unit.evolutionClassLevel,row.isEnableSelect_);
            long now=Environment.TickCount64;
            string signature=$"{row.Pointer}:{index}:{unit.uniqueId}:{status}";
            bool changed=signature!=treeKey||now-treeSeen>700;treeSeen=now;
            if(!changed)return;
            treeKey=signature;CommonUi.QueueSelection(status);
            Monitor.Record("p2-life-tree-readout",new{index,total=units.Count,unit.uniqueId,unit.evolutionClass,unit.evolutionClassLevel,unit.isLive,unit.cap,empty,enabled=row.isEnableSelect_,text=status});
        }catch(Exception e){Error(e);}
    }
    private static void Grid(P2.Bases.Camp.EvolutionMap.IconSelector __instance,P2.Bases.Camp.EvolutionMap.PhyleticIcon? __result)
    {
        try {
            long now=Environment.TickCount64;int x=__instance.posX_,y=__instance.posY_;
            bool previous=gridOwner==__instance.Pointer&&now-gridSeen<700;
            bool moved=!previous||x!=gridX||y!=gridY;
            int direction=Direction();
            gridOwner=__instance.Pointer;gridX=x;gridY=y;gridSeen=now;
            string text="";
            if(moved&&__result!=null&&!__result.isUnlock_){
                var id=__result.classTypeParam_?.msgId;
                // Class message IDs are not unit-name category IDs. Never probe
                // an unverified category/index pair in the native localizer.
                string name=id==null?"":UnitNames.Localized("P2",id.Value)??"";
                text=string.IsNullOrWhiteSpace(name)?I18n.T("ui.fc7f0f8d88ad","未解鎖"):I18n.T("ui.41e88f9de3dc","未解鎖，")+name;
            }else if(previous&&!moved&&direction!=0)text=P2NavigationText.Blocked(x,y,direction);
            if(text.Length==0)return;
            P2EvolutionAccess.Navigation(text,moved);
            Monitor.Record("p2-evolution-navigation-speech",new{x,y,direction,moved,unlocked=__result?.isUnlock_,text});
        }catch(Exception e){Error(e);}
    }
    private static void Hero(P2.Bases.HeadQuarters.HeroUI __instance)
    {
        try {
            var model=__instance.heroModel_;if(model==null||model.isFinish_)return;
            long now=Environment.TickCount64;int slot=model.slotID_;
            bool entered=now-heroSeen>700;
            string signature=$"{model.Pointer}:{slot}";
            bool changed=entered||signature!=heroKey;heroKey=signature;heroSeen=now;
            if(!changed)return;
            string text=slot<0?I18n.T("ui.aef0f9d3e32c","英雄稀有PON形態，按確認變更"):I18n.T("ui.9f0096b15e8d","英雄裝備，第 ")+(slot+1)+I18n.T("ui.6d1f8ad23ef0"," 格");
            if(entered)text+=I18n.T("ui.7a443cf4e299","。切換英雄兵種，按 ")+Key(MultiPlatformInputManager.AllAction.Menu_West)+I18n.T("ui.beba98411b62","。兵種會決定英雄模式招式");
            CommonUi.QueueSelection(text);
            Monitor.Record("p2-hero-navigation",new{slot,state=model.state_.ToString(),text});
        }catch(Exception e){Error(e);}
    }
    private static void Menu(P2.Bases.Organization.Managed.ItemMenu __instance)
    {
        try {
            var menu=__instance;if(!menu.isActive_){managedEquipmentIntro.Close("managed:"+menu.Pointer);menuSeen=0;return;}
            var behavior=menu.currentSelectBehavior_;
            bool job=behavior?.TryCast<P2.Bases.Organization.Managed.UnitTypeSelectBehavior>()!=null;
            bool rare=behavior?.TryCast<P2.Bases.Organization.Managed.ClassSelectBehavior>()!=null;
            var window=menu.itemSelectWindow_;if(window==null||!window.isActive_)return;
            long now=Environment.TickCount64;bool reopened=now-menuSeen>700;menuSeen=now;
            int index=window.selectIndex_;var ids=window.belongingItemIdArray_;
            if(ids==null)return;
            if(index<0||index>=ids.Count){ReadMenu(I18n.T("ui.fe2add6cf52b","空白位置"),index,"managed",reopened,ref menuKey);return;}
            var desc=ids[index];if(desc==null||desc.itemId<0){ReadMenu(I18n.T("ui.fe2add6cf52b","空白位置"),index,"managed",reopened,ref menuKey);return;}
            int id=desc.itemId;var param=menu.itemOperator_?.getItemParam(id);if(param==null){ReadMenu(I18n.T("ui.bc3b7dc141b7","選項資料未取得"),index,"managed",reopened,ref menuKey);return;}
            string? name=UnitNames.FetchLocalized("P2",unchecked((uint)param.nameId));if(string.IsNullOrWhiteSpace(name)){ReadMenu(I18n.T("ui.ce8bbee8ab33","選項名稱未取得"),index,"managed",reopened,ref menuKey);return;}
            string? explain=UnitNames.FetchLocalized("P2",unchecked((uint)param.explainId));
            string context=job?I18n.T("ui.523bbd6ffa13","英雄兵種選擇"):rare?I18n.T("ui.beadc2575e12","稀有PON型態選擇"):I18n.T("ui.f23b8e657c9a","裝備選擇");
            int? count=job||rare?null:menu.itemOperator_!.getItemBelongNum(id);
            string text=P2NavigationText.Item(context,name,index,ids.Count,count,window.equipItemId_==id,explain);
            bool changed=text!=menuKey||reopened;
            if(!changed)return;menuKey=text;
            if(!job&&!rare){
                string owner="managed:"+menu.Pointer;
                if(reopened)managedEquipmentIntro.Close(owner);
                text=managedEquipmentIntro.Observe(owner,context,text.Substring(context.Length+1))??text;
            }else managedEquipmentIntro.Close("managed:"+menu.Pointer);
            CommonUi.QueueSelection(text);
            selectionRepeat=text;
            Monitor.Record("p2-hero-type-selection",new{job,index,id,request=desc.request,text});
        }catch(Exception e){Error(e);}
    }
    private static void UnitPage(P2.Bases.HeadQuarters.NormalUnitUI __instance)
    {
        try {
            var model=__instance.heroModel_;if(model==null||model.isFinish_||model.itemMenu_?.isActive_==true)return;
            int slot=model.slotID_;
            var unit=model.squad_?.getUnit(model.unitID_);var param=unit?.addingParam_;
            string form="";int rare=param?.evolutionClass??-1;
            var icons=P2.Bases.Organization.Managed.ClassSelectBehavior.iconItemIdTable_g;
            if(icons!=null&&rare>=0&&rare<icons.Length){
                int id=icons[rare];var item=id<0?null:model.itemMenu_?.itemOperator_?.getItemParam(id);
                if(item!=null)form=UnitNames.Localized("P2",item.nameId)??"";
            }
            long now=Environment.TickCount64;bool entered=now-unitSeen>700;
            string signature=$"{model.Pointer}:{model.unitID_}:{slot}:{rare}:{param?.evolutionClassLevel}";
            bool changed=entered||signature!=unitKey;unitSeen=now;unitKey=signature;
            if(!changed)return;
            int total=checked((int)(model.squad_?.numUnit_??0));
            unitRepeat=P2NavigationText.UnitPosition(unchecked((int)model.unitID_),total)+"，"+P2NavigationText.UnitPage(slot,form,param?.evolutionClassLevel??0,entered);
            if(entered)unitRepeat+=I18n.T("ui.b45472ee746e","。切換士兵，先退出裝備清單，再按 ")+Key(MultiPlatformInputManager.AllAction.Menu_SelectLeft)+I18n.T("ui.60ab743988bb"," 或 ")+Key(MultiPlatformInputManager.AllAction.Menu_SelectRight);
            CommonUi.QueueSelection(unitRepeat);
            Monitor.Record("p2-unit-page-navigation",new{unit=model.unitID_,total,slot,rare,level=param?.evolutionClassLevel,form,text=unitRepeat});
        }catch(Exception e){Error(e);}
    }
    private static void ReadMenu(string text,int index,string context,bool reopened,ref string prior)
    {
        string signature=context+":"+index+":"+text;
        selectionRepeat=text;
        if(signature==prior&&!reopened)return;
        prior=signature;CommonUi.QueueSelection(text);
        Monitor.Record("p2-item-menu-selection",new{context,index,text});
    }
    private static void NormalMenu(P2.Bases.Organization.ItemMenu __instance)
    {
        try {
            var menu=__instance;if(!menu.isActive_){normalEquipmentIntro.Close("normal:"+menu.Pointer);normalMenuSeen=0;return;}
            var window=menu.itemSelectWindow_;if(window==null||!window.isActive_)return;
            long now=Environment.TickCount64;bool reopened=now-normalMenuSeen>700;normalMenuSeen=now;
            bool rare=menu.currentSelectBehavior_?.TryCast<P2.Bases.Organization.ClassSelectBehavior>()!=null;
            string context=rare?I18n.T("ui.beadc2575e12","稀有PON型態選擇"):I18n.T("ui.f23b8e657c9a","裝備選擇");
            int index=window.selectIndex_;var ids=window.belongingItemIdArray_;
            if(ids==null)return;
            if(index<0||index>=ids.Count||ids[index]<0){ReadMenu(I18n.T("ui.fe2add6cf52b","空白位置"),index,context,reopened,ref normalMenuKey);return;}
            int id=ids[index];var items=menu.itemOperator_;var param=items?.getItemParam(id);
            if(param==null){ReadMenu(I18n.T("ui.bc3b7dc141b7","選項資料未取得"),index,context,reopened,ref normalMenuKey);return;}
            string? name=UnitNames.FetchLocalized("P2",unchecked((uint)param.nameId));
            if(string.IsNullOrWhiteSpace(name)){ReadMenu(I18n.T("ui.ce8bbee8ab33","選項名稱未取得"),index,context,reopened,ref normalMenuKey);return;}
            string? explain=UnitNames.FetchLocalized("P2",unchecked((uint)param.explainId));
            int? count=rare?null:items!.getItemBelongNum(id);
            string text=P2NavigationText.Item(context,name,index,ids.Count,count,window.equipItemId_==id,explain);
            string signature=text;
            if(signature==normalMenuKey&&!reopened)return;
            normalMenuKey=signature;
            if(!rare){
                string owner="normal:"+menu.Pointer;
                if(reopened)normalEquipmentIntro.Close(owner);
                text=normalEquipmentIntro.Observe(owner,context,text.Substring(context.Length+1))??text;
            }else normalEquipmentIntro.Close("normal:"+menu.Pointer);
            selectionRepeat=text;CommonUi.QueueSelection(text);
            Monitor.Record("p2-item-menu-selection",new{context,index,text});
        }catch(Exception e){Error(e);}
    }
}

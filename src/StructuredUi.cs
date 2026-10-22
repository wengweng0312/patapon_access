using HarmonyLib;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace PataponAccess;
internal static class StructuredUi
{
    private record DialogData(string body,string[] choices,long generation);
    private record ChoiceState(string key,string prompt,string selected);
    private static readonly DialogSpeechTracker dialogSpeech=new();
    private static ChoiceState? giftChoice;
    private static long generation;
    private static readonly Dictionary<IntPtr,string[]> stats=new();
    private static readonly Dictionary<IntPtr,DialogData> dialogs=new();
    private static string gift="",squad="",candidate="",spoken="",repeat="";
    private static long giftSeen,squadSeen,changedAt,extraSeen,nextSquadRead;
    private static string extra="";
    private static bool active;
    public static string ContextKind {get;private set;}="";
    private static readonly SquadIntroTracker squadIntro=new();
    public static string RepeatText=>active?repeat:"";
    private static string squadKind="squad";
    internal static void PublishSquad(string text,string kind="squad"){squad=text;squadKind=kind;squadSeen=Environment.TickCount64;}
    private static object? Get(object o,string name)=>o.GetType().GetProperty(name)?.GetValue(o);
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name)?.Invoke(o,args);
    private static IntPtr Ptr(object o)=>((Il2CppObjectBase)o).Pointer;
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.structured-ui");
        P2SquadAccess.Install(h);
        RushP2SquadAccess.Install(h);
        foreach(string game in new[]{"P1","P1S"})Patch(h,game+".Bases.Organization.ExtraMenu","update",nameof(Extra));
        foreach(string ns in new[]{"P1","P2","P1S","P2S"}) {
            Patch(h,ns+".Bases.Item.Dialog","setup",nameof(DialogSetup));
            Patch(h,ns+".Bases.Item.PresentDialog","update",nameof(Gift));
        }
        foreach(string ns in new[]{"P1","P2","P1S","P2S"}) {
            // P2 Message is a value struct, unlike P1 reference type. Do not detour it.
            if(ns is "P1" or "P1S")Patch(h,ns+".Bases.Organization.SquadStatusDisplay","setMessage",nameof(Stat));
            Patch(h,ns+".Bases.Organization.SquadSelectMenu","update",nameof(Squad));
        }
    }
    private static void Patch(Harmony h,string type,string method,string callback)
    {
        try {var t=typeof(LauncherMain).Assembly.GetType(type);if(t==null)return;var m=t.GetMethod(method);if(m==null||m.GetParameters().Any(p=>p.ParameterType.IsByRef||(p.ParameterType.IsValueType&&!p.ParameterType.IsPrimitive&&!p.ParameterType.IsEnum)))return;h.Patch(m,postfix:new HarmonyMethod(typeof(StructuredUi).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));Monitor.Record("structured-ui-hook",new{type,method});}
        catch(Exception e){Reader.Log.LogWarning("Structured UI hook: "+e.Message);}
    }
    private static void Stat(object __instance,string __1,uint __2)
    {
        try {
            if(__2>=ReadoutText.SquadLabels.Length)return;
            var key=Ptr(__instance);if(!stats.TryGetValue(key,out var values)){if(stats.Count>32)stats.Clear();stats[key]=values=new string[ReadoutText.SquadLabels.Length];}
            values[__2]=DialogueText.Clean(__1);
        }catch(Exception e){Reader.Log.LogWarning("Squad value: "+e.Message);}
    }
    private static void Squad(object __instance)
    {
        try {
            if((Call(__instance,"isActive") is not true&&Get(__instance,"isActiveStatusDisplay_") is not true)||ItemAndPlaceAccess.EquipmentActive)return;
            if(Environment.TickCount64<nextSquadRead)return;nextSquadRead=Environment.TickCount64+100;
            // An active native status page owns its readout even while resources
            // are loading. Never fall back to a soup of pooled background text.
            squadKind="squad";squad=I18n.T("ui.5ea850d4e5f5","部隊狀態，資料尚未取得");
            squadSeen=Environment.TickCount64;
            int index=Convert.ToInt32(Get(__instance,"selectIndex_"));var indices=Get(__instance,"paramIndexArray_");int total=indices==null?0:Convert.ToInt32(Get(indices,"Count"));
            if(index<0||index>=total)return;
            // getSelectSquad returns the currently assigned squad, not the replacement cursor.
            var slots=Get(__instance,"activeSlotList_");if(slots==null||index>=Convert.ToInt32(Get(slots,"Count")))return;
            var slot=Call(slots,"get_Item",index);var param=slot==null?null:Get(slot,"param_");if(param==null)return;
            string? name=UnitNames.FetchLocalized(__instance.GetType().Namespace!.Split('.')[0],LocalizeIds.Unit(Convert.ToUInt32(Get(param,"charNameId"))));
            if(string.IsNullOrWhiteSpace(name))return;
            string game=__instance.GetType().Namespace!.Split('.')[0];
            var display=Get(__instance,"statusDisplay_");
            string[] values=Array.Empty<string>();
            if((game is "P1" or "P1S")&&(display==null||!stats.TryGetValue(Ptr(display),out values!)||values.Any(string.IsNullOrWhiteSpace)))return;
            if(game is "P2" or "P2S")values=SquadScreenValues.Read();
            if(game is "P2" or "P2S")squad=SquadScreenValues.Describe(name,index,total,0,0,values);
            else {
                // The active display owns both labels and values. Do not replace its
                // terminology or derive counters from the underlying soldier list.
                SquadScreenValues.Read();
                var parts=new List<string>{name};parts.AddRange(SquadScreenValues.Headers);
                var rows=display==null?null:Get(display,"messageArray_");
                if(rows!=null)for(int i=0;i<Math.Min(values.Length,Convert.ToInt32(Get(rows,"Count")));i++){
                    var row=Call(rows,"get_Item",i);var labelMessage=row==null?null:Get(row,"explain");
                    string label=labelMessage==null?"":DialogueText.Clean(Call(labelMessage,"getDrawInfoMessage") as string??"");
                    if(label.Length>0&&!string.IsNullOrWhiteSpace(values[i]))parts.Add(label+" "+values[i]);
                }
                squad=string.Join("。",parts);
            }
            squadSeen=Environment.TickCount64;
        }catch(Exception e){Reader.Log.LogWarning("Squad readout: "+e.Message);}
    }
    private static void Extra(object __instance)
    {
        try {
            var menu=__instance;string game=menu.GetType().Namespace!.Split('.')[0];
            var scene=Get(menu,"scene_");var selector=scene==null?null:Get(scene,"itemSelectWindow_");
            if(Get(menu,"isActive_") is not true||Get(menu,"isActiveItemSelect_") is true||(selector!=null&&Get(selector,"isActive_") is true)){extraSeen=0;return;}
            int index=Convert.ToInt32(Get(menu,"selectIndex_"));
            if(index is not (0 or 1)){extraSeen=0;return;}
            int id=Convert.ToInt32(Call(menu,index==0?"getMiracleItemId":"getCookingItemId"));
            string header=index==0?I18n.T("loadout.miracle","奇蹟"):I18n.T("loadout.cooking","料理");
            string name=I18n.T("loadout.none","未裝備");string? explain=null;
            if(id>=0){
                var items=scene==null?null:Call(scene,"getItemOperator");var param=items==null?null:Call(items,"getItemParam",id);
                name=param==null?I18n.T("ui.ce8bbee8ab33","選項名稱未取得"):UnitNames.FetchLocalized(game,unchecked((uint)Convert.ToInt32(Get(param,"nameId"))))??I18n.T("ui.ce8bbee8ab33","選項名稱未取得");
                if(param!=null)explain=UnitNames.FetchLocalized(game,unchecked((uint)Convert.ToInt32(Get(param,"explainId"))));
            }
            string text=header+"，"+name+(string.IsNullOrWhiteSpace(explain)?"":"。"+explain);
            if(extra!=text||Environment.TickCount64-extraSeen>500)Monitor.Record("loadout-extra-slot",new{game,index,id,text});
            extra=text;extraSeen=Environment.TickCount64;
        }catch(Exception e){extraSeen=0;Reader.Log.LogWarning("Extra slot: "+e.Message);}
    }
    private static void DialogSetup(object __instance,int __1,object __2,int __3,object __4)
    {
        try {
            string ns=__instance.GetType().Namespace!.Split('.')[0];
            string[] Read(object array,int count)=>Enumerable.Range(0,Math.Clamp(count,0,32)).Select(i=>Call(array,"get_Item",i)).Select(v=>v==null?"":UnitNames.FetchLocalized(ns,unchecked((uint)Convert.ToInt32(v)))??"").ToArray();
            string body=string.Join("。",Read(__2,__1).Where(s=>s.Length>0));var choices=Read(__4,__3);
            if(dialogs.Count>32)dialogs.Clear();dialogs[Ptr(__instance)]=new DialogData(body,choices,++generation);
            Monitor.Record("item-dialog-content",new{ns,body,choices});
        }catch(Exception e){Reader.Log.LogWarning("Item dialog content: "+e.Message);}
    }
    private static void Gift(object __instance)
    {
        try {
            if(Get(__instance,"isActive_") is not true)return;
            var dialog=Get(__instance,"dialog_");if(dialog==null)return;
            if(!dialogs.TryGetValue(Ptr(dialog),out var data))return;
            int index=Convert.ToInt32(Call(dialog,"getSelectIndex"));
            string context=ItemAndPlaceAccess.CurrentItemName;
            gift=ReadoutText.Choice(data.body,data.choices,index,context);
            if(gift.Length>0){giftSeen=Environment.TickCount64;giftChoice=new ChoiceState("gift:"+Ptr(dialog)+":"+data.generation+":"+context,ReadoutText.Prompt(data.body,context),data.choices[index]);}
        }catch(Exception e){Reader.Log.LogWarning("Present confirmation: "+e.Message);}
    }
    private static string SaveDialog(out ChoiceState? state)
    {
        state=null;
        foreach(var window in UnityEngine.Object.FindObjectsOfType<Px.SaveData.ConfirmationWindow>()) {
            if(!window||!window.gameObject.activeInHierarchy||window.IsClosed()||!window.description||!window.description.isActiveAndEnabled)continue;
            if(!window.okText||!window.cancelText)continue;
            var choices=new[]{Speech.Clean(window.okText.text),Speech.Clean(window.cancelText.text)};
            string body=Speech.Clean(window.description.text);string text=ReadoutText.Choice(body,choices,window.selectIndex);
            if(text.Length>0)state=new ChoiceState("save:"+window.GetInstanceID()+":"+window.targetSlot+":"+body,ReadoutText.Prompt(body),choices[window.selectIndex]);
            return text;
        }
        return "";
    }
    public static bool Scan()
    {
        long now=Environment.TickCount64;string save=SaveDialog(out var choice);
        bool giving=save.Length==0&&now-giftSeen<250;
        if(giving)choice=giftChoice;
        bool extraActive=now-extraSeen<250&&!ItemAndPlaceAccess.EquipmentActive;
        string text=ReadoutText.Select(save,giving?gift:"",extraActive?extra:now-squadSeen<250?squad:"",CommonUi.CommandWindowActive);
        if(text.Length==0){if(active){if(!ItemAndPlaceAccess.EquipmentActive)CommonUi.Clear();DialogueAccess.Clear();}active=false;ContextKind="";squadIntro.Reset();candidate=spoken=repeat="";dialogSpeech.Reset();return false;}
        ContextKind=save.Length>0?"save-confirm":giving?"gift-confirm":extraActive?"extra-slot":squadKind;
        if(ContextKind!="squad")squadIntro.Reset();
        if(!active){CommonUi.Clear();DialogueAccess.Clear();}active=true;repeat=text;
        string signature=choice==null?text:choice.key+"|"+text;
        if(signature!=candidate){candidate=signature;changedAt=now;}
        if(signature!=spoken&&now-changedAt>=180){spoken=signature;string speech=choice==null?(ContextKind=="squad"?squadIntro.Say(text):text):dialogSpeech.Say(choice.key,choice.prompt,choice.selected);if(choice==null)dialogSpeech.Reset();Speech.Speak(speech);Monitor.Record("structured-ui-speech",new{text=speech,fullText=text});}
        return true;
    }
}

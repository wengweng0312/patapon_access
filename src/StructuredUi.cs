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
    private static long giftSeen,squadSeen,changedAt;
    private static bool active;
    public static string RepeatText=>active?repeat:"";
    private static object? Get(object o,string name)=>o.GetType().GetProperty(name)?.GetValue(o);
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name)?.Invoke(o,args);
    private static IntPtr Ptr(object o)=>((Il2CppObjectBase)o).Pointer;
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.structured-ui");
        foreach(string ns in new[]{"P1","P2"}) {
            Patch(h,ns+".Bases.Item.Dialog","setup",nameof(DialogSetup));
            Patch(h,ns+".Bases.Item.PresentDialog","update",nameof(Gift));
        }
        Patch(h,"P1.Bases.Organization.SquadStatusDisplay","setMessage",nameof(Stat));
        Patch(h,"P1.Bases.Organization.SquadSelectMenu","update",nameof(Squad));
    }
    private static void Patch(Harmony h,string type,string method,string callback)
    {
        try {var t=typeof(LauncherMain).Assembly.GetType(type);if(t==null)return;var m=t.GetMethod(method);if(m==null||m.GetParameters().Any(p=>p.ParameterType.IsByRef))return;h.Patch(m,postfix:new HarmonyMethod(typeof(StructuredUi).GetMethod(callback,BindingFlags.NonPublic|BindingFlags.Static)));Monitor.Record("structured-ui-hook",new{type,method});}
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
            if(Call(__instance,"isActive") is not true||ItemAndPlaceAccess.EquipmentActive)return;
            int index=Convert.ToInt32(Get(__instance,"selectIndex_"));var indices=Get(__instance,"paramIndexArray_");int total=indices==null?0:Convert.ToInt32(Get(indices,"Count"));
            if(index<0||index>=total)return;
            // getSelectSquad returns the currently assigned squad, not the replacement cursor.
            var slots=Get(__instance,"activeSlotList_");if(slots==null||index>=Convert.ToInt32(Get(slots,"Count")))return;
            var slot=Call(slots,"get_Item",index);var param=slot==null?null:Get(slot,"param_");if(param==null)return;
            string? name=UnitNames.FetchLocalized("P1",LocalizeIds.Unit(Convert.ToUInt32(Get(param,"charNameId"))));
            if(string.IsNullOrWhiteSpace(name))return;
            var display=Get(__instance,"statusDisplay_");if(display==null||!stats.TryGetValue(Ptr(display),out var values)||values.Any(string.IsNullOrWhiteSpace))return;
            int reborn=0,missions=0;var units=Call(__instance,"getSelectAddingParam");
            if(units!=null)for(int i=0;i<Math.Min(64,Convert.ToInt32(Get(units,"Count")));i++){var unit=Call(units,"get_Item",i);if(unit==null)continue;reborn+=Convert.ToInt32(Get(unit,"deadCount"));missions=Math.Max(missions,Convert.ToInt32(Get(unit,"missionCount")));}
            squad=ReadoutText.Squad(name,index,total,Math.Clamp(reborn,0,999),Math.Clamp(missions,0,999),values);squadSeen=Environment.TickCount64;
        }catch(Exception e){Reader.Log.LogWarning("Squad readout: "+e.Message);}
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
        string text=ReadoutText.Select(save,giving?gift:"",now-squadSeen<250?squad:"",CommonUi.CommandWindowActive);
        if(text.Length==0){if(active){CommonUi.Clear();DialogueAccess.Clear();}active=false;candidate=spoken=repeat="";dialogSpeech.Reset();return false;}
        if(!active){CommonUi.Clear();DialogueAccess.Clear();}active=true;repeat=text;
        string signature=choice==null?text:choice.key+"|"+text;
        if(signature!=candidate){candidate=signature;changedAt=now;}
        if(signature!=spoken&&now-changedAt>=180){spoken=signature;string speech=choice==null?text:dialogSpeech.Say(choice.key,choice.prompt,choice.selected);if(choice==null)dialogSpeech.Reset();Speech.Speak(speech);Monitor.Record("structured-ui-speech",new{text=speech,fullText=text});}
        return true;
    }
}

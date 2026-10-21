using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Reflection;

namespace PataponAccess;

internal static class CommonUi
{
    private static string pending="",candidate="",stable="",candidateText="",lastFocus="";
    private static string selectedPending="";
    private static long selectedReadyAt;
    private static long readyAt,changedAt,focusSeen;
    private static long commandWindowSeen;
    public static bool CommandWindowActive=>Environment.TickCount64-commandWindowSeen<350;
    private static float next;
    private static string repeatText="";
    private static readonly CommandDialogSpeech dialogSpeech=new();
    private static bool pendingDialog;
    public static string RepeatText=>Environment.TickCount64-focusSeen<800?repeatText:"";
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.commonui");
        UnitNames.Install(h);
        ItemAndPlaceAccess.Install(h);
        foreach(string ns in new[]{"P1","P2","P1S","P2S"}) {
            var type=typeof(LauncherMain).Assembly.GetType(ns+".System.Gfx.Window.CommandWindow");
            if(type==null) continue;
            try {h.Patch(type.GetMethod("update"),postfix:new HarmonyMethod(typeof(CommonUi).GetMethod(nameof(ObserveWindow),BindingFlags.Static|BindingFlags.NonPublic)));Reader.Log.LogInfo("Common selection speech hook: "+type.FullName);}
            catch(Exception e) {Reader.Log.LogWarning("Common selection hook: "+e.Message);}
        }
    }
    private static object? Get(object o,string name)=>o.GetType().GetProperty(name)?.GetValue(o);
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name)?.Invoke(o,args);
    private static string lastResolved="",templateOwner="";
    private static readonly Dictionary<string,string> templates=new();
    private static bool unresolvedVariables,pendingVariables;
    private static string Chars(object? array)
    {
        if(array is Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<char> chars)
            return ResolveVariables(new string(chars.ToArray()).Split('\0')[0]);
        return "";
    }
    private static string ResolveVariables(string raw)
    {
        string? template=DialogueText.HasVariableToken(raw)?raw:templates.GetValueOrDefault(raw)??VariableTemplates.Find(raw,Environment.TickCount64);
        if(template!=null){if(templates.Count>=64)templates.Clear();templates[raw]=template;raw=template;}
        if(DialogueText.HasVariableToken(raw))foreach(var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) {
            if(!text||!text.isActiveAndEnabled||text.color.a<0.05f||text.maxVisibleCharacters==0)continue;
            if(!text.name.StartsWith("DrumSysmteText_",StringComparison.Ordinal)&&!text.name.StartsWith("DrumNormalText_",StringComparison.Ordinal))continue;
            string resolved=DialogueText.ResolveTemplate(raw,text.text);
            if(resolved.Length==0)continue;
            string signature=raw+"|"+resolved;
            if(lastResolved!=signature){lastResolved=signature;Monitor.Record("common-variable-resolved",new{template=raw,text=resolved,path=Monitor.PathOf(text.transform)});}
            return resolved;
        }
        if(DialogueText.HasVariableToken(raw))unresolvedVariables=true;
        return DialogueText.Clean(raw);
    }
    private static void ObserveWindow(object __instance)
    {
        try {
            if(Call(__instance,"isForcus") is not true || Call(__instance,"isRunning") is not true) return;
            string variableOwner=__instance.GetType().FullName+":"+((Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)__instance).Pointer;
            if(templateOwner!=variableOwner||Environment.TickCount64-commandWindowSeen>500){templates.Clear();templateOwner=variableOwner;}
            unresolvedVariables=false;
            long priorWindowSeen=commandWindowSeen;
            commandWindowSeen=Environment.TickCount64;
            int index=Convert.ToInt32(Call(__instance,"getCursorPos"));
            var commands=Get(__instance,"cmdAry_");
            if(commands==null || index<0 || index>=Convert.ToInt32(Get(commands,"Count"))) return;
            var item=Call(commands,"get_Item",index);
            if(item==null) return;
            string label=Chars(Get(item,"name"));
            string help=Chars(Get(item,"help"));
            string title=Chars(Call(__instance,"getName"));
            string ns=__instance.GetType().Namespace?.Split('.')[0]??"";
            if(ns is "P2" or "P2S" && P2EvolutionAccess.ObserveWindow(title,label,((Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)__instance).Pointer)){commandWindowSeen=priorWindowSeen;return;}
            string body="";
            var optionIndices=new List<int>();
            var commandLabels=new List<string>();
            {
                var content=new List<string>();
                int count=Math.Min(64,Convert.ToInt32(Get(commands,"Count")));
                for(int j=0;j<count;j++) {
                    var other=Call(commands,"get_Item",j);
                    if(other==null) continue;
                    string value=Chars(Get(other,"name"));
                    commandLabels.Add(value);
                    if(CommandDialogSpeech.IsChoice(value)) optionIndices.Add(j);
                    else if(value.Length>0 && value!=title && value!="%") content.Add(value);
                }
                body=string.Join("。",content.Distinct());
            }
            if(title=="英雄的記憶") title=I18n.T("ui.20c967cdc8ef","生命之樹，英雄的記憶");
            if(label=="%" && help.StartsWith("unit")) {
                string? unitName=UnitNames.Resolve(__instance.GetType().Namespace?.Split('.')[0]??"P1",help);
                if(unitName!=null) {label=unitName;help="";}
            }
            string text=string.Join("，",new[]{title,label,help}.Where(s=>s.Length>0).Distinct());
            if(label.Length==0) return;
            // Only native life-tree behaviours own this balance; do not infer it from text or cost_.
            if(ns=="P1") {
                var callback=Get(item,"callback");
                var target=callback==null?null:Get(callback,"Target") as Il2CppSystem.Object;
                if(target?.TryCast<P1.Bases.Camp.SelectItem.Behavior.EnagyValueDisp>()!=null) {
                    var data=P1.LaboCommon.getInstance()?.getLaboGlobalData()?.basesData;
                    if(data!=null) text+="，"+I18n.T("training.currency","持有響聲 {0}",data.energy);
                }
            }
            long now=Environment.TickCount64;
            string ordinal=optionIndices.Contains(index)?I18n.T("ui.33ca23bae65d","，第 {0} 項，共 {1} 項",optionIndices.IndexOf(index)+1,optionIndices.Count):I18n.T("ui.fab2a038b6d0","，第 ")+(index+1)+I18n.T("ui.06189562e746"," 項");
            string owner=__instance.GetType().FullName+":"+((Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)__instance).Pointer;
            bool confirmation=optionIndices.Contains(index);
            bool isDialog=title.Equals("Information",StringComparison.OrdinalIgnoreCase)||confirmation;
            bool entered=false;
            if(isDialog) {
                string prompt=confirmation?string.Join("。",new[]{body,help}.Where(s=>s.Length>0).Distinct()):body;
                string selected=confirmation?label:string.Join("，",new[]{label,help}.Where(s=>s.Length>0).Distinct())+ordinal;
                entered=dialogSpeech.Observe(owner,title,prompt,selected,string.Join("|",commandLabels),now);
                repeatText=dialogSpeech.Repeat;
            }else {dialogSpeech.Reset();repeatText=text+ordinal;}
            string focusKey=owner+"|"+text+"|"+body;
            if(focusKey!=lastFocus || now-focusSeen>1500 || entered) {
                lastFocus=focusKey;pending=repeatText;pendingDialog=isDialog;readyAt=now+140;
                Monitor.Record("common-selection",new{type=__instance.GetType().FullName,index,title,body,label,help,isDialog,confirmation,text});
            }
            pendingVariables=unresolvedVariables;
            focusSeen=now;
        } catch(Exception e) {
            if(Environment.TickCount64-focusSeen>5000) {focusSeen=Environment.TickCount64;Reader.Log.LogWarning("Common selection observation: "+e.Message);}
        }
    }
    public static void Scan()
    {
        if(P2EvolutionAccess.IsActive || ItemAndPlaceAccess.EquipmentActive || ItemAndPlaceAccess.ItemActive)return;
        if(Time.unscaledTime<next) return;
        next=Time.unscaledTime+0.15f;
        var values=new List<(string text,float x,float y)>();
        bool hasWindow=Environment.TickCount64-focusSeen<700;
        // Focused command windows supply their own label/help/dialog body.
        // Pooled background text must not interrupt that selection speech.
        if(hasWindow)return;
        foreach(var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) {
            if(!t || !t.isActiveAndEnabled || t.maxVisibleCharacters==0 || t.color.a<0.05f) continue;
            if(MiracleAccess.Suppress(t))continue;
            string name=t.name;
            if(!name.StartsWith("DrumNormalText_") && !(name.StartsWith("DrumSysmteText_") && !hasWindow)) continue;
            string text=Speech.Clean(t.text);
            if(text.Length==0||text=="SQUAD STATUS") continue;
            if(BattleChants.SuppressVisible(DialogueText.Clean(t.text)))continue;
            var p=t.transform.position;
            values.Add((text,p.x,p.y));
        }
        // Compare content as a set because the game reuses and reorders text pool slots.
        string signature=string.Join("\n",values.Select(v=>v.text).Distinct().OrderBy(s=>s,StringComparer.Ordinal));
        long now=Environment.TickCount64;
        if(signature!=candidate) {
            candidate=signature;changedAt=now;
            candidateText=string.Join("。",values.OrderByDescending(v=>v.y).ThenBy(v=>v.x).Select(v=>v.text).Distinct());
        }
        if(signature!=stable && now-changedAt>=180) {
            stable=signature;
            if(candidateText.Length>0) {
                pending=candidateText;pendingDialog=false;pendingVariables=false;readyAt=now+50;
                Monitor.Record("common-visible-text",new{text=candidateText,selectionKnown=hasWindow});
            }
        }
    }
    private static bool selectedInterrupt=true;
    public static bool TakenInterrupt {get;private set;}=true;
    public static string Take()
    {
        if(selectedPending.Length>0) {
            if(Environment.TickCount64<selectedReadyAt)return "";
            string selected=selectedPending;TakenInterrupt=selectedInterrupt;selectedPending="";pending="";pendingDialog=false;
            stable=candidate;
            Monitor.Record("selection-speech-ready",new{text=selected});
            return selected;
        }
        if(Environment.TickCount64<readyAt||pendingVariables&&Environment.TickCount64-readyAt<350) return "";
        string text=pendingDialog&&pending.Length>0?dialogSpeech.Take():pending;TakenInterrupt=true;pending="";pendingDialog=false;return text;
    }
    public static void Clear() {pendingVariables=false;pending="";selectedPending="";pendingDialog=false;dialogSpeech.Reset();stable=candidate;}
    public static void QueueSelection(string text,bool interrupt=true) {selectedInterrupt=interrupt;selectedPending=text;repeatText=text;focusSeen=Environment.TickCount64;selectedReadyAt=focusSeen+120;}
}


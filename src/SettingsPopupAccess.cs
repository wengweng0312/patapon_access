using TMPro;
using UnityEngine;
namespace PataponAccess;
internal static class SettingsPopupAccess
{
    private static string key="",repeat="";
    private static bool active;
    private static readonly DialogSpeechTracker tracker=new();
    public static string RepeatText=>active?repeat:"";
    private static string Text(TMP_Text? t)=>VisibleUiText.Read(t);
    public static bool Scan()
    {
        foreach(var w in UnityEngine.Object.FindObjectsOfType<SettingWindowBase>()){
            if(!w||!w.gameObject.activeInHierarchy||w.IsClosed())continue;
            string prompt="",selected="";
            var bind=w.TryCast<RebindKeyWindow>();var dup=w.TryCast<DuplicateKeyCheckWindow>();var confirm=w.TryCast<ConfirmWithTimerWindow>();
            if(bind!=null)prompt=string.Join("，",new[]{Text(bind.labelText),Text(bind.descriptionText),Text(bind.allocationText),Text(bind.currentKeyText)}.Where(s=>s.Length>0).Distinct());
            else if(dup!=null){prompt=string.Join("，",new[]{Text(dup.selectKeyText),Text(dup.descriptionText),Text(dup.actionNameText),Text(dup.confirmationText)}.Where(s=>s.Length>0).Distinct());selected=dup.selectIndex==0?Text(dup.saveText):Text(dup.cancelText);}
            else if(confirm!=null){prompt=Text(confirm.descriptionText);selected=confirm.selectIndex==0?Text(confirm.saveText):Text(confirm.cancelText);}
            else continue;
            if(prompt.Length==0&&selected.Length==0)continue;
            string owner=w.GetInstanceID()+":"+prompt;string signature=owner+":"+selected;
            active=true;repeat=prompt+(selected.Length>0?"，"+selected:"");
            if(signature!=key){key=signature;string speech=tracker.Say(owner,prompt,selected);Speech.Speak(speech);Monitor.Record("settings-popup-speech",new{text=speech,fullText=repeat,windowPath=Monitor.PathOf(w.transform)});}
            return true;
        }
        if(active){active=false;key=repeat="";tracker.Reset();}
        return false;
    }
}
internal static class NativeWindowAudit
{
    private static readonly Dictionary<int,(string signature,string path)> states=new();
    public static void Tick()
    {
        var seen=new HashSet<int>();
        foreach(var w in UnityEngine.Object.FindObjectsOfType<SettingWindowBase>()){
            if(!w)continue;
            Observe(w.GetInstanceID(),Monitor.PathOf(w.transform),!w.IsClosed()&&w.gameObject.activeInHierarchy,seen);
        }
        foreach(var w in UnityEngine.Object.FindObjectsOfType<Px.SaveData.ConfirmationWindow>()){
            if(!w)continue;
            Observe(w.GetInstanceID(),Monitor.PathOf(w.transform),!w.IsClosed()&&w.gameObject.activeInHierarchy,seen);
        }
        foreach(int id in states.Keys.Where(id=>!seen.Contains(id)).ToArray()){
            var old=states[id];Monitor.Record("native-window-state",new{id,windowPath=old.path,open=false,reason="inactive-or-destroyed"});states.Remove(id);
        }
    }
    private static void Observe(int id,string path,bool open,HashSet<int> seen)
    {
        seen.Add(id);string signature=path+":"+open;
        if(states.TryGetValue(id,out var old)&&old.signature==signature)return;
        if(states.Count>128)states.Clear();states[id]=(signature,path);
        Monitor.Record("native-window-state",new{id,windowPath=path,open,reason="native-IsClosed-and-active-hierarchy"});
    }
}

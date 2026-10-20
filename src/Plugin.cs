using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using Il2CppInterop.Runtime.Injection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace PataponAccess;

[BepInPlugin("org.patapon.access.start", "Patapon Start Screen Access", "0.6.49")]
public class Plugin : BasePlugin
{
    private Reader? reader;
    public override void Load()
    {
        Reader.Log = Log;
        CueAudio.Initialize(Config);
        var native = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "nvdaControllerClient64.dll");
        Speech.Initialize(native);
        AttackCue.Initialize(Config.Bind("Audio", "AttackReadyCue", false, "Play a short sound when visible text says 攻擊預備.").Value);
        DialogueAccess.QuietCampChatter=Config.Bind("Speech", "QuietCampChatter", true, "Silence automatic villager idle/stroll speech; preserve appeal messages and other dialogue. Also deduplicate seven known P1 chatter lines for two minutes.").Value;
        BattleChants.Configure(Config.Bind("Speech","ReadBattleChants",false,"Read confirmed repeating player march/attack chants. Story, NPC dialogue and tactical warnings remain readable. Toggle in F1 help."));
        DrumPuzzleAccess.Configure(Config.Bind("Speech","DrumPuzzleReadKeys",false,"Read puzzle keyboard bindings instead of drum names. Toggle in F1 help."));
        DialogueAccess.Install();
        CommonUi.Install();
        StructuredUi.Install();
        bool diagnostics=Config.Bind("Diagnostics", "Enabled", true, "Record local UI and message events for accessibility development.").Value;
        Monitor.Initialize(diagnostics, Config.Bind("Diagnostics", "AutoCleanLogs", true, "Clean old accessibility event logs on startup.").Value, Config.Bind("Diagnostics", "LogRetentionDays", 7, "Keep event logs for this many days (1-3650).").Value, Config.Bind("Diagnostics", "LogMaxTotalMB", 200, "Startup event log size limit in MiB (50-10240); current session is additional.").Value);
        HeroSongVolume.Initialize(Config);
        MiracleAccess.Install(Config);
        BattleMonitor.Install(diagnostics && Config.Bind("Diagnostics", "Battle", true, "Record P1/P2 scalar attack checks, collision call counts and weather requests without reading native object fields.").Value);
        RangeMonitor.Install(diagnostics,Config.Bind("Audio", "EnemyRangeCue", true, "Experimental cue on rising P1/P2 long enemy sight flag; does not indicate hit.").Value);
        OpeningAccess.Install();
        NameInputAccess.Install();
        RewardAccess.Install();
        EnemyRangeGeometry.Install();
        BossAttackCue.Install(Config.Bind("Audio","BossAttackCue",true,"Experimental P1 boss script preparation cue with verified startup fallbacks.").Value);
        P2BossAccess.Install();
        CannonWarning.InstallTrace(diagnostics);
        SceneMechanismLogger.Install(diagnostics);
        DrumPuzzleAccess.Install();
        P1TotemAccess.Install();
        P2TreasureAccess.Install();
        P2EvolutionAccess.Install();
        if(diagnostics)P2NavigationLogger.Install();
        P2CampNavigation.Install();
        SpecialBossAccess.Install();
        CartRange.Install(Config.Bind("Audio","CartRangeCue",true,"Experimental P1 wagon-specific sight cue with distinct chimes; no hit guarantee.").Value);
        VegetableCue.Install(Config.Bind("Audio","VegetableCatchCue",true,"Experimental P1/P2 vegetable countdown cue; does not press keys.").Value,Config.Bind("Audio","VegetableLeadFrames",12,"Experimental lead in native item update frames, 1-35; not a verified judgement time.").Value);
        reader=AddComponent<Reader>();
        Log.LogInfo("Start screen accessibility loaded. F1: help, F2: repeat, F3: visible text, F4: stop.");
    }
}

internal static class Speech
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    private delegate int SayDelegate([MarshalAs(UnmanagedType.LPWStr)] string text);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StatusDelegate();
    private static SayDelegate? say;
    private static StatusDelegate? stop;
    private static StatusDelegate? running;
    public static string Last = "";
    public static void Initialize(string path)
    {
        try {
            var handle = NativeLibrary.Load(path);
            say = Marshal.GetDelegateForFunctionPointer<SayDelegate>(NativeLibrary.GetExport(handle,"nvdaController_speakText"));
            stop = Marshal.GetDelegateForFunctionPointer<StatusDelegate>(NativeLibrary.GetExport(handle,"nvdaController_cancelSpeech"));
            running = Marshal.GetDelegateForFunctionPointer<StatusDelegate>(NativeLibrary.GetExport(handle,"nvdaController_testIfRunning"));
            Reader.Log.LogInfo("NVDA controller loaded: " + path + "; testIfRunning=" + running());
        } catch(Exception e) { Reader.Log.LogError("NVDA initialization: " + e.Message); }
    }
    public static void Speak(string text)
    {
        text = Clean(text);
        if(string.IsNullOrWhiteSpace(text)) return;
        Last = text;
        Reader.Log.LogInfo("SPEAK: " + text);
        if(running?.Invoke() == 0) { stop?.Invoke(); var result=say?.Invoke(text); if(result!=0) Reader.Log.LogWarning("NVDA speech result: "+result); }
        else Reader.Log.LogWarning("NVDA is not running or cannot be reached.");
    }
    public static void Stop() => stop?.Invoke();
    public static string Clean(string text) => Regex.Replace(Regex.Replace(text ?? "", @"<[^>]*>", " "), @"\s+", " ").Trim();
}

public class Reader : MonoBehaviour
{
    public Reader(IntPtr pointer) : base(pointer) { }
    public void OnApplicationQuit()=>Monitor.Finish();
    internal static ManualLogSource Log = null!;
    private float nextPoll;
    private int focusId;
    private string focusText = "";
    private string pending = "";
    private float pendingAt;
    private bool announced;
    private string context = "";
    private LauncherMain? launcher;
    private bool firstUpdate;
    private string titleFocus="";
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    private readonly bool[] keyDown = new bool[7];

    public void Update()
    {
        try {
            if(!firstUpdate) { firstUpdate=true; Log.LogInfo("Reader.Update is active."); Monitor.Record("reader-update",new{active=true}); }
            Monitor.Tick();
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            if(pid != Environment.ProcessId) return;
            if(!announced){announced=true;Speech.Speak("模組載入完成。需要操作說明或音效試聽，請按 F1 查看模組幫助。");nextPoll=Time.unscaledTime+2.5f;}
            for(int i=0;i<7;i++) {
                bool down=(GetAsyncKeyState(0x70+i)&0x8000)!=0;
                if(down && !keyDown[i]) {
                    if(i==0){pending="";DialogueAccess.Clear();CommonUi.Clear();ModuleHelp.Toggle();keyDown[i]=down;continue;}
                    else if(ModuleHelp.Active){ModuleHelp.Handle(i);keyDown[i]=down;continue;}
                    if(!ModuleHelp.Active && i==1) Speech.Speak(DrumPuzzleAccess.RepeatText.Length>0?DrumPuzzleAccess.RepeatText:P2CampNavigation.RepeatText.Length>0?P2CampNavigation.RepeatText:P2EvolutionAccess.RepeatText.Length>0?P2EvolutionAccess.RepeatText:P2TreasureAccess.RepeatText.Length>0&&!CommonUi.CommandWindowActive?P2TreasureAccess.RepeatText:StructuredUi.RepeatText.Length>0?StructuredUi.RepeatText:RewardAccess.IsResultActive?RewardAccess.RepeatText:NameInputAccess.IsActive?NameInputAccess.RepeatText:OpeningAccess.IsActive?OpeningAccess.RepeatText:CommonUi.RepeatText.Length>0?CommonUi.RepeatText:string.IsNullOrEmpty(focusText)?Speech.Last:focusText);
                    if(!ModuleHelp.Active && i==2) Speech.Speak(VisibleText());
                    if(!ModuleHelp.Active && i==3) { pending=""; DialogueAccess.Clear(); CommonUi.Clear(); Speech.Stop(); }
                    if(!ModuleHelp.Active && i==5) AttackCue.Test();
                    if(!ModuleHelp.Active && i==6) Speech.Speak(RangeMonitor.CurrentStatus);
                }
                keyDown[i]=down;
            }
            if(ModuleHelp.Active){ModuleHelp.Tick();return;}
            if(Time.unscaledTime < nextPoll) return;
            nextPoll=Time.unscaledTime+0.1f;
            if(StructuredUi.Scan()){pending="";return;}
            if(NameInputAccess.Scan()) {pending="";return;}
            MiracleAccess.Tick();
            DrumPuzzleAccess.Tick();
            AttackCue.Scan();
            RangeCue.Scan();
            CartRange.Scan();
            if(RewardAccess.Scan()) {pending="";return;}
            CommonUi.Scan();
            string dialogue=DialogueAccess.Take();
            if(dialogue.Length>0) {pending="";focusText="";Speech.Speak(dialogue);}
            string common=CommonUi.Take();
            if(common.Length>0 && dialogue.Length==0) {pending="";focusText=common;Speech.Speak(common);}
            if(OpeningAccess.IsActive)return;
            if(TitleAccess.IsActive) {
                string titleText=TitleAccess.Current;
                if(titleText!=titleFocus) {titleFocus=titleText;focusText=titleText;pending=titleText;pendingAt=Time.unscaledTime+0.18f;}
                if(pending.Length>0 && Time.unscaledTime>=pendingAt) {var message=pending;pending="";Speech.Speak(message);}
                return;
            }
            titleFocus="";
            if(!launcher) launcher=UnityEngine.Object.FindObjectOfType<LauncherMain>();
            string currentContext=launcher ? GetContext() : "";
            var selected=EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            selected=SettingsAccess.Resolve(selected);
            if(!selected && !launcher) {focusId=0;context="";return;}
            int id=selected ? selected!.GetInstanceID() : 0;
            string text=selected ? Describe(selected!) : "";
            if(!announced && launcher) {
                announced=true;
                Speech.Speak("模組載入完成。需要操作說明或音效試聽，請按 F1 查看模組幫助。");
            }
            bool changedContext=currentContext!=context;
            if(changedContext || id!=focusId || text!=focusText) {
                context=currentContext; focusId=id; focusText=text;
                pending=string.Join("，",new[]{changedContext?context:"",text}.Where(s=>!string.IsNullOrEmpty(s)));
                pendingAt=Time.unscaledTime+0.18f;
                Log.LogInfo($"FOCUS {id}: {selected?.name} => {text}; context={context}");
                Monitor.Record("speech-focus", new { id, path=selected ? Monitor.PathOf(selected!.transform) : "", text, context });
            }
            if(pending.Length>0 && Time.unscaledTime>=pendingAt) { var message=pending; pending=""; Speech.Speak(message); }
        } catch(Exception e) { nextPoll=Time.unscaledTime+2; Log.LogError(e.ToString()); }
    }
    private string GetContext()
    {
        var e=UnityEngine.Object.FindObjectOfType<EndUserLicenseAgreementWindow>();
        if(e && e!.gameObject.activeInHierarchy && !e.IsClosed()) return "使用者授權合約。內文為圖片，本版尚未提供完整內文朗讀";
        var p=UnityEngine.Object.FindObjectOfType<PrivacyPolicyAgreementWindow>();
        if(p && p!.gameObject.activeInHierarchy && !p.IsClosed()) return "隱私權政策。內文為圖片，本版尚未提供完整內文朗讀";
        var d=UnityEngine.Object.FindObjectOfType<DataUsageAgreementWindow>();
        if(d && d!.gameObject.activeInHierarchy && !d.IsClosed()) return "資料使用同意選項";
        if(launcher && launcher!.settingMain && launcher.settingMain.gameObject.activeInHierarchy && !launcher.settingMain.IsClosed()) return "設定";
        if(launcher && launcher!.menuRoot && launcher.menuRoot.activeInHierarchy) return "開始選單";
        return "標題畫面";
    }
    private string Describe(GameObject go)
    {
        var row=go.GetComponentInParent<MenuItemBase>();
        if(row) return SettingsAccess.Describe(row!);
        string label="";
        if(launcher is not null && launcher) {
            if(Is(go,launcher.p1Button)) label="PATAPON 一代";
            else if(Is(go,launcher.p2Button)) label="PATAPON 二代";
            else if(Is(go,launcher.specialButton)) label="特別內容";
            else if(Is(go,launcher.settingButton)) label="設定";
            else if(Is(go,launcher.quitButton)) label="結束遊戲";
        }
        if(label.Length==0) label=TextOf(go);
        if(label.Length==0) label="未標示的選項，"+go.name;
        var toggle=go.GetComponent<Toggle>();
        if(toggle) return label+"，核取方塊，"+(toggle.isOn?"已勾選":"未勾選");
        var slider=go.GetComponent<Slider>();
        if(slider) return label+"，滑桿，"+slider.value.ToString("0.##");
        var scrollbar=go.GetComponent<Scrollbar>();
        if(scrollbar) return "文件捲動，"+Math.Round(scrollbar.value*100)+"%";
        var select=go.GetComponent<Selectable>();
        return label+(select ? (select.IsInteractable()?"，按鈕":"，無法使用") : "");
    }
    private static bool Is(GameObject go, Component? c) => c && go==c!.gameObject;
    private static string TextOf(GameObject go)
    {
        var text=new List<string>();
        foreach(var t in go.GetComponentsInChildren<TMP_Text>()) if(t && t.isActiveAndEnabled) text.Add(Speech.Clean(t.text));
        foreach(var t in go.GetComponentsInChildren<Text>()) if(t && t.isActiveAndEnabled) text.Add(Speech.Clean(t.text));
        return string.Join("，",text.Where(s=>s.Length>0).Distinct());
    }
    private string VisibleText()
    {
        if(P2CampNavigation.RepeatText.Length>0)return P2CampNavigation.RepeatText;
        if(StructuredUi.RepeatText.Length>0)return StructuredUi.RepeatText;
        if(RewardAccess.IsResultActive)return RewardAccess.RepeatText;
        if(NameInputAccess.IsActive)return NameInputAccess.RepeatText;
        if(OpeningAccess.IsActive)return OpeningAccess.RepeatText;
        if(TitleAccess.IsActive) return "開始選單：開始新遊戲、繼續遊戲、返回。現在選擇："+TitleAccess.Current;
        var texts=new List<string>();
        foreach(var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) if(t && t.isActiveAndEnabled && !MiracleAccess.Suppress(t)) texts.Add(Speech.Clean(t.text));
        foreach(var t in UnityEngine.Object.FindObjectsOfType<Text>()) if(t && t.isActiveAndEnabled) texts.Add(Speech.Clean(t.text));
        string result=string.Join("。",texts.Where(t=>t.Length>0).Distinct());
        return result.Length>0?result:"目前找不到可讀取的文字";
    }
}

internal static class SettingsAccess
{
    private static SettingMain? setting;
    private static SaveLoadMenu? saveMenu;
    private static string lastBinding="";
    public static GameObject? Resolve(GameObject? selected)
    {
        // Custom menus own their cursor; EventSystem may retain a stale row.
        if(!saveMenu) saveMenu=UnityEngine.Object.FindObjectOfType<SaveLoadMenu>();
        if(saveMenu && saveMenu!.saveDataListCanvas && saveMenu.saveDataListCanvas.enabled && saveMenu.saveDataListCanvas.gameObject.activeInHierarchy) {
            foreach(var row in saveMenu.GetComponentsInChildren<MenuItemBase>())
                if(row && row.isActiveAndEnabled && row.isFocus) return row.gameObject;
            if(saveMenu.CurrentMenuItem) return saveMenu.CurrentMenuItem.gameObject;
        }
        if(!setting) setting=UnityEngine.Object.FindObjectOfType<SettingMain>();
        if(setting && setting!.gameObject.activeInHierarchy && !setting.IsClosed()) {
            var tab=setting.settingTab;
            var menus=tab?tab.contents:null;
            if(tab && menus!=null && tab.currentIndex>=0 && tab.currentIndex<menus.Length) {
                var menu=menus[tab.currentIndex];
                if(menu && menu.CurrentMenuItem)return menu.CurrentMenuItem.gameObject;
            }
            foreach(var row in setting.GetComponentsInChildren<MenuItemBase>())
                if(row && row.isActiveAndEnabled && row.isFocus) return row.gameObject;
        }
        return selected && selected!.activeInHierarchy ? selected : null;
    }
    public static string Describe(MenuItemBase row)
    {
        var panel=row.gameObject.GetComponent<SaveDataPanel>();
        var fresh=row.gameObject.GetComponent<NewSavePanel>();
        if(panel || fresh) {
            string mode=saveMenu && saveMenu!.title ? Speech.Clean(saveMenu.title.text) : "存檔欄位";
            mode=mode switch {"SAVE"=>"存檔","LOAD"=>"讀取存檔","DELETE"=>"刪除存檔",_=>mode};
            string slot=saveMenu?$"第 {saveMenu!.cursorIndex+1} 格，":"";
            if(fresh) return mode+"，"+slot+"新資料";
            var detail=new List<string>{mode,slot+"已有存檔"};
            if(panel!.playerNameText) detail.Add(Speech.Clean(panel.playerNameText.text));
            if(panel.lastMissionNameText) detail.Add(Speech.Clean(panel.lastMissionNameText.text));
            if(panel.playTimeText) detail.Add(Speech.Clean(panel.playTimeText.text));
            return string.Join("，",detail.Where(s=>s.Length>0));
        }
        var parts=new List<string>();
        if(setting && setting!.settingTab) {
            var tab=setting.settingTab;
            int index=tab.currentIndex;
            if(tab.contentTitles!=null && index>=0 && index<tab.contentTitles.Length) parts.Add(tab.contentTitles[index]);
        }
        var rebind=row.gameObject.GetComponent<RebindKeyMenuItem>();
        if(rebind) {
            string action=Speech.Clean(rebind.Label);
            if(action.Length==0 && rebind.labelText) action=Speech.Clean(rebind.labelText.text);
            parts.Add(action.Length>0?action:rebind.GetAction().ToString());
            int count=rebind.keyTexts==null?0:rebind.keyTexts.Length;
            int selected=rebind.CurrentIndex;
            var keys=new List<string>();
            var rawKeys=new List<string>();
            var bindingNames=BindingNames.Read(rebind.GetAction());
            for(int i=0;i<count;i++) {
                string raw=rebind.GetKeyText(i)??"";
                rawKeys.Add(raw);
                string key=i<bindingNames.Count?bindingNames[i]:Speech.Clean(raw);
                if(key.Length==0)key=BindingNames.FromSprite(raw,rebind.GetAction().ToString());
                keys.Add($"第 {i+1} 組，"+(key.Length>0?key:"未取得按鍵名稱")+(i==selected?"，目前選取":""));
            }
            parts.Add("目前綁定，"+(keys.Count>0?string.Join("；",keys):"未取得綁定欄位"));
            string bindingKey=action+"|"+selected+"|"+string.Join("|",rawKeys);
            if(bindingKey!=lastBinding) {
                lastBinding=bindingKey;
                Monitor.Record("key-binding-focus",new{action= rebind.GetAction().ToString(),label=action,selected,keys=rawKeys,bindingNames});
            }
            if(row.IsEditing)parts.Add("編輯中");
            return string.Join("，",parts.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct());
        }
        string label=row.itemText ? Speech.Clean(row.itemText.text) : "";
        parts.Add(label.Length>0?label:row.name);
        var choice=row.gameObject.GetComponent<SelectableMenuItem>();
        var text=row.gameObject.GetComponent<LabelMenuItem>();
        var slider=row.gameObject.GetComponent<SliderMenuItem>();
        if(choice) {
            parts.Add(Speech.Clean(choice.CurrentText));
            if(choice.options!=null) parts.Add($"第 {choice.currentIndex+1} 項，共 {choice.options.Length} 項");
        } else if(text && text.contentText) parts.Add(Speech.Clean(text.contentText.text));
        else if(slider && slider.slider) parts.Add("滑桿，"+slider.slider.value.ToString("0.##"));
        else {
            foreach(var t in row.GetComponentsInChildren<TMP_Text>())
                if(t && t.isActiveAndEnabled) parts.Add(Speech.Clean(t.text));
        }
        if(row.IsEditing) parts.Add("編輯中");
        string result=string.Join("，",parts.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct());
        return result;
    }
}
























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

[BepInPlugin("org.patapon.access.start", "Patapon Start Screen Access", Plugin.ModVersion)]
public class Plugin : BasePlugin
{
    public const string ModVersion="0.6.71";
    private Reader? reader;
    public override void Load()
    {
        Reader.Log = Log;
        I18nRuntime.Initialize(Config,BepInEx.Paths.GameRootPath);
        CueAudio.Initialize(Config);
        DanceCueAccess.Initialize(Config);
        FarmingRestCue.Initialize(Config);
        var native = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location)!, "nvdaControllerClient64.dll");
        Speech.Initialize(native,Config.Bind("Speech","UseTolk",true,"Use Tolk to detect supported screen readers; direct NVDA remains the fallback.").Value,Config.Bind("Speech","WindowsVoiceFallback",false,"Allow Tolk OneCore/SAPI voices if no screen reader is available.").Value);
        AttackCue.Initialize(Config.Bind("Audio", "AttackReadyCue", false, "Play a short sound when visible text says 攻擊預備.").Value);
        DialogueAccess.QuietCampChatter=Config.Bind("Speech", "QuietCampChatter", true, "Silence automatic villager idle/stroll speech; preserve appeal messages and other dialogue. Also deduplicate seven known P1 chatter lines for two minutes.").Value;
        BattleChants.Configure(Config.Bind("Speech","ReadBattleChants",false,"Read confirmed repeating player march/attack chants. Story, NPC dialogue and tactical warnings remain readable. Toggle in F1 help."));
        DrumPuzzleAccess.Configure(Config.Bind("Speech","DrumPuzzleReadKeys",false,"Read puzzle keyboard bindings instead of drum names. Toggle in F1 help."));
        DialogueAccess.Install();
        MapWeatherAccess.Install();
        CommonUi.Install();
        StructuredUi.Install();
        bool diagnostics=Config.Bind("Diagnostics", "Enabled", true, "Record local UI and message events for accessibility development.").Value;
        Monitor.Initialize(diagnostics, Config.Bind("Diagnostics", "AutoCleanLogs", true, "Clean old accessibility event logs on startup.").Value, Config.Bind("Diagnostics", "LogRetentionDays", 7, "Keep event logs for this many days (1-3650).").Value, Config.Bind("Diagnostics", "LogMaxTotalMB", 200, "Startup event log size limit in MiB (50-10240); current session is additional.").Value);
        HeroSongVolume.Initialize(Config);
        CampHintAccess.Install();
        MiracleAccess.Install(Config);
        BattleMonitor.Install(diagnostics && Config.Bind("Diagnostics", "Battle", true, "Record P1/P2 scalar attack checks, collision call counts and weather requests without reading native object fields.").Value);
        RangeMonitor.Install(diagnostics,Config.Bind("Audio", "EnemyRangeCue", true, "Shared enter/leave cue for enemies and attackable scenery; does not indicate a hit.").Value,false);
        OpeningAccess.Install();
        NameInputAccess.Install();
        RewardAccess.Install();
        NativeTipsAccess.Install();
        NativeResultGuideAccess.Install();
        EnemyRangeGeometry.Install();
        bool bossAttackEnabled=Config.Bind("Audio","BossAttackCue",true,"Experimental P1 boss script preparation cue with verified startup fallbacks.").Value;
        BossAttackCue.Install(bossAttackEnabled);
        RushP1BossAttackCue.Install(bossAttackEnabled);
        Monitor.Record("audit-settings",new{bossAttackCue=bossAttackEnabled,quietCampChatter=DialogueAccess.QuietCampChatter,readBattleChants=BattleChants.Enabled});
        P2BossAccess.Install();
        RushP2BossAccess.Install();
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
        Log.LogInfo("Start screen accessibility loaded. F1: help, F2: context guide, F3: repeat, F4: visible text, F5: stop, F7: cue test, F8: range.");
    }
}

internal static class Speech
{
    private static readonly object gate=new();
    private static SpeechRouter? router;
    private static NvdaSpeechDriver? direct;
    private static string nativePath="";
    private static bool requestedTolk,windowsVoice,activated,closed;
    private static string initialization="not-initialized";
    public static string Last="";
    public static void Initialize(string path,bool useTolk,bool windowsFallback)
    {
        lock(gate){
            direct=new NvdaSpeechDriver(path);router=new SpeechRouter(null,direct);
            nativePath=path;requestedTolk=useTolk;windowsVoice=windowsFallback;
            initialization="Tolk="+(useTolk?"pending Unity Update":"disabled")+"; NVDA-direct="+direct.Initialization;
            Reader.Log.LogInfo("Speech output: "+initialization);
        }
    }
    public static void ReportStatus()
    {
        // Initialize COM-backed readers on Reader.Update's Unity thread, not on
        // whichever thread the plugin loader happened to use.
        lock(gate){
            if(!activated&&!closed&&direct!=null){
                activated=true;
                if(requestedTolk){
                    router?.Stop();
                    var tolk=new TolkSpeechDriver(Path.Combine(Path.GetDirectoryName(nativePath)!,"Tolk","Tolk.dll"),windowsVoice);
                    router=new SpeechRouter(tolk,direct);
                    initialization="Tolk="+tolk.Initialization+"; NVDA-direct="+direct.Initialization+"; WindowsVoiceFallback="+windowsVoice;
                    Reader.Log.LogInfo("Speech output on Unity thread: "+initialization);
                }
            }
            Monitor.Record("speech-backends",new{initialization,thread=Environment.CurrentManagedThreadId});
        }
    }
    public static void Speak(string text,bool interrupt=true)
    {
        text=Clean(text);if(string.IsNullOrWhiteSpace(text))return;
        lock(gate){
            Last=text;Reader.Log.LogInfo("SPEAK: "+text);
            var result=router?.Speak(text,interrupt)??new SpeechDispatch(false,"none","not-initialized");
            CampHintAccess.Dispatched(text,result.Accepted);
            Monitor.Record("speech-dispatch",new{text,interrupt,mode=interrupt?"immediate":"queued",backend=result.Backend,accepted=result.Accepted,result=result.Result,reason=result.Reason,meaning="API acceptance only; not proof of completed playback"});
            if(!result.Accepted)Reader.Log.LogWarning("Speech unavailable: "+result.Reason);
        }
    }
    public static void Stop(){lock(gate){bool accepted=router?.Stop()==true;Monitor.Record("speech-stop",new{accepted});}}
    public static void Shutdown(){lock(gate){closed=true;try{router?.Dispose();Monitor.Record("speech-shutdown",new{completed=true});}catch(Exception e){Reader.Log.LogWarning("Speech shutdown: "+e.Message);Monitor.Record("speech-shutdown",new{completed=false,error=e.Message});}finally{router=null;direct=null;}}}
    public static string Clean(string text) => Regex.Replace(Regex.Replace(text ?? "", @"<[^>]*>", " "), @"\s+", " ").Trim();
}

public class Reader : MonoBehaviour
{
    public Reader(IntPtr pointer) : base(pointer) { }
    public void OnApplicationQuit(){Speech.Shutdown();Monitor.Finish();}
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
    private string titleFocus="",firstStartImage="";
    private long firstStartSeen;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    private readonly bool[] keyDown = new bool[8];

    public void Update()
    {
        try {
            I18nRuntime.Tick();
            if(!firstUpdate) { firstUpdate=true; Log.LogInfo("Reader.Update is active."); Speech.ReportStatus(); Monitor.Record("reader-update",new{active=true}); }
            Monitor.Tick();
            NativeDialogueRoles.Tick();
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            if(pid != Environment.ProcessId) return;
            if(!announced){announced=true;Speech.Speak(I18n.T("ui.9e46d77d08a5","模組載入完成。需要操作說明或音效試聽，請按 F1。需要當前畫面引導幫助，請按 F2。"));nextPoll=Time.unscaledTime+2.5f;}
            for(int i=0;i<8;i++) {
                bool down=(GetAsyncKeyState(0x70+i)&0x8000)!=0;
                if(down && !keyDown[i]) {
                    if(i==0){pending="";DialogueAccess.Clear("help-open");CommonUi.Clear();ModuleHelp.Toggle();keyDown[i]=down;continue;}
                    else if(i==1){ContextGuide.Speak();keyDown[i]=down;continue;}
                    else if(ModuleHelp.Active){ModuleHelp.Handle(i);keyDown[i]=down;continue;}
                    if(!ModuleHelp.Active && i==2) Speech.Speak(NativeRushScreenAccess.RepeatText.Length>0?NativeRushScreenAccess.RepeatText:BossRushDescriptionAccess.RepeatText.Length>0?BossRushDescriptionAccess.RepeatText:SettingsPopupAccess.RepeatText.Length>0?SettingsPopupAccess.RepeatText:AgreementImageAccess.RepeatText.Length>0?AgreementImageAccess.RepeatText:NativeTipsAccess.RepeatText.Length>0?NativeTipsAccess.RepeatText:BalloonChoiceAccess.RepeatText.Length>0?BalloonChoiceAccess.RepeatText:SettingsPopupAccess.RepeatText.Length>0?SettingsPopupAccess.RepeatText:DrumPuzzleAccess.RepeatText.Length>0?DrumPuzzleAccess.RepeatText:P2CampNavigation.RepeatText.Length>0?P2CampNavigation.RepeatText:P2EvolutionAccess.RepeatText.Length>0?P2EvolutionAccess.RepeatText:P2TreasureAccess.RepeatText.Length>0&&!CommonUi.CommandWindowActive?P2TreasureAccess.RepeatText:StructuredUi.RepeatText.Length>0?StructuredUi.RepeatText:RewardAccess.IsResultActive?RewardAccess.RepeatText:NameInputAccess.IsActive?NameInputAccess.RepeatText:OpeningAccess.IsActive?OpeningAccess.RepeatText:CommonUi.RepeatText.Length>0?CommonUi.RepeatText:string.IsNullOrEmpty(focusText)?Speech.Last:focusText);
                    if(!ModuleHelp.Active && i==3) Speech.Speak(VisibleText());
                    if(!ModuleHelp.Active && i==4) { pending=""; DialogueAccess.Clear("user-stop-F5"); AgreementImageAccess.Clear(); CommonUi.Clear(); Speech.Stop(); }
                    if(!ModuleHelp.Active && i==6) AttackCue.Test();
                    if(!ModuleHelp.Active && i==7) Speech.Speak(RangeMonitor.CurrentStatus);
                }
                keyDown[i]=down;
            }
            if(ModuleHelp.Active){ModuleHelp.Tick();return;}
            if(Time.unscaledTime < nextPoll) return;
            nextPoll=Time.unscaledTime+0.1f;
            if(SettingsPopupAccess.Scan()){pending="";return;}
            if(NativeRushScreenAccess.Scan()){pending="";return;}
            if(BossRushDescriptionAccess.Scan()){pending="";return;}
            if(AgreementImageAccess.Scan()){pending="";return;}
            if(StructuredUi.Scan()){pending="";return;}
            if(NameInputAccess.Scan()) {
                pending="";
                // The original naming question is a native dialogue, not input-field text.
                string prompt=DialogueAccess.Take();if(prompt.Length>0)Speech.Speak(prompt,interrupt:false);
                return;
            }
            if(!launcher)launcher=UnityEngine.Object.FindObjectOfType<LauncherMain>();
            var firstRoot=launcher?launcher!.titleFirstStartTelopRoot:null;
            string firstText=firstRoot&&firstRoot!.activeInHierarchy?TextOf(firstRoot):"";
            // The visible prompt may live outside the launcher root after a scene/layout change.
            // Only a source-image-verified sprite is eligible; never infer a prompt from a model name.
            if(firstText.Length==0)foreach(var image in UnityEngine.Object.FindObjectsOfType<Image>()){
                string verified=AgreementImageAccess.ButtonImage(image);
                if(verified=="PRESS ANY BUTTON"){firstText=verified;break;}
            }
            if(firstText.Length>0){
                firstStartSeen=Environment.TickCount64;focusText=firstText;
                if(firstText!=firstStartImage){firstStartImage=firstText;Speech.Speak(firstText);Monitor.Record("launcher-first-start-image",new{text=firstText,source="visible-root-native-text-or-verified-image"});}
                pending="";return;
            }
            if(Environment.TickCount64-firstStartSeen>1500)firstStartImage="";
            MiracleAccess.Tick();
            DrumPuzzleAccess.Tick();
            AttackCue.Scan();
            RangeCue.Scan();
            CartRange.Scan();
            ObjectRangeCue.Scan();
            if(RewardAccess.Scan()) {pending="";return;}
            CommonUi.Scan();
            string dialogue=DialogueAccess.Take();
            if(dialogue.Length>0) {pending="";focusText="";Speech.Speak(dialogue,interrupt:false);}
            string common=dialogue.Length==0?CommonUi.Take():"";
            if(common.Length>0 && dialogue.Length==0) {pending="";focusText=common;Speech.Speak(common,CommonUi.TakenInterrupt);}
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
                Speech.Speak(I18n.T("ui.9e46d77d08a5","模組載入完成。需要操作說明或音效試聽，請按 F1。需要當前畫面引導幫助，請按 F2。"));
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
        string specialHint=SpecialTitleHintAccess.Read();if(specialHint.Length>0)return specialHint;
        var e=UnityEngine.Object.FindObjectOfType<EndUserLicenseAgreementWindow>();
        if(e && e!.gameObject.activeInHierarchy && !e.IsClosed()) return "";
        var p=UnityEngine.Object.FindObjectOfType<PrivacyPolicyAgreementWindow>();
        if(p && p!.gameObject.activeInHierarchy && !p.IsClosed()) return "";
        var d=UnityEngine.Object.FindObjectOfType<DataUsageAgreementWindow>();
        if(d && d!.gameObject.activeInHierarchy && !d.IsClosed()) return I18n.T("ui.64cf356da31d","資料使用同意選項");
        if(launcher && launcher!.settingMain && launcher.settingMain.gameObject.activeInHierarchy && !launcher.settingMain.IsClosed()) return I18n.T("ui.0d8619aae051","設定");
        if(launcher && launcher!.menuRoot && launcher.menuRoot.activeInHierarchy) return I18n.T("ui.ad42dd3d047b","開始選單");
        return I18n.T("ui.6469ddabee38","標題畫面");
    }
    private static readonly HashSet<int> unlabelledOptions=new();
    private static readonly HashSet<string> launcherLabels=new();
    private string Describe(GameObject go)
    {
        var row=go.GetComponentInParent<MenuItemBase>();
        if(row) return SettingsAccess.Describe(row!);
        string label=TextOf(go);
        if(label.Length==0){
            var special=go.GetComponentInParent<SpecialTitle>();
            if(special&&special!.gameObject.activeInHierarchy){
                int index=Is(go,special.p1Button)?0:Is(go,special.p2Button)?1:Is(go,special.backButton)?2:-1;
                label=ImageTextCatalog.SpecialTitleButton(index);
            }
        }
        if(label.Length==0 && launcher is not null && launcher) {
            if(Is(go,launcher.p1Button)) label=I18n.T("ui.120d60cc3c42","PATAPON 1");
            else if(Is(go,launcher.p2Button)) label=I18n.T("ui.fac22c2337e1","PATAPON 2");
            else {
                string sprite=Is(go,launcher.specialButton)?"title_menu_special_01":Is(go,launcher.settingButton)?"title_menu_setting_01":Is(go,launcher.quitButton)?"title_menu_quit_01":"";
                if(sprite.Length>0){
                    var manager=UnityEngine.Object.FindObjectOfType<Px.Media.Dat.LauncherLocalizeManager>();
                    string language=manager?manager!.GetLanguageTypeKey():"";
                    label=ImageTextCatalog.FocusedLauncherButton(sprite,language);
                    if(label.Length>0&&launcherLabels.Add(sprite+":"+language+":"+label))Monitor.Record("launcher-focused-image-label",new{sprite,language,text=label,source="native focused button identity and visually checked game artwork"});
                }
            }
        }
        if(label.Length==0) label=TextOf(go);
        if(label.Length==0){
            if(unlabelledOptions.Count>128)unlabelledOptions.Clear();
            if(unlabelledOptions.Add(go.GetInstanceID()))Monitor.Record("unlabelled-option",new{path=Monitor.PathOf(go.transform),images=go.GetComponentsInChildren<Image>().Where(i=>i&&i.sprite).Select(i=>new{sprite=i.sprite.name,width=i.sprite.rect.width,height=i.sprite.rect.height}).ToArray(),reason="no native visible label or verified image transcription matched"});
            label=I18n.T("ui.19317f59e5a9","未標示的選項，").TrimEnd('，',',',' ');
        }
        var toggle=go.GetComponent<Toggle>();
        if(toggle) return label+I18n.T("ui.cf9551d9635b","，核取方塊，")+(toggle.isOn?I18n.T("ui.7990b4e6847e","已勾選"):I18n.T("ui.a2d59805e3b3","未勾選"));
        var slider=go.GetComponent<Slider>();
        if(slider) return label+I18n.T("ui.95a8d6e9711b","，滑桿，")+slider.value.ToString("0.##");
        var scrollbar=go.GetComponent<Scrollbar>();
        if(scrollbar) return I18n.T("ui.f870885ac81f","文件捲動，")+Math.Round(scrollbar.value*100)+"%";
        var select=go.GetComponent<Selectable>();
        return label+(select ? (select.IsInteractable()?I18n.T("ui.f9afcd60b0b0","，按鈕"):I18n.T("ui.90d8b88f33b8","，無法使用")) : "");
    }
    private static bool Is(GameObject go, Component? c) => c && go==c!.gameObject;
    private static string TextOf(GameObject go)
    {
        var text=new List<string>();var captureIds=VisibleUiText.TipsCaptureTextIds();
        foreach(var t in go.GetComponentsInChildren<TMP_Text>()) if(t) text.Add(VisibleUiText.Read(t,captureIds));
        foreach(var t in go.GetComponentsInChildren<Text>()) if(t) text.Add(VisibleUiText.Read(t));
        if(text.All(s=>s.Length==0))foreach(var image in go.GetComponentsInChildren<Image>())text.Add(AgreementImageAccess.ButtonImage(image));
        return string.Join("，",text.Where(s=>s.Length>0).Distinct());
    }
    private string VisibleText()
    {
        // Prefer text owned by a known foreground screen over the shared font pool.
        var snapshots=new (string source,string text)[]{
            ("balloon-choice",BalloonChoiceAccess.RepeatText),
            ("settings-popup",SettingsPopupAccess.RepeatText),
            ("agreement-image",AgreementImageAccess.RepeatText),
            ("confirmation",StructuredUi.ContextKind is "save-confirm" or "gift-confirm"?StructuredUi.RepeatText:""),
            ("focused-command-window",CommonUi.WindowRepeatText),
            ("tips",NativeTipsAccess.RepeatText),
            ("treasure-selection",P2TreasureAccess.RepeatText),
            ("p2-equipment",P2CampNavigation.RepeatText),
            ("equipment",ItemAndPlaceAccess.EquipmentRepeatText),
            ("inventory",ItemAndPlaceAccess.ItemRepeatText),
            ("structured-screen",StructuredUi.RepeatText),
            ("result",RewardAccess.IsResultActive?RewardAccess.RepeatText:""),
            ("name-entry",NameInputAccess.IsActive?NameInputAccess.RepeatText:""),
            ("opening",OpeningAccess.IsActive?OpeningAccess.RepeatText:""),
            ("title",TitleAccess.IsActive?TitleAccess.Current:"")
        };
        foreach(var snapshot in snapshots)if(snapshot.text.Length>0){Monitor.Record("screen-readout",new{source=snapshot.source,text=snapshot.text});return snapshot.text;}
        var texts=new List<string>();var captureIds=VisibleUiText.TipsCaptureTextIds();
        foreach(var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>()) if(t&&!MiracleAccess.Suppress(t)) texts.Add(VisibleUiText.Read(t,captureIds));
        foreach(var t in UnityEngine.Object.FindObjectsOfType<Text>()) if(t) texts.Add(VisibleUiText.Read(t));
        foreach(var picture in UnityEngine.Object.FindObjectsOfType<Image>())texts.Add(AgreementImageAccess.ButtonImage(picture));
        string result=string.Join("。",texts.Where(t=>t.Length>0).Distinct());
        Monitor.Record("screen-readout",new{source="visibility-filtered-fallback",text=result});
        return result.Length>0?result:I18n.T("ui.e30eecc6c0bb","目前找不到可讀取的文字");
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
            string mode=saveMenu && saveMenu!.title ? Speech.Clean(saveMenu.title.text) : I18n.T("ui.2f4fa933d1b1","存檔欄位");
            string slot=saveMenu?I18n.T("ui.590977803a0e","第 {0} 格，",saveMenu!.cursorIndex+1):"";
            if(fresh) {
                var native=new List<string>();
                foreach(var t in row.GetComponentsInChildren<TMP_Text>())native.Add(VisibleUiText.Read(t));
                foreach(var t in row.GetComponentsInChildren<Text>())native.Add(VisibleUiText.Read(t));
                return string.Join("，",new[]{mode,slot,string.Join("，",native.Where(t=>t.Length>0).Distinct())}.Where(t=>t.Length>0));
            }
            var detail=new List<string>{mode,slot+I18n.T("ui.aa3c79dc6ac6","已有存檔")};
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
            if(action.Length>0)parts.Add(action);
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
                keys.Add(I18n.T("ui.a1a9bf40e288","第 {0} 組，",i+1)+(key.Length>0?key:I18n.T("ui.bf40c3ac4135","未取得按鍵名稱"))+(i==selected?I18n.T("ui.7f16d4fdd7dd","，目前選取"):""));
            }
            parts.Add(I18n.T("ui.921b915c5e98","目前綁定，")+(keys.Count>0?string.Join("；",keys):I18n.T("ui.3b769bda35a7","未取得綁定欄位")));
            string bindingKey=action+"|"+selected+"|"+string.Join("|",rawKeys);
            if(bindingKey!=lastBinding) {
                lastBinding=bindingKey;
                Monitor.Record("key-binding-focus",new{action= rebind.GetAction().ToString(),label=action,selected,keys=rawKeys,bindingNames});
            }
            if(row.IsEditing)parts.Add(I18n.T("ui.730c0648427f","編輯中"));
            return string.Join("，",parts.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct());
        }
        string label=row.itemText ? Speech.Clean(row.itemText.text) : "";
        if(label.Length>0)parts.Add(label); // Object names are query/log metadata, never labels.
        var choice=row.gameObject.GetComponent<SelectableMenuItem>();
        var text=row.gameObject.GetComponent<LabelMenuItem>();
        var slider=row.gameObject.GetComponent<SliderMenuItem>();
        if(choice) {
            parts.Add(Speech.Clean(choice.CurrentText));
            if(choice.options!=null) parts.Add(I18n.T("ui.73f1855f451e","第 {0} 項，共 {1} 項",choice.currentIndex+1,choice.options.Length));
        } else if(text && text.contentText) parts.Add(Speech.Clean(text.contentText.text));
        else if(slider && slider.slider) parts.Add(I18n.T("ui.a2b83904bfbf","滑桿，")+slider.slider.value.ToString("0.##"));
        else {
            foreach(var t in row.GetComponentsInChildren<TMP_Text>())
                if(t) parts.Add(VisibleUiText.Read(t));
        }
        if(row.IsEditing) parts.Add(I18n.T("ui.730c0648427f","編輯中"));
        string result=string.Join("，",parts.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct());
        return result;
    }
}

























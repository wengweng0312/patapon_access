using BepInEx.Configuration;
namespace PataponAccess;
internal static class I18nRuntime
{
    private static ConfigEntry<string>? language;
    private static string activeMode="auto";
    private static long nextProbe,nextError;
    private static bool detected;
    private static string ModeName(string mode)=>mode=="auto"?I18n.T("language.auto","自動跟隨遊戲語言"):I18n.LanguageName(mode);
    public static string HelpLabel=>"Language / 語言 / 言語："+ModeName(language?.Value??activeMode)+(language?.Value=="auto"?" ("+I18n.LanguageName(I18n.Language)+")":"");
    public static void Initialize(ConfigFile config,string? gameRoot=null)
    {
        language=config.Bind("Speech","Language","auto",new ConfigDescription("Mod interface language: auto follows the game; unsupported game languages use English. Game settings and original dialogue are unchanged. Restart after changing this option.",new AcceptableValueList<string>(LanguageSelection.Modes)));
        activeMode=LanguageSelection.NormalizeMode(language.Value);
        detected=false;nextProbe=0;
        if(activeMode!="auto")I18n.SetLanguage(activeMode);
        else {
            var steam=SteamLanguageSource.Read(gameRoot);
            I18n.SetLanguage(steam?.language??SteamLanguageSource.Fallback);
            Reader.Log.LogInfo("Initial mod language: "+I18n.Language+"; source="+(steam?.source??"Traditional Chinese fallback; Steam language unavailable"));
        }
        I18n.Missing+=key=>{Reader.Log.LogWarning("Missing mod translation: "+key);Monitor.Record("localization-missing",new{key});};
        // Never detour localization setters. Their tiny IL2CPP implementations
        // may share native entry points; 0.6.61 crashed in that trampoline.
        // Probe only from Reader.Update, after Unity has started.
    }
    private static void Observe(string native,string source)
    {
        string? resolved=LanguageSelection.Follow(activeMode,native);
        if(resolved==null)return;
        bool changed=I18n.Language!=resolved;
        I18n.SetLanguage(resolved);
        if(!detected||changed)Monitor.Record("mod-language-follow",new{source,native,language=resolved});
        detected=true;
    }
    public static void Tick()
    {
        if(activeMode!="auto"||Environment.TickCount64<nextProbe)return;
        nextProbe=Environment.TickCount64+1000;
        try {
            var p1=P1.LaboCommon.pLaboCommonInstance_g;
            var p2=P2.LaboCommon.pLaboCommonInstance_g;
            // Interop field getters read data directly, with no game method invocation.
            var manager1=p1?.localizeManager_;
            string? a=manager1?.msgPacArray_ is { Length: >0 }?manager1.languageType_.ToString():null;
            string? b=p2?.isSetupLocalizeResource_==true?p2.localizeManager_?.languageType_.ToString():null;
            // Do not guess between two resident game engines with different settings.
            if(a!=null&&(b==null||a==b))Observe(a,"P1/localization-field");
            else if(a==null&&b!=null)Observe(b,"P2/localization-field");
        }catch(Exception e){
            if(Environment.TickCount64>=nextError){nextError=Environment.TickCount64+30000;Monitor.Record("mod-language-read-unavailable",new{error=e.Message});}
        }
    }
    public static string Cycle()
    {
        if(language==null)return I18n.LanguageName(I18n.Language);
        language.Value=LanguageSelection.Next(language.Value);
        string announcementLanguage=language.Value=="auto"?I18n.Language:language.Value;
        return I18n.In(announcementLanguage,"language.restart","Language: {0}. Restart the game to apply.",ModeName(language.Value));
    }
}

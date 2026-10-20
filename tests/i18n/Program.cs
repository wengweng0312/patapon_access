using PataponAccess;
using System.Text.Json;
using System.Text.RegularExpressions;
int checks=0;
void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
var catalogs=Directory.GetFiles("work/mod/locales","*.json").ToDictionary(p=>Path.GetFileNameWithoutExtension(p),p=>JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(p))!);
var source=catalogs["zh-TW"];
string Slots(string value)=>string.Join(",",Regex.Matches(value,@"\{\d+(?:[^}]*)\}").Select(m=>m.Value).Order());
foreach(var lang in I18n.Languages){
 var catalog=catalogs[lang];Check(catalog.Count==source.Count,lang+" key count");
 foreach(var entry in source){Check(catalog.TryGetValue(entry.Key,out var value)&&!string.IsNullOrWhiteSpace(value),lang+" "+entry.Key);Check(Slots(value!)==Slots(entry.Value),lang+" placeholders "+entry.Key);}
 I18n.SetLanguage(lang);
 Check(I18n.T("opening.unavailable","fallback")==catalog["opening.unavailable"],lang+" embedded catalog");
 const string native="神明の契約書。吾以抵達世界的盡頭為目標";
 Check(I18n.T("test.native.argument","{0}",native)==native,lang+" native argument unchanged");
}
I18n.SetLanguage("bad");Check(I18n.Language=="zh-TW","unknown locale fallback");
Console.WriteLine($"{checks} localization checks passed. No gameplay test.");

foreach(var pair in new[]{("LanguageType_tc","zh-TW"),("LanguageType_sc","zh-CN"),("LanguageType_us","en"),("LanguageType_jp","ja"),("LanguageType_kr","en"),("LanguageType_fr","en")})Check(LanguageSelection.FromGame(pair.Item1)==pair.Item2,"native game mapping "+pair.Item1);
Check(LanguageSelection.FromGame("LanguageType_Current")==null,"unresolved native language does not overwrite locale");
Check(LanguageSelection.FromGame("LanguageType_Max")==null,"sentinel does not overwrite locale");
Check(LanguageSelection.Next("ja")=="auto","auto is selectable after Japanese");
Check(LanguageSelection.Next("auto")=="zh-TW","manual choice remains available");
Console.WriteLine($"{checks} total checks passed including automatic language selection.");
Check(LanguageSelection.Follow("en","LanguageType_jp")==null,"explicit English is never overwritten by Japanese game setting");
Check(LanguageSelection.Follow("auto","LanguageType_jp")=="ja","auto follows Japanese game setting");
Console.WriteLine($"Final: {checks} checks passed.");
void Probe(){typeof(I18nRuntime).GetField("nextProbe",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,0L);I18nRuntime.Tick();}
I18n.SetLanguage("zh-TW");
I18nRuntime.Initialize(new BepInEx.Configuration.ConfigFile());
P2.LaboCommon.pLaboCommonInstance_g=new(){isSetupLocalizeResource_=false,localizeManager_=new(){languageType_=P2.Localize.LanguageType.LanguageType_jp}};
Probe();Check(I18n.Language=="zh-TW","unready game localization is not read");
P2.LaboCommon.pLaboCommonInstance_g.isSetupLocalizeResource_=true;
Probe();Check(I18n.Language=="ja","ready P2 field selects Japanese with no native method calls");
P2.LaboCommon.pLaboCommonInstance_g.localizeManager_!.languageType_=P2.Localize.LanguageType.LanguageType_us;
Probe();Check(I18n.Language=="en","game language change is followed on subsequent read");
P1.LaboCommon.pLaboCommonInstance_g=new(){isSetupLocalizeResource_=true,localizeManager_=new(){languageType_=P1.Localize.LanguageType.LanguageType_tc,msgPacArray_=new[]{Array.Empty<byte>()}}};
Probe();Check(I18n.Language=="en","conflicting engines preserve current language");
P2.LaboCommon.pLaboCommonInstance_g=null;
Probe();Check(I18n.Language=="zh-TW","ready P1 field selects Traditional Chinese");
I18nRuntime.Initialize(new BepInEx.Configuration.ConfigFile{Mode="ja"});
Probe();Check(I18n.Language=="ja","manual Japanese is not replaced by game Traditional Chinese");
Console.WriteLine($"Final with runtime doubles: {checks} checks passed. Native gameplay remains untested.");
string Manifest(string body)=>"\"AppState\" { \"appid\" \"2383200\" "+body+" }";
string dual=Manifest("\"UserConfig\" { \"language\" \"english\" } \"MountedConfig\" { \"language\" \"tchinese\" }");
Check(SteamLanguageSource.Parse(dual)?.language=="en","Steam selected English wins over old mounted Traditional Chinese");
Check(SteamLanguageSource.Parse(dual)?.source=="Steam/UserConfig","source is selected Steam preference");
Check(SteamLanguageSource.Parse(Manifest("\"MountedConfig\" { \"language\" \"japanese\" }"))?.language=="ja","mounted fallback used only without selected preference");
Check(SteamLanguageSource.Parse(Manifest("\"UserConfig\" { \"language\" \"unknown\" } \"MountedConfig\" { \"language\" \"english\" }"))==null,"unknown selected preference does not select stale mounted English");
Check(SteamLanguageSource.Parse("\"AppState\" { \"appid\" \"123\" \"UserConfig\" { \"language\" \"english\" }}")==null,"other Steam app ignored");
Check(SteamLanguageSource.Parse("\"AppState\" { \"appid\" \"2383200\" \"UserConfig\" {")==null,"truncated manifest falls back");
Check(SteamLanguageSource.Parse(Manifest("// comment\n \"UserConfig\" { \"language\" \"schinese\" }"))?.language=="zh-CN","Valve comments and nested section parsed");
string fixture=Path.GetFullPath("work/i18n-tests/fixture/steamapps");
Directory.CreateDirectory(Path.Combine(fixture,"common","PATAPON12_REPLAY"));
File.WriteAllText(Path.Combine(fixture,"appmanifest_2383200.acf"),dual);
P1.LaboCommon.pLaboCommonInstance_g=null;P2.LaboCommon.pLaboCommonInstance_g=null;
I18nRuntime.Initialize(new BepInEx.Configuration.ConfigFile(),Path.Combine(fixture,"common","PATAPON12_REPLAY"));
Check(I18n.Language=="en","Steam English selected synchronously during plugin initialization");
Check(I18n.T("ui.9e46d77d08a5","模組載入完成").StartsWith("Mod"),"loading announcement is English before first Unity Update");
I18nRuntime.Initialize(new BepInEx.Configuration.ConfigFile(),"work/nonexistent-game");
Check(I18n.Language=="zh-TW","missing Steam language falls back to Traditional Chinese");
I18nRuntime.Initialize(new BepInEx.Configuration.ConfigFile{Mode="ja"},Path.Combine(fixture,"common","PATAPON12_REPLAY"));
Check(I18n.Language=="ja","manual Japanese wins over Steam English");
Console.WriteLine($"Final Steam startup checks: {checks} passed.");

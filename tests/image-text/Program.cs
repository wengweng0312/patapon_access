using PataponAccess;
using System.Text.Json;
int count=0;void Check(bool b,string label){if(!b)throw new Exception(label);count++;}
var asm=typeof(ImageTextCatalog).Assembly;
using var stream=asm.GetManifestResourceStream("game-text.image-transcriptions.json")!;
var pages=JsonSerializer.Deserialize<ImageTextPage[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
Check(pages.Count(p=>p.Review!="manual-image-check")==176,"all original policy images included");
foreach(var page in pages){
 Check(ImageTextCatalog.Find(page.Sprite,page.Width,page.Height)==page || ImageTextCatalog.Find(page.Sprite,page.Width,page.Height)?.Source==page.Source,"exact sprite match");
 Check(page.Lines.Length>0,"no empty transcription");
 Check(page.Lines.All(l=>l.Text.Length>0&&l.Top>=0&&l.Bottom<=1&&l.Top<=l.Bottom),"valid source line coordinates");
 Check(ImageTextCatalog.Find(page.Sprite,page.Width+1,page.Height)==null,"modified sprite size rejected");
 Check(ImageTextCatalog.VisibleLines(page,0,1).Count()==page.Lines.Length,"full visible page retains lines");
}
Check(ImageTextCatalog.Find("unknown",2048,1152)==null,"no guessed unknown image text");
var sample=new ImageTextPage{Sprite="page",Lines=new[]{new ImageTextLine{Text="top",Top=.1,Bottom=.2},new ImageTextLine{Text="bottom",Top=.7,Bottom=.8}}};
Check(ImageTextCatalog.VisibleLines(sample,0,.5).Single().text=="top","clipping excludes hidden lower text");
Check(ImageTextCatalog.VisibleLines(sample,.5,1).Single().text=="bottom","scroll reveals lower text");
Check(!ImageTextCatalog.VisibleLines(sample,.5,.2).Any(),"invalid range silent");
Check(!ImageTextCatalog.VisibleLines(sample,double.NaN,1).Any(),"invalid geometry silent");
foreach(var game in new[]{"P1","P2"})foreach(int lang in new[]{0,1,7,8})Check(ImageTextCatalog.TitleFallback(game,2,lang)=="BACK","verified title image back");
Check(ImageTextCatalog.TitleFallback("P1",2,null)=="","unknown game locale not guessed");
Check(ImageTextCatalog.TitleFallback("P3",2,8)=="","unknown game not guessed");
Check(ImageTextCatalog.TitleFallback("P1",1,8)=="","native text stays preferred");
Check(pages.Where(p=>p.Sprite=="title_menu_special_01").All(p=>p.Lines.Single().Text=="BOSS RUSH"),"special image literal");
Check(pages.Where(p=>p.Sprite=="title_menu_quit_01").All(p=>p.Lines.Single().Text=="QUIT"),"quit image literal");
if(args.Length>0){
 var production=System.Reflection.Assembly.LoadFrom(Path.GetFullPath(args[0]));
 using var productionStream=production.GetManifestResourceStream("game-text.image-transcriptions.json");
 Check(productionStream!=null,"actual production DLL embeds transcriptions");
 var type=production.GetType("PataponAccess.ImageTextCatalog")!;
 var find=type.GetMethod("Find",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)!;
 Check(find.Invoke(null,new object[]{"title_menu_special_01",511,102})!=null,"actual production DLL resolves verified button image");
}
Check(ImageTextCatalog.ButtonText("title_menu_special_01",800,132,511,102)=="BOSS RUSH","actual trimmed special sprite geometry");
Check(ImageTextCatalog.ButtonText("title_menu_quit_01",800,132,258,101)=="QUIT","actual trimmed quit sprite geometry");
Check(ImageTextCatalog.ButtonText("title_menu_setting_01",800,132,355,126)=="OPTION","actual TC JA setting artwork");
Check(ImageTextCatalog.ButtonText("title_menu_setting_01",800,132,427,126)=="OPTIONS","actual EN setting artwork not mod language");
Check(ImageTextCatalog.ButtonText("title_menu_setting_01",800,132,0,0)=="","ambiguous setting text is not guessed");
Check(ImageTextCatalog.ButtonText("unknown",800,132,511,102)=="","geometry alone cannot label unknown sprite");
foreach(string language in new[]{"TC","SC","JA","EN"}){
 Check(ImageTextCatalog.FocusedLauncherButton("title_menu_special_01",language)=="BOSS RUSH","focused boss rush "+language);
 Check(ImageTextCatalog.FocusedLauncherButton("title_menu_quit_01",language)=="QUIT","focused quit "+language);
 Check(ImageTextCatalog.FocusedLauncherButton("title_menu_setting_01",language)==(language=="EN"?"OPTIONS":"OPTION"),"focused settings original artwork "+language);
}
Check(ImageTextCatalog.FocusedLauncherButton("unknown","TC")=="","unrecognized button not guessed");
Check(ImageTextCatalog.FocusedLauncherButton("title_menu_setting_01","")=="","unknown native language not guessed");
Check(ImageTextCatalog.SpecialTitleButton(0)=="PATAPON 1","boss rush first native button");
Check(ImageTextCatalog.SpecialTitleButton(1)=="PATAPON 2","boss rush second native button");
Check(ImageTextCatalog.SpecialTitleButton(2)=="BACK","boss rush back artwork");
Check(ImageTextCatalog.SpecialTitleButton(-1)=="","unknown boss rush button not guessed");
Console.WriteLine($"PASS {count} image-text resource and clipping checks; not a gameplay test.");

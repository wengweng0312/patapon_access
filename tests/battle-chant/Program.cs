using PataponAccess;
using System.Text.Json;
int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
Check(BattleChantText.ShouldSuppress(false,true,true,"前進 前進 前進～！"),"confirmed flag chant suppressed when off");
Check(!BattleChantText.ShouldSuppress(true,true,true,"前進 前進 前進～！"),"switch on preserves chant");
Check(!BattleChantText.ShouldSuppress(false,true,false,"前進 前進 前進～！"),"identical text from different story source preserved");
Check(!BattleChantText.ShouldSuppress(false,false,true,"前進 前進 前進～！"),"outside mission unchanged");
string[] protectedLines={"這裡有太鼓的神明 留下的 寶物在此沈眠","請證明您具有 正確的判斷力吧","我們是負責守護 這片土地的 砲台三兄弟…","現在的攻擊很強烈 要防禦才行唷～","守住！！ 說不定很危險…","如果是CHAKACHAKA之歌的話 那就安心了！","那麼 就送你一個 藏在深處的禮物吧！","３０…５０…８０…！ 填充能量 百分之１２０！"};
foreach(var text in protectedLines)Check(!BattleChantText.ShouldSuppress(false,true,true,text),"protected: "+text);
var state=new CannonWarningState();const string intro="我們是負責守護 這片土地的 砲台三兄弟…",charge="３０…５０…８０…！ 填充能量 百分之１２０！",source="P2.Game.Window.Message.openW";
Check(!state.Observe(true,source,charge),"unrelated charging line has no cue without cannon context");
Check(!state.Observe(true,source,intro),"intro arms context without cue");
Check(!state.Observe(true,source,"攻ー擊 預備！"),"player attack chant never becomes enemy warning");
Check(state.Observe(true,source,charge),"cannon charging is warning trigger");
state.Reset();Check(!state.Observe(true,source,charge),"mission reset removes prior cannon context");
int cues=0,protectedCount=0,chantCount=0;state.Reset();
if(args.Length>0)foreach(var line in File.ReadLines(args[0])){
 JsonDocument doc;try{doc=JsonDocument.Parse(line);}catch{continue;}using(doc){var e=doc.RootElement;string kind=e.GetProperty("kind").GetString()??"";
 if(kind=="mission-start"){state.Reset();continue;}
 if(kind!="dialogue-queued")continue;var d=e.GetProperty("data");if(!d.TryGetProperty("text",out var t)||!d.TryGetProperty("source",out var s))continue;string text=t.GetString()??"",src=s.GetString()??"";
 if(state.Observe(true,src,text))cues++;
 if(protectedLines.Contains(text)){protectedCount++;Check(!BattleChantText.ShouldSuppress(false,true,true,text),"real log mission instruction retained");}
 if(BattleChantText.ShouldSuppress(false,true,true,text))chantCount++;
 }}
if(args.Length>0){Check(cues==1,"actual cannon session yields exactly one charging cue");Check(protectedCount>=8,"actual session includes and retains mission/tutorial warnings");Console.WriteLine($"Replay: {chantCount} exact chant candidates; {protectedCount} protected lines; {cues} charging cue. Runtime flag-source scope still requires live verification.");}
Console.WriteLine($"{checks} checks passed; not a gameplay test.");
string[] feedback={"什麼都沒有","你是要 進攻哪裡？","不前進嗎…？","無法 做出任何 攻擊？","找不到 戰鬥的對象！","神明大人哪！ 什麼都沒有啊！","現在的攻擊很強烈 要防禦才行唷～","守住！！ 說不定很危險…","CHAKA CHAKA 撐著點！","快逃！ 快逃！ 快逃～！！"};
foreach(string text in feedback)Check(!BattleChantText.ShouldSuppress(false,true,true,text),"action feedback retained: "+text);
foreach(string text in new[]{"打敗他們！！","給他好看♪","要守住唷！","要守住喔♪","PON PON PATA PON"}){
 Check(BattleChantText.ShouldSuppress(false,true,true,text),"confirmed player chant quiet: "+text);
 Check(!BattleChantText.ShouldSuppress(false,true,false,text),"unverified or story speaker retained: "+text);
 Check(!BattleChantText.ShouldSuppress(true,true,true,text),"toggle restores: "+text);
}

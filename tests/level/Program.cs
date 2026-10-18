using PataponAccess;
using System.Text.Json;
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
string template="～短暫的守護者多咚嘎 Lv.<N1>～/為了完成守護者的使命";
string display="<color=#4A453BFF>～短暫的守護者多咚嘎 Lv.4～\n為了完成守護者的使命</color>";
Check(template.Length>0&&display.Length>0,"real log fixtures found");
Check(DialogueText.ResolveLevel(template,display).Contains("Lv.4"),"actual boss template resolves level four");
Check(DialogueText.ResolveLevel(template,display.Replace("多咚嘎","另一隻王"))=="","different boss rejected");
Check(DialogueText.ResolveLevel(template,template)=="","unresolved token rejected");
Check(DialogueText.ResolveLevel(template,display.Replace("Lv.4","Lv.12")).Contains("Lv.12"),"multidigit level retained");
Check(!DialogueText.HasLevelToken("一般劇情對話"),"ordinary dialogue unaffected");

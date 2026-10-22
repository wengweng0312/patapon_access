using PataponAccess;
using System.Text.Json;
int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
var queue=new DialogueQueue();
Check(queue.Peek(100)==null,"empty queue is silent");
queue.Add("first","","","P1",100);queue.Add("second","","","P1",110);queue.Add("third","","","P1",120);
Check(queue.Count==3,"burst retains all sentences");
Check(queue.Peek(159)==null,"original capture debounce retained");
Check(queue.Peek(160)?.Text=="first","new sentence does not postpone earlier sentence");
var first=queue.Peek(200)!;queue.Complete(first);
Check(queue.Peek(200)?.Text=="second","FIFO after first");
queue.Complete(first);Check(queue.Count==2,"stale completion cannot remove another entry");
queue.Complete(queue.Peek(200)!);Check(queue.Peek(200)?.Text=="third","third is retained");
queue.Complete(queue.Peek(200)!);Check(queue.Count==0,"drain has no repeats");
queue.Add("name line","name template","hint-one","P2",300);queue.Add("ordinary line","","hint-two","P2",301);
var templated=queue.Peek(400)!;
Check(templated.Template=="name template"&&templated.Hint=="hint-one","entry owns template and hint");
templated.ReadyAt=450;
Check(queue.Peek(440)==null,"waiting head is not overtaken");
Check(queue.Peek(450)?.Source=="P2","entry source preserved");
templated.Template="";queue.Complete(templated);
Check(queue.Peek(450)?.Hint=="hint-two"&&queue.Peek(450)?.Template=="","later entry metadata independent");
Check(queue.Peek(100000)?.Text=="ordinary line","long foreground wait or app switch does not expire dialogue");
Check(queue.Clear()==1&&queue.Count==0&&queue.Peek(100000)==null,"F5 clears all pending sentences");
queue.Add("","","","P1",0);Check(queue.Count==0,"empty string does not block queue");
foreach(string game in new[]{"P1","P2","P1S","P2S"}){
 queue.Add(game,"","",game,100);Check(queue.Peek(200)?.Text==game,"each existing game path supported");queue.Clear();
}
string log="D:/SteamLibrary/steamapps/common/PATAPON12_REPLAY/BepInEx/accessibility-logs/20261021-211757-14700-0.jsonl";
var expected=new List<string>();long last=0;
foreach(string line in File.ReadLines(log)){
 using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
 if(root.GetProperty("kind").GetString()!="dialogue-queued")continue;
 var data=root.GetProperty("data");string text=data.GetProperty("text").GetString()??"";
 if(text.Length==0)continue;last=root.GetProperty("elapsedMs").GetInt64();expected.Add(text);
 queue.Add(text,"","",data.GetProperty("source").GetString()??"",last);
}
var actual=new List<string>();while(queue.Peek(last+100000) is {} entry){actual.Add(entry.Text);queue.Complete(entry);}
Check(expected.Count>0&&actual.SequenceEqual(expected),"recorded dialogue remains complete and ordered, without rewritten wording");
Console.WriteLine($"{checks} dialogue queue checks passed; {actual.Count} captured sentences replayed in order. Not gameplay verification.");

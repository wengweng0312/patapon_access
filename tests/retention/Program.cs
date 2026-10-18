using PataponAccess;
var root=Path.Combine(Path.GetTempPath(),"patapon-retention-"+Guid.NewGuid());
Directory.CreateDirectory(root);
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
string Make(string name,int age,long size=1){var p=Path.Combine(root,name);using(var f=File.Create(p))f.SetLength(size);File.SetLastWriteTimeUtc(p,DateTime.UtcNow.AddDays(-age));return p;}
try {
var old=Make("20260101-120000-123-0.jsonl",8);
var recent=Make("20260102-120000-123-0.jsonl",1);
var other=Make("notes.jsonl",90);
var locked=Make("20260103-120000-123-0.jsonl",9);
using(var held=new FileStream(locked,FileMode.Open,FileAccess.Write,FileShare.Read)) {
LogRetention.Clean(root,7,200,Console.WriteLine);
Check(!File.Exists(old)&&File.Exists(recent),"age cleanup preserves recent session");
Check(File.Exists(other),"unrecognized filenames preserved");
Check(File.Exists(locked),"active writer protected");
}
LogRetention.Clean(root,7,200,Console.WriteLine);
Check(!File.Exists(locked),"closed old session cleaned");
var bigOld=Make("20260104-120000-123-0.jsonl",2,30*1024*1024);
var bigNew=Make("20260105-120000-123-0.jsonl",0,30*1024*1024);
LogRetention.Clean(root,7,50,Console.WriteLine);
Check(!File.Exists(bigOld)&&File.Exists(bigNew),"capacity cleanup oldest first");
}finally{Directory.Delete(root,true);}

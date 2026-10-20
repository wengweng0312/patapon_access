using PataponAccess;
using System.Text.Json;
void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
Check(!P2RangeTargets.Inside(0,0),"empty range silent");
Check(P2RangeTargets.Inside(1,0),"enemy range retained");
Check(P2RangeTargets.Inside(0,2),"jungle object range included");
Check(P2RangeTargets.Inside(1,2),"enemy and object combine");
Check(P2RangeTargets.Inside(4,0)&&P2RangeTargets.Inside(0,4),"P2 four-squad native flags accepted");
Check(!P2RangeTargets.Inside(-1,0)&&!P2RangeTargets.Inside(0,-1),"invalid negative counts rejected");
Check(!P2RangeTargets.Inside(5,0)&&!P2RangeTargets.Inside(0,5),"out-of-bounds counts rejected");
if(args.Length>0){
 int old=0,current=0,objectOnly=0;
 foreach(string line in File.ReadLines(args[0])){
  using var doc=JsonDocument.Parse(line);var r=doc.RootElement;
  if(r.GetProperty("kind").GetString()!="range-state"||r.GetProperty("mission").GetInt32()!=3)continue;
  var d=r.GetProperty("data");int enemy=d.GetProperty("longEnemySquads").GetInt32(),obj=d.GetProperty("longGimmickSquads").GetInt32();
  if(enemy>0)old++;if(P2RangeTargets.Inside(enemy,obj))current++;if(enemy==0&&obj>0)objectOnly++;
 }
 Check(old==0&&objectOnly>0&&current==objectOnly,"actual last jungle mission object samples now eligible");
 Console.WriteLine($"Replay: old eligible={old}, new eligible={current}; no audio/gameplay simulation.");
}

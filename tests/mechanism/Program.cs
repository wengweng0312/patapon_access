using PataponAccess;
using System.Text.Json;
int count=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
Check(MechanismNames.IsDrumPuzzle("OEP_0_BODY"),"body recognized");
foreach(int slot in Enumerable.Range(1,4))foreach(string drum in new[]{"PON","PATA","CHAKA","DON"})Check(MechanismNames.IsDrumPuzzle($"OEP_{slot}_{drum}"),"glyph "+slot+" "+drum);
foreach(string? other in new string?[]{null,"","GOAL","POISONFLOWER","OEP_5_PON","OEP_1_PON_EXTRA","NPC_OEP_1_PON"})Check(!MechanismNames.IsDrumPuzzle(other),"unrelated object excluded: "+other);
if(args.Length>0){using var doc=JsonDocument.Parse(File.ReadAllText(args[0]));var items=doc.RootElement.GetProperty("items");int missions=0,swamp=0;
 var keys=items.GetProperty("m_Keys").EnumerateArray().Select(x=>x.GetString()).ToArray();int i=0;
 foreach(var stage in items.GetProperty("m_Values").EnumerateArray()){
  string? key=keys[i++];int names=stage.GetProperty("stageData").GetProperty("stageGimmickParam").GetProperty("param").EnumerateArray().Count(x=>MechanismNames.IsDrumPuzzle(x.GetProperty("name").GetString()));
  if(names>0){missions++;Check(names==17,"same body and 16 glyph objects in "+key);if(key=="missionid_10320")swamp=names;}
 }
 Check(missions==5&&swamp==17,"swamp puzzle family shares asset structure across five mission configurations");}
Console.WriteLine($"{count} checks passed; configuration presence does not prove runtime visibility or activation.");


var puzzle=new DrumPuzzleState();
void ObserveAt(long now,float flag,float front){
 puzzle.Observe("OEP_0_BODY",true,2587,-112,now);puzzle.ObservePlayer(flag,front,now);
 foreach(int slot in Enumerable.Range(1,4))foreach(string drum in new[]{"PON","PATA","CHAKA","DON"})
 puzzle.Observe($"OEP_{slot}_{drum}",true,(slot%2==1?drum=="PATA":drum=="CHAKA")?2587:-500,(slot%2==1?drum=="PATA":drum=="CHAKA")?-112:0,now);
}
ObserveAt(1000,0,150);Check(puzzle.Take(1500)=="","loaded complete glyphs far away remain silent");
// Real latest run accepted a drum at flag distance 271.80737. Front offset here
// is a simulated formation; the old run did not record that native front value.
ObserveAt(2000,2315.19263f,2495);Check(puzzle.Take(2000)=="","native front approach settles");
ObserveAt(2300,2315.19263f,2495);Check(puzzle.Take(2300)=="PATA CHAKA PATA CHAKA","front warns before old flag threshold");
Check(puzzle.FlagDistance>250&&puzzle.Distance<150,"flag and troop front remain distinct");
Check(puzzle.Take(2400)=="","stationary formation does not repeat");
ObserveAt(2500,2315,2410);Check(puzzle.CurrentAt(2500)!="","front position hysteresis");
ObserveAt(3000,2100,2250);Check(puzzle.Take(3000)==""&&puzzle.CurrentAt(3000)=="","retreat leaves approach area");
ObserveAt(4000,2315,2495);puzzle.Take(4000);ObserveAt(4300,2315,2495);Check(puzzle.Take(4300)!="","reapproach can notify");
ObserveAt(4400,2460,2580);Check(puzzle.CurrentAt(4400)==""&&puzzle.Take(4400)=="","native flag dismissal boundary never triggers late cue");
ObserveAt(5000,2700,2800);Check(puzzle.Take(5300)=="","passing pillar does not become an absolute-distance approach");
ObserveAt(6000,2315,2495);puzzle.Take(6000);ObserveAt(6300,2315,2495);puzzle.Take(6300);
Check(puzzle.CurrentAt(7000)=="","stale scalar positions become unknown");
ObserveAt(8000,2315,2495);puzzle.Take(8000);ObserveAt(8300,2315,2495);puzzle.Take(8300);
puzzle.Observe("OEP_0_BODY",false,2587,-112,8400);Check(puzzle.CurrentAt(8400)=="","disabled body is silent");
puzzle.Clear();Check(puzzle.Current==""&&puzzle.CurrentAt(8500)=="","new mission clears positions and glyphs");
ObserveAt(9000,2315,2200);Check(puzzle.Take(9300)=="","invalid front behind flag cannot generate a cue");
Console.WriteLine("Native troop-front proximity regressions passed; simulated formations, not gameplay validation.");
var otherPuzzle=new DrumPuzzleState();
otherPuzzle.Observe("OEP_0_BODY",true,9000,-50,10000);
string[] otherDrums={"PON","DON","CHAKA","PATA"};
for(int i=0;i<4;i++)otherPuzzle.Observe($"OEP_{i+1}_{otherDrums[i]}",true,9000,-50,10000);
otherPuzzle.ObservePlayer(8700,8900,10000);otherPuzzle.Take(10000);otherPuzzle.ObservePlayer(8700,8900,10300);otherPuzzle.Observe("OEP_0_BODY",true,9000,-50,10300);
Check(otherPuzzle.Take(10300)=="PON DON CHAKA PATA","different position and sequence need no boss, mission or step-count condition");

foreach(var fixture in new[]{("missionid_00_02",6u),("missionid_01_01",18u)}){
 string path=Path.Combine("work/p1-totem-assets",fixture.Item1+"-missionscript.pac");
 if(!File.Exists(path)){Console.WriteLine("SKIP private game fixture: "+fixture.Item1);continue;}
 byte[] script=File.ReadAllBytes(path);var parsed=P1TotemScript.Read(script);
 Check(parsed.Any(p=>p.LayoutId==fixture.Item2&&p.Sequence=="CHAKA PON CHAKA PON"),"P1 original script sequence and layout "+fixture.Item1);
 Check(P1TotemScript.Read(script.Take(100).ToArray()).Count==0,"truncated script cannot invent a sequence "+fixture.Item1);
 // Change native required percussion literals, demonstrating no stored answer.
 byte[] changed=(byte[])script.Clone();
 for(int i=0;i<changed.Length-20;i+=4)
  if(BitConverter.ToUInt32(changed,i)==0x00070025&&BitConverter.ToUInt32(changed,i+4)==8&&BitConverter.ToUInt32(changed,i+8)==0x7e&&BitConverter.ToUInt32(changed,i+12)==0x10&&BitConverter.ToSingle(changed,i+16)==4)
   BitConverter.GetBytes(2f).CopyTo(changed,i+16);
 Check(P1TotemScript.Read(changed).Any(p=>p.LayoutId==fixture.Item2&&p.Sequence=="PATA PON PATA PON"),"P1 script parser follows changed drum values "+fixture.Item1);
}
var p1=new DrumPuzzleState();p1.ObserveBody(true,550,-112,"CHAKA PON CHAKA PON",11000);p1.ObservePlayer(200,450,11000);p1.Take(11000);
p1.ObserveBody(true,550,-112,"CHAKA PON CHAKA PON",11300);p1.ObservePlayer(200,450,11300);
Check(p1.Take(11300)=="CHAKA PON CHAKA PON","single P1 body uses shared native position cue");
p1.ObserveBody(true,550,-112,"",11400);Check(p1.CurrentAt(11400)=="","unrecognized P1 script is silent instead of guessing");
Console.WriteLine("P1 source-script fixtures and shared proximity checks passed.");

using PataponAccess;
int count=0;
void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
byte[] pack=File.ReadAllBytes(args[0]);
foreach(var (offset,motion) in new[]{(0x1345c,8),(0x1398c,13),(0x13ec0,16),(0x1424c,19)}){
 Check(BitConverter.ToInt32(pack,offset)==0x00081625&&BitConverter.ToInt32(pack,offset+8)==motion,"real P2 preparation motion "+motion);
 // P2 setMotion parses an extra rate parameter; its continuation starts at +28.
 int address=offset+28;var header=pack.AsSpan(0,8);var next=pack.AsSpan(address,128);
 foreach(string unit in new[]{"unit201_01_01","unit208_01_01","unit242_01_01"})Check(P2BossRules.IsEntry(unit,10,0,address,header,next),unit+" phase0 startup "+motion);
 Check(!P2BossRules.IsEntry("unit201_01_01",10,1,address,header,next),"late phase excluded "+motion);
 Check(!P2BossRules.IsEntry("unit201_01_01",9,0,address,header,next),"nonattack action excluded "+motion);
 Check(!P2BossRules.IsEntry("unit211_01_01",10,0,address,header,next),"other boss cannot inherit dragon addresses "+motion);
 Check(!P2BossRules.IsEntry("unit201_01_01",10,0,address-8,header,next),"P1 argument length rejected "+motion);
}
Console.WriteLine(count+" checks passed. Not a gameplay/audio test.");

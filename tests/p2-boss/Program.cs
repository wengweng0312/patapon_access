using PataponAccess;
int count=0;
void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
if(args.Length==0){Console.WriteLine("SKIP: provide a local P2 boss script fixture path; game files are not distributed.");return;}
byte[] pack=File.ReadAllBytes(args[0]);
foreach(var (offset,motion) in new[]{(0x1345c,8),(0x1398c,13),(0x13ec0,16),(0x1424c,19)}){
 Check(BitConverter.ToInt32(pack,offset)==0x00081625&&BitConverter.ToInt32(pack,offset+8)==motion,"real P2 preparation motion "+motion);
 // P2 setMotion parses an extra rate parameter; its continuation starts at +28.
 int address=offset+28;var header=pack.AsSpan(0,8);var next=pack.AsSpan(address,128);
 foreach(string unit in new[]{"unit201_01_01","unit208_01_01","unit242_01_01"})Check(P2BossRules.IsEntry(unit,10,0,address,header,next),unit+" phase0 startup "+motion);
 Check(!P2BossRules.IsEntry("unit201_01_01",10,1,address,header,next),"late phase excluded "+motion);
 Check(!P2BossRules.IsEntry("unit201_01_01",9,0,address,header,next),"nonattack action excluded "+motion);
 Check(P2BossRules.IsEntry("unit211_01_01",10,0,address,header,next),"shared straight-entry pattern works across boss families "+motion);
 Check(!P2BossRules.IsEntry("unit111_01_01",10,0,address,header,next),"non-P2 family excluded "+motion);
 Check(!P2BossRules.IsEntry("unit201_01_01",10,0,address+2,header,next),"misaligned continuation rejected "+motion);
}
Console.WriteLine(count+" checks passed. Not a gameplay/audio test.");

foreach(var (unit,address) in new[]{("unit217_01_01",267524),("unit217_01_01",266988),("unit218_01_01",281272),("unit237_01_01",663468),("unit228_01_01",519272),("unit224_01_01",432412)}){
 var next=pack.AsSpan(address,128).ToArray();
 Check(P2BossRules.IsEntry(unit,10,0,address,pack.AsSpan(0,8),next),"reviewed shared/helper startup "+unit);
 Check(!P2BossRules.IsEntry(unit,10,1,address,pack.AsSpan(0,8),next),"later phase excluded "+unit);
 if(unit is "unit217_01_01" or "unit218_01_01" or "unit237_01_01"){
  next[24]^=1;
  Check(!P2BossRules.IsEntry(unit,10,0,address,pack.AsSpan(0,8),next),"unknown helper rejected "+unit);
 }
}
Console.WriteLine(count+" total checks passed. Requires local fixture; no game assets are distributed.");
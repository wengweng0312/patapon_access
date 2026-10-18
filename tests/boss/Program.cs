using PataponAccess;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
byte[] Words(params int[] words)=>words.SelectMany(BitConverter.GetBytes).ToArray();
byte[] header=Words(0x00021225,104);
byte[] advance=Words(0x00070025,4,0x21,0x10,0x3f800000);
Check(BossWindupState.IsEntry(10,0,header,advance),"attack entry accepted before phase advances");
foreach(float state in new[]{0,1,2,3,4,5,7,9,11})Check(!BossWindupState.IsEntry(state,0,header,advance),"nonattack action rejected: "+state);
foreach(float phase in new[]{1,2,3})Check(!BossWindupState.IsEntry(10,phase,header,advance),"attack already in progress rejected: "+phase);
Check(!BossWindupState.IsEntry(float.NaN,0,header,advance),"invalid state rejected");
Check(!BossWindupState.IsEntry(10,0,Words(0x00011225,104),advance),"different script rejected");
Check(!BossWindupState.IsEntry(10,0,header,Words(0x00070025,4,0x20,0x10,0x3f800000)),"different variable assignment rejected");
Check(!BossWindupState.IsEntry(10,0,header,Words(0x00070025,4,0x21,0x10,0)),"phase not advancing rejected");
Check(!BossWindupState.IsEntry(10,0,header,Words(0x00010025).Concat(advance).ToArray()),"does not scan past return into another action");
Check(!BossWindupState.IsEntry(10,0,header,Words(0x00030025,0x100).Concat(advance).ToArray()),"does not scan past a call");
Check(!BossWindupState.IsEntry(10,0,header,Words(0x00081625,2,18,2,0).Concat(advance).ToArray()),"does not scan past a second motion");
Check(BossWindupState.IsEntry(10,0,header,Words(0x00070025,4,0x23,0x10,0x3f800000).Concat(advance).ToArray()),"intermediate flag assignment allowed");
Check(!BossWindupState.IsEntry(10,0,header,advance[..^1]),"truncated bytecode rejected");
Check(BossWindupState.IsVerifiedFallback("unit201_01_01",16),"recent dragon startup covered");
Check(BossWindupState.IsVerifiedFallback("unit208_01_01",13),"dragon variant startup covered");
Check(!BossWindupState.IsVerifiedFallback("unit201_01_01",18),"dragon damaging motion never used as fallback");
Check(!BossWindupState.IsVerifiedFallback("unit211_01_01",16),"unrelated model cannot inherit dragon motion id");
Check(BossWindupState.IsVerifiedFallback("unit212_01_01",7)&&BossWindupState.IsVerifiedFallback("unit216_01_01",11)&&BossWindupState.IsVerifiedFallback("unit221_01_01",13),"previous verified fallbacks retained");
if(args.Length>=1) {
    var pack=File.ReadAllBytes(args[0]);
    // Local verification against installed game's extracted script, never shipped as an asset.
    foreach(int offset in new[]{0xde78,0xe2f4,0xe738,0x10fe8,0x11450,0x1180c,0x21a80,0x21da0,0x22900,0x2d0d4,0x2d974,0x2f97c,0x395e4,0x3a3c8,0x3a6a8,0x4f80c,0x4fb30,0x51878,0x5a79c,0x5b354,0x64398,0x64bec,0x65300}) {
        Check(BitConverter.ToInt32(pack,offset)==0x00081625,"real script motion header: "+offset.ToString("X"));
        Check(BossWindupState.IsEntry(10,0,pack.AsSpan(0,8),pack.AsSpan(offset+20,128)),"real script preparation advances phase: "+offset.ToString("X"));
    }
    foreach(int offset in new[]{0xe438,0xe88c,0x21ba8,0x399c4,0x4f96c,0x519d8})
        Check(!BossWindupState.IsEntry(10,1,pack.AsSpan(0,8),pack.AsSpan(offset+20,128)),"real damaging phase rejected: "+offset.ToString("X"));
}
if(args.Length==2) {
    var pack=File.ReadAllBytes(args[0]);int starts=0,late=0;
    foreach(var line in File.ReadLines(args[1])) {
        using var doc=System.Text.Json.JsonDocument.Parse(line);var root=doc.RootElement;
        if(root.GetProperty("kind").GetString()!="boss-script-motion")continue;
        var d=root.GetProperty("data");if(d.GetProperty("unit").GetString()!="unit211_01_01")continue;
        if(d.GetProperty("action").ValueKind!=System.Text.Json.JsonValueKind.Number)continue;
        // 0.6.24 logged the float interpretation of integer union fields.
        int action=BitConverter.SingleToInt32Bits(d.GetProperty("action").GetSingle());
        int phase=BitConverter.SingleToInt32Bits(d.GetProperty("phase").GetSingle());
        int address=d.GetProperty("packAddress").GetInt32();
        bool entry=BossWindupState.IsEntry(action,phase,pack.AsSpan(0,8),pack.AsSpan(address,Math.Min(128,pack.Length-address)));
        if(action==10&&phase==0){Check(entry,"recorded crab preparation detected: mission "+root.GetProperty("mission")+" motion "+d.GetProperty("motion"));starts++;}
        if(action==10&&phase>0){Check(!entry,"recorded crab later phase rejected");late++;}
    }
    Check(starts>0&&late>0,"actual crab log contains both preparation and later phases");
    Console.WriteLine($"REPLAY crab starts={starts}, later phases={late}");
}

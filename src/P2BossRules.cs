namespace PataponAccess;
internal static class P2BossRules
{
    private static readonly byte[] PhaseAdvance = {0x25,0,7,0,4,0,0,0,0x21,0,0,0,0x10,0,0,0,0,0,0x80,0x3f};
    private static readonly byte[] TimerCopy = {0x25,0,7,0,0x20,0,0,0,0x28,0,0,0,0x20,0,0,0,0x29,0,0,0};
    // The shared unitbase script uses action 10 / phase 0 for the attack cycle.
    // Confirm the next phase assignment rather than copying dragon motion IDs.
    public static bool IsEntry(string unit,float action,float phase,int address,ReadOnlySpan<byte> header,ReadOnlySpan<byte> continuation)
    {
        if(!IsFamily(unit)||address<28||(address&3)!=0)return false;
        if(IsLinearEntry(action,phase,header,continuation))return true;
        // Shark and crab families copy their travel timer and call an inspected
        // direction helper before advancing phase. Unknown calls stay rejected.
        if(action!=10||phase!=0||continuation.Length<48||!continuation.StartsWith(TimerCopy))return false;
        if(BitConverter.ToUInt32(continuation.Slice(20,4))!=0x00030025)return false;
        uint target=BitConverter.ToUInt32(continuation.Slice(24,4));
        uint expected=unit switch {"unit237_01_01"=>0xa1754,"unit217_01_01"=>265596,"unit218_01_01"=>278336,_=>0};
        if(expected==0||target!=expected||target>=address)return false;
        return IsLinearEntry(action,phase,header,continuation.Slice(28));
    }
    private static bool IsLinearEntry(float action,float phase,ReadOnlySpan<byte> header,ReadOnlySpan<byte> next)
    {
        if(action!=10||phase!=0||header.Length<8||header[0]!=0x25||header[1]!=0x12||header[2]!=2||header[3]!=0)return false;
        // Only straight local = float assignments may precede the phase advance.
        // Unknown opcodes, calls, jumps, other motions or effects fail closed.
        for(int i=0;i+20<=next.Length;i+=20){
            var command=next.Slice(i,20);
            if(command.SequenceEqual(PhaseAdvance))return true;
            if(BitConverter.ToUInt32(command)!=0x00070025||BitConverter.ToUInt32(command.Slice(4))!=4||BitConverter.ToUInt32(command.Slice(12))!=16)return false;
            uint variable=BitConverter.ToUInt32(command.Slice(8));
            if(variable is 0x20 or 0x21)return false;
        }
        return false;
    }
    private static bool IsFamily(string unit)=>unit.Length==13&&unit.StartsWith("unit2",StringComparison.Ordinal)&&unit[5]>='0'&&unit[5]<='9'&&unit[6]>='0'&&unit[6]<='9'&&unit.EndsWith("_01_01",StringComparison.Ordinal);
}

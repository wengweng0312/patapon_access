namespace PataponAccess;

// Derived from P1 unitbase.pac: local 0x20 is the action, 0x21 its phase.
// The action dispatcher selects its attack routine at 10, not at a damage frame.
internal static class BossWindupState
{
    private static readonly byte[] AdvancePhase = {0x25,0,7,0,4,0,0,0,0x21,0,0,0,0x10,0,0,0,0,0,0x80,0x3f};
    public static bool IsEntry(float action,float phase,ReadOnlySpan<byte> header,ReadOnlySpan<byte> continuation)
    {
        if(action!=10||phase!=0||header.Length<8||header[0]!=0x25||header[1]!=0x12||header[2]!=2||header[3]!=0)return false;
        // Do not accept an unrelated state machine merely because its locals equal 10/0.
        // Confirm this very motion command is followed by a phase-1 assignment,
        // before a return, jump, call or another motion can lead us elsewhere.
        for(int i=0;i+4<=continuation.Length;i+=4) {
            var next=continuation.Slice(i);
            if(next.StartsWith(AdvancePhase))return true;
            if(next[0]==0x25&&next[3]==0&&((next[1]==0&&next[2]>=1&&next[2]<=6)||(next[1]==0x16&&next[2]==8)))return false;
        }
        return false;
    }

    // Additional startup motions tied to explicitly named attack activity parameters.
    // These are not copied across unrelated model families.
    public static bool IsVerifiedFallback(string unit,int motion)=>unit switch {
        "unit201_01_01" or "unit208_01_01" => motion is 8 or 13 or 16,
        "unit204_01_01" => motion is 7 or 8,
        "unit217_01_01" => motion is 6 or 7,
        "unit218_01_01" => motion is 10 or 11,
        "unit212_01_01" => motion==7,
        "unit216_01_01" => motion is 7 or 11,
        "unit221_01_01" => motion is 7 or 13,
        _=>false
    };
}

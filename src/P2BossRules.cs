namespace PataponAccess;
internal static class P2BossRules
{
    // P2 unitbase: kinds 13/14/15 share the dragon attack dispatcher.
    // These addresses follow all THREE P2 setMotion arguments (28 bytes).
    public static bool IsEntry(string unit,float action,float phase,int address,ReadOnlySpan<byte> header,ReadOnlySpan<byte> continuation)
        =>unit is "unit201_01_01" or "unit208_01_01" or "unit242_01_01"
          &&address is 0x13478 or 0x139a8 or 0x13edc or 0x14268
          &&BossWindupState.IsEntry(action,phase,header,continuation);
}

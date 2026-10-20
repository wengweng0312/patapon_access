namespace PataponAccess;
internal static class MechanismNames
{
    public static bool IsDrumPuzzle(string? name)
        =>name=="OEP_0_BODY"||System.Text.RegularExpressions.Regex.IsMatch(name??"",@"^OEP_[1-4]_(PON|PATA|CHAKA|DON)$");
    public static string Role(string name)
    {
        if(name=="OEP_0_BODY")return "drum-puzzle-body";
        return IsDrumPuzzle(name)?"drum-puzzle-glyph":"unclassified";
    }
}

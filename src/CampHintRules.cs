namespace PataponAccess;
internal static class CampHintRules
{
    // Native P2 colony message indices, verified against the game's locale data.
    // Rain's continuation is retained together with its introductory clue.
    public static bool IsProtected(string game,uint id)=>game.StartsWith("P2",StringComparison.Ordinal)&&id is 162 or 166 or 167 or 174;
}

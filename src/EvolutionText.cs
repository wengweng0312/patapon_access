namespace PataponAccess;
internal static class EvolutionText
{
    // The game owns the wording, levels and inventory ratios. Keep them intact.
    public static string Requirements(string text)=>text;
    public static string Build(string description,string requirements,int level)
        =>string.Join("。",new[]{description,requirements}.Where(s=>!string.IsNullOrWhiteSpace(s)));
}

namespace PataponAccess;
internal static class LanguageSelection
{
    public static readonly string[] Modes={"auto","zh-TW","zh-CN","en","ja"};
    // Use native enum names, not enum ordinals or rendered game dialogue.
    public static string? FromGame(string value)=>value switch {
        "LanguageType_tc"=>"zh-TW", "LanguageType_sc"=>"zh-CN",
        "LanguageType_jp"=>"ja", "LanguageType_us"=>"en",
        "LanguageType_fr" or "LanguageType_de" or "LanguageType_it" or
        "LanguageType_sp" or "LanguageType_kr"=>"en", _=>null
    };
    public static string NormalizeMode(string? mode)=>Modes.FirstOrDefault(m=>m.Equals(mode,StringComparison.OrdinalIgnoreCase))??"auto";
    public static string? Follow(string mode,string native)=>NormalizeMode(mode)=="auto"?FromGame(native):null;
    public static string Next(string mode)=>Modes[(Array.IndexOf(Modes,NormalizeMode(mode))+1)%Modes.Length];
}

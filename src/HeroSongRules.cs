namespace PataponAccess;
internal static class HeroSongRules
{
    // Original requests emitted by P2 UnitSquad.barTiming (intro SE 7, song SE 8/9, bank 27).
    public static uint PreviewSeIndex(int variant)=>(variant%3) switch{0=>0x0770071Bu,1=>0x0780081Bu,_=>0x0790091Bu};
    public static bool IsTarget(string? game,string? bank,int wave)
    {
        if(game!="P2"&&game!="P2S")return false;
        string name=Path.GetFileNameWithoutExtension((bank??"").Replace('\\','/'));
        return name.Equals("ptp_battle_hero",StringComparison.OrdinalIgnoreCase)&&(wave==12||wave==13||wave==14);
    }
    public static float Scale(float raw,int level)=>float.IsFinite(raw)&&raw>=0?raw*Math.Clamp(level,0,20)/10f:raw;
}

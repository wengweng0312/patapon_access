namespace PataponAccess;
internal static class MapWeatherText
{
    // Native P1/P2 Scroll.setupWeather builds this exact seven-entry icon table.
    // Zero has no weather icon. Unknown data must not be described as clear skies.
    public static string Kind(uint value)=>value switch{0=>"none",1=>"rain",2=>"wind",3=>"thunder",4=>"blizzard",5=>"fog",6=>"sand",_=>""};
    public static string Append(string original,string description)=>description.Length==0?original:original+"。"+description;
}

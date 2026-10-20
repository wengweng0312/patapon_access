using System.Text.RegularExpressions;
namespace PataponAccess;
internal static class EvolutionText
{
    public static string Requirements(string text)=>Regex.Replace(text,@"[（(]\s*(\d+)\s*[/／]\s*(\d+)\s*[)）]",m=>$"，持有 {m.Groups[1].Value}，需要 {m.Groups[2].Value}。");
    public static string Build(string description,string requirements,int level)
    {
        string detail=level>=0?Regex.Replace(description,@"\s*Lv\.?\s*\d+","",RegexOptions.IgnoreCase):description;
        string cost=Regex.Replace(Requirements(requirements),@"Lv\.?\s*(\d+)",m=>"升級目標等級 "+m.Groups[1].Value,RegexOptions.IgnoreCase);
        return "進化地圖。"+detail+"。"+(level>=0?$"目前等級 {level}。":"")+cost;
    }
}

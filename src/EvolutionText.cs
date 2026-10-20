using System.Text.RegularExpressions;
namespace PataponAccess;
internal static class EvolutionText
{
    public static string Requirements(string text)=>Regex.Replace(text,@"[（(]\s*(\d+)\s*[/／]\s*(\d+)\s*[)）]",m=>I18n.T("ui.66c0fdc802ea","，持有 {0}，需要 {1}。",m.Groups[1].Value,m.Groups[2].Value));
    public static string Build(string description,string requirements,int level)
    {
        string detail=level>=0?Regex.Replace(description,@"\s*Lv\.?\s*\d+","",RegexOptions.IgnoreCase):description;
        string cost=Regex.Replace(Requirements(requirements),@"Lv\.?\s*(\d+)",m=>I18n.T("ui.45a4631117aa","升級目標等級 ")+m.Groups[1].Value,RegexOptions.IgnoreCase);
        return I18n.T("ui.ba43e9267e46","進化地圖。")+detail+"。"+(level>=0?I18n.T("ui.73ae9bb4e4b3","目前等級 {0}。",level):"")+cost;
    }
}

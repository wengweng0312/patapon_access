using TMPro;
using UnityEngine;
namespace PataponAccess;
internal static class SquadScreenValues
{
    private static string lastSnapshot="";
    internal static string[] Labels {get;private set;}=new string[12];
    internal static string[] Headers {get;private set;}=Array.Empty<string>();
    private static readonly string[] Keys={"HP","Damage","Attack Speed","Resist Ignite","Resist Freeze","Resist Sleep","Crit","KB","Cnc","Ignite","Freeze","Sleep"};
    public static string[] Read()
    {
        // Only called from the active native squad owner. Pair spatial neighbours,
        // never deduplicate equal values or include the other squad names behind it.
        var cells=new List<SquadValuePairs.Cell>();
        foreach(var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>()){
            if(!t||!t.isActiveAndEnabled||t.maxVisibleCharacters==0||t.color.a<0.05f||(!t.name.StartsWith("DrumSysmteText_")&&!t.name.StartsWith("DrumNormalText_")))continue;
            string text=Speech.Clean(t.text);if(text.Length==0)continue;
            var bounds=t.textBounds;var center=t.transform.TransformPoint(bounds.center);
            float height=Math.Abs(t.transform.TransformVector(new Vector3(0,bounds.size.y,0)).y);
            cells.Add(new(text,center.x,center.y,Math.Max(height,0.0001f)));
        }
        Labels=Keys.Select(key=>{
            var matches=cells.Select(c=>c.text).Where(text=>text==key||text==key+" Ave."||text==key+" Ratio"||text==key+" Ratio."||text==key+".").ToArray();
            return matches.Length==1?matches[0]:"";
        }).ToArray();
        Headers=cells.Select(c=>c.text).Where(text=>System.Text.RegularExpressions.Regex.IsMatch(text,@"^(Reborn|Missions)\s+\d+$")).Distinct().GroupBy(text=>text.Split(' ')[0]).Where(group=>group.Count()==1).SelectMany(group=>group).ToArray();
        return SquadValuePairs.Read(cells);
    }
    public static string Describe(string name,int index,int total,int reborn,int missions,IReadOnlyList<string> values)
    {
        var parts=new List<string>{name};parts.AddRange(Headers);
        for(int i=0;i<Math.Min(values.Count,Labels.Length);i++)if(!string.IsNullOrWhiteSpace(values[i])&&!string.IsNullOrWhiteSpace(Labels[i]))parts.Add(Labels[i]+" "+values[i]);
        if(values.Count<Labels.Length||values.Any(string.IsNullOrWhiteSpace))parts.Add(I18n.T("ui.ca422add79da","部分能力數值未取得"));
        string signature=name+":"+index+":"+string.Join("|",values);
        if(signature!=lastSnapshot){lastSnapshot=signature;Monitor.Record("squad-status-snapshot",new{game="P2",name,index,total,values,complete=values.Count==Labels.Length&&values.All(v=>!string.IsNullOrWhiteSpace(v)),source="native-active-squad-and-spatial-value-pairs"});}
        return string.Join("。",parts);
    }
}

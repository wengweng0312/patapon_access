using TMPro;
using UnityEngine;
namespace PataponAccess;
internal static class SquadScreenValues
{
    private static string lastSnapshot="";
    internal static readonly string[] Labels={"平均生命","平均攻擊力","平均攻擊間隔","抗燃燒","抗冰凍","抗睡眠","暴擊率","擊退率","暈眩率","燃燒率","冰凍率","睡眠率"};
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
        return SquadValuePairs.Read(cells);
    }
    public static string Describe(string name,int index,int total,int reborn,int missions,IReadOnlyList<string> values)
    {
        var parts=new List<string>{"出擊部隊選擇",name,$"第 {index+1} 項，共 {total} 項",$"Reborn {Math.Clamp(reborn,0,999)}",$"任務次數 {Math.Clamp(missions,0,999)}"};
        for(int i=0;i<Math.Min(values.Count,Labels.Length);i++)if(!string.IsNullOrWhiteSpace(values[i]))parts.Add(Labels[i]+" "+values[i]);
        if(values.Count<Labels.Length||values.Any(string.IsNullOrWhiteSpace))parts.Add("部分能力數值未取得");
        string signature=name+":"+index+":"+string.Join("|",values);
        if(signature!=lastSnapshot){lastSnapshot=signature;Monitor.Record("squad-status-snapshot",new{game="P2",name,index,total,values,complete=values.Count==Labels.Length&&values.All(v=>!string.IsNullOrWhiteSpace(v)),source="native-active-squad-and-spatial-value-pairs"});}
        return string.Join("。",parts);
    }
}

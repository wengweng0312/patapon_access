using TMPro;
using UnityEngine;
namespace PataponAccess;
internal static class SquadScreenValues
{
    private static string lastSnapshot="";
    internal static string[] Labels=>new[]{I18n.T("ui.bc6429e4653d","平均生命"),I18n.T("ui.e759ba5ba955","平均攻擊力"),I18n.T("ui.d5907b7ac282","平均攻擊間隔"),I18n.T("ui.cf848a52491a","抗燃燒"),I18n.T("ui.78a369efd4ed","抗冰凍"),I18n.T("ui.5b6f8ea7f0e6","抗睡眠"),I18n.T("ui.e50606bb6fae","暴擊率"),I18n.T("ui.5597f14fc0cf","擊退率"),I18n.T("ui.d718956de694","暈眩率"),I18n.T("ui.6999ef4bbd13","燃燒率"),I18n.T("ui.348f10acb469","冰凍率"),I18n.T("ui.d9f1a7f5ab79","睡眠率")};
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
        var parts=new List<string>{I18n.T("ui.59d030f11226","出擊部隊選擇"),name,I18n.T("ui.73f1855f451e","第 {0} 項，共 {1} 項",index+1,total),$"Reborn {Math.Clamp(reborn,0,999)}",I18n.T("ui.269c3ad67c5f","任務次數 {0}",Math.Clamp(missions,0,999))};
        for(int i=0;i<Math.Min(values.Count,Labels.Length);i++)if(!string.IsNullOrWhiteSpace(values[i]))parts.Add(Labels[i]+" "+values[i]);
        if(values.Count<Labels.Length||values.Any(string.IsNullOrWhiteSpace))parts.Add(I18n.T("ui.ca422add79da","部分能力數值未取得"));
        string signature=name+":"+index+":"+string.Join("|",values);
        if(signature!=lastSnapshot){lastSnapshot=signature;Monitor.Record("squad-status-snapshot",new{game="P2",name,index,total,values,complete=values.Count==Labels.Length&&values.All(v=>!string.IsNullOrWhiteSpace(v)),source="native-active-squad-and-spatial-value-pairs"});}
        return string.Join("。",parts);
    }
}

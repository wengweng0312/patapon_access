namespace PataponAccess;
internal static class ReadoutText
{
    public static string Select(string save,string gift,string squad,bool commandWindow)=>save.Length>0?save:gift.Length>0?gift:commandWindow?"":squad;
    public static string[] SquadLabels=>new[]{I18n.T("ui.bc6429e4653d","平均生命"),I18n.T("ui.e759ba5ba955","平均攻擊力"),I18n.T("ui.d5907b7ac282","平均攻擊間隔"),I18n.T("ui.2b832844873a","近戰減傷"),I18n.T("ui.79a1a229f282","弓箭減傷"),I18n.T("ui.a883d9938b91","投槍減傷"),I18n.T("ui.45af2804776e","長槍減傷"),I18n.T("ui.e50606bb6fae","暴擊率"),I18n.T("ui.5597f14fc0cf","擊退率"),I18n.T("ui.d718956de694","暈眩率"),I18n.T("ui.45b428fa8d65","抗暴擊"),I18n.T("ui.84bd678faa8b","抗擊退"),I18n.T("ui.530099f69bb2","抗暈眩")};
    public static string Squad(string name,int index,int total,int reborn,int missions,IReadOnlyList<string> values)
    {
        var parts=new List<string>{I18n.T("ui.59d030f11226","出擊部隊選擇"),name,I18n.T("ui.73f1855f451e","第 {0} 項，共 {1} 項",index+1,total),$"Reborn {reborn}",I18n.T("ui.269c3ad67c5f","任務次數 {0}",missions)};
        for(int i=0;i<Math.Min(values.Count,SquadLabels.Length);i++)if(!string.IsNullOrWhiteSpace(values[i]))parts.Add(SquadLabels[i]+" "+values[i]);
        return string.Join("。",parts);
    }
    public static string Choice(string body,IReadOnlyList<string> options,int index,string context="")
    {
        if(index<0||index>=options.Count||string.IsNullOrWhiteSpace(body)||string.IsNullOrWhiteSpace(options[index]))return "";
        return Prompt(body,context)+" "+options[index];
    }
    public static string Prompt(string body,string context="")=>(context.Length>0?context+"。":"")+body.TrimEnd('。',' ');
}
internal sealed class DialogSpeechTracker
{
    private string previous="";
    public string Say(string key,string prompt,string selected)
    {bool first=key!=previous;previous=key;return first?prompt+" "+selected:selected;}
    public void Reset()=>previous="";
}

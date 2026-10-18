namespace PataponAccess;
internal static class ReadoutText
{
    public static string Select(string save,string gift,string squad,bool commandWindow)=>save.Length>0?save:gift.Length>0?gift:commandWindow?"":squad;
    public static readonly string[] SquadLabels={"平均生命","平均攻擊力","平均攻擊間隔","近戰減傷","弓箭減傷","投槍減傷","長槍減傷","暴擊率","擊退率","暈眩率","抗暴擊","抗擊退","抗暈眩"};
    public static string Squad(string name,int index,int total,int reborn,int missions,IReadOnlyList<string> values)
    {
        var parts=new List<string>{"出擊部隊選擇",name,$"第 {index+1} 項，共 {total} 項",$"Reborn {reborn}",$"任務次數 {missions}"};
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

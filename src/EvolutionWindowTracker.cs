using System.Text.RegularExpressions;
namespace PataponAccess;
internal sealed class EvolutionWindowTracker
{
    private readonly Dictionary<string,(string title,string body,long at)> descriptions=new();
    private string activeName="",previous="";
    private long activeAt;
    private static readonly Regex level=new(@"\s*Lv\.?\s*(\d+)",RegexOptions.IgnoreCase);
    private static string Name(string title){var m=level.Match(title);return(m.Success?title[..m.Index]:title).Trim();}
    public void InvalidateSelection(){previous="";activeName="";activeAt=0;}
    public (bool handled,string text) Observe(string title,string body,long now,string role="")
    {
        if(role=="confirmation")return(false,"");
        if(role=="requirements"||(role.Length==0&&title=="升級條件")){
            var m=level.Match(body);if(!m.Success)return(false,"");
            string name=Name(body);activeName=name;
            bool reopened=now-activeAt>1000;activeAt=now;
            string text;
            if(descriptions.TryGetValue(name,out var d)&&now-d.at<1000){
                var current=level.Match(d.title);
                int lv=current.Success&&int.TryParse(current.Groups[1].Value,out int number)?number:-1;
                text=EvolutionText.Build(d.title+"。"+d.body,title+"。"+body,lv);
            }else text=title+"。"+EvolutionText.Requirements(body);
            if(!reopened&&text==previous)return(true,"");
            previous=text;return(true,text);
        }
        if(title.Length>0&&body.Length>0&&(role=="description"||role.Length==0&&title!="Information")){
            string name=Name(title);
            if(descriptions.Count>64)descriptions.Clear();
            descriptions[name]=(title,body,now);
            if(name==activeName&&now-activeAt<500)return(true,"");
        }
        return(false,"");
    }
}

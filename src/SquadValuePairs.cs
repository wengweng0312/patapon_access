namespace PataponAccess;
internal static class SquadValuePairs
{
    public record Cell(string text,float x,float y,float height);
    internal static readonly string[] Keys={"HP","Damage","Attack Speed","Resist Ignite","Resist Freeze","Resist Sleep","Crit","KB","Cnc","Ignite","Freeze","Sleep"};
    public static string[] Read(IReadOnlyList<Cell> cells){
        var values=new string[Keys.Length];
        for(int i=0;i<Keys.Length;i++){
            var label=cells.Where(c=>c.text==Keys[i]||c.text==Keys[i]+" Ave."||c.text==Keys[i]+" Ratio"||c.text==Keys[i]+" Ratio."||c.text==Keys[i]+".").ToArray();
            if(label.Length!=1)continue;
            var l=label[0];
            float nextColumn=cells.Where(c=>c.x>l.x&&Math.Abs(c.y-l.y)<=Math.Max(l.height,c.height)*0.4f&&Keys.Any(k=>c.text==k||c.text.StartsWith(k+" ")||c.text==k+".")).Select(c=>c.x).DefaultIfEmpty(float.PositiveInfinity).Min();
            var right=cells.Where(c=>c.x>l.x&&c.x<nextColumn&&Math.Abs(c.y-l.y)<=Math.Max(l.height,c.height)*0.4f&&System.Text.RegularExpressions.Regex.IsMatch(c.text,@"^\d+(?:\.\d+)?(?:\s*-\s*\d+)?(?:\s*(?:%|sec))?$"))
                .OrderBy(c=>c.x-l.x).ToArray();
            if(right.Length>0)values[i]=right[0].text;
        }
        return values;
    }
}

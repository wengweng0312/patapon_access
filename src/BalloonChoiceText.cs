namespace PataponAccess;
internal record BalloonChoicePrompt(string Question,string[] Options);
internal static class BalloonChoiceText
{
    public static BalloonChoicePrompt? Parse(string raw)
    {
        var lines=raw.Replace('/', '\n').Split('\n').Select(DialogueText.Clean).Where(s=>s.Length>0).ToArray();
        if(lines.Length<3)return null;
        int start=lines.Length;
        while(start>0&&CommandDialogSpeech.IsChoice(lines[start-1]))start--;
        if(start==0||lines.Length-start<2)return null;
        return new(string.Join("。",lines.Take(start)),lines.Skip(start).ToArray());
    }
}
internal sealed class BalloonChoiceState
{
    private int previous=-1;
    private bool active;
    public (string selected,bool first) Observe(BalloonChoicePrompt prompt,bool ready,int index)
    {
        if(!ready||index<0||index>=prompt.Options.Length){active=false;previous=-1;return("",false);}
        bool first=!active;active=true;
        if(!first&&index==previous)return("",false);
        previous=index;return(prompt.Options[index],first);
    }
}

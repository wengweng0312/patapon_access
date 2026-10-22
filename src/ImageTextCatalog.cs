using System.Text.Json;
namespace PataponAccess;
internal sealed class ImageTextLine
{
    public string Text {get;set;}="";
    public double Top {get;set;}
    public double Bottom {get;set;}
}
internal sealed class ImageTextPage
{
    public string Sprite {get;set;}="";
    public int Width {get;set;}
    public int Height {get;set;}
    public string Source {get;set;}="";
    public string Review {get;set;}="";
    public ImageTextLine[] Lines {get;set;}=Array.Empty<ImageTextLine>();
}
internal static class ImageTextCatalog
{
    // Transcribed from the BOSS RUSH selection screen supplied on 2026-10-22.
    // These are game artwork labels, not translated module descriptions.
    public static string SpecialTitleButton(int index)=>index switch{0=>"PATAPON 1",1=>"PATAPON 2",2=>"BACK",_=>""};
    public static string FocusedLauncherButton(string sprite,string languageKey)
    {
        // The focused native Button identifies its artwork even when its renderer is animated.
        // These four locales were visually checked; do not use the module locale.
        string sourceLanguage=languageKey.ToUpperInvariant() switch{"EN"=>"EN","TC" or "SC" or "JA"=>"JA",_=>""};
        if(sourceLanguage.Length==0)return "";
        var matches=pages.Value.Where(p=>p.Review=="manual-image-check"&&p.Sprite==sprite);
        var text=matches.Select(p=>string.Join(" ",p.Lines.Select(l=>l.Text))).Distinct().ToArray();
        if(text.Length==1)return text[0];
        return matches.Where(p=>p.Source.Contains("/"+sourceLanguage+"/",StringComparison.Ordinal)).Select(p=>string.Join(" ",p.Lines.Select(l=>l.Text))).Distinct().SingleOrDefault()??"";
    }
    public static string ButtonText(string sprite,int logicalWidth,int logicalHeight,int artworkWidth,int artworkHeight)
    {
        // Unity retains the untrimmed layout rect; exported artwork uses the packed texture rect.
        var page=Find(sprite,artworkWidth,artworkHeight)??Find(sprite,logicalWidth,logicalHeight);
        return page?.Review=="manual-image-check"?string.Join(" ",page.Lines.Select(l=>l.Text)):"";
    }
    private static readonly Lazy<ImageTextPage[]> pages=new(()=>{
        var assembly=typeof(ImageTextCatalog).Assembly;
        using var stream=assembly.GetManifestResourceStream("game-text.image-transcriptions.json")??throw new InvalidOperationException("Image transcription resource missing");
        return JsonSerializer.Deserialize<ImageTextPage[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??Array.Empty<ImageTextPage>();
    });
    // Both games' TC/SC/EN/JA title_language00 models use the BACK atlas region.
    // Native type names and unused titleString entries still say Language.
    public static string TitleFallback(string game,int index,int? nativeLanguage)
        =>game is "P1" or "P2" && index==2 && nativeLanguage is 0 or 1 or 7 or 8 ? "BACK" : "";
    public static ImageTextPage? Find(string sprite,int width,int height)
    {
        // Match the actual loaded sprite, not the mod locale or the current policy-language enum.
        var found=pages.Value.Where(p=>p.Sprite==sprite&&p.Width==width&&p.Height==height).ToArray();
        return found.Length==1?found[0]:null;
    }
    public static IEnumerable<(string key,string text)> VisibleLines(ImageTextPage page,double top,double bottom)
    {
        if(!double.IsFinite(top)||!double.IsFinite(bottom)||top>=bottom)yield break;
        for(int i=0;i<page.Lines.Length;i++){
            var line=page.Lines[i];double center=(line.Top+line.Bottom)/2;
            if(center>=top&&center<=bottom&&!string.IsNullOrWhiteSpace(line.Text))yield return(page.Sprite+":"+i,line.Text);
        }
    }
}

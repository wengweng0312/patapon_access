using System.Text.Json;
namespace PataponAccess;
internal static class BossRushManualText
{
    private sealed class Page{public string Language{get;set;}="";public string Sprite{get;set;}="";public string Text{get;set;}="";}
    private static readonly Lazy<Page[]> pages=new(()=>{
        using var stream=typeof(BossRushManualText).Assembly.GetManifestResourceStream("game-text.boss-rush-manual.json")??throw new InvalidOperationException("Missing BOSS RUSH manual transcription");
        return JsonSerializer.Deserialize<Page[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??Array.Empty<Page>();
    });
    public static string Read(string sprite,string nativeLanguage)=>pages.Value.SingleOrDefault(p=>p.Sprite==sprite&&p.Language.Equals(nativeLanguage,StringComparison.OrdinalIgnoreCase))?.Text??"";
}

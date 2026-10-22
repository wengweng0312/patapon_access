using System.Text.Json;
namespace PataponAccess;

// Game prose, separate from the module UI language. P1 text is transcribed from
// the four shipped contract textures; P2 fallback comes from LocalizeData.
internal static class OpeningContractText
{
    private static readonly Lazy<Dictionary<string,string>> texts=new(() => {
        var assembly=typeof(OpeningContractText).Assembly;
        string? name=assembly.GetManifestResourceNames().FirstOrDefault(n=>n.EndsWith("game-text.opening-contracts.json",StringComparison.Ordinal));
        using var stream=name==null?null:assembly.GetManifestResourceStream(name);
        if(stream==null)throw new InvalidOperationException("Opening contract embedded resource is missing.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidOperationException("Opening contract resource is empty.");
    });
    public static string? Resolve(string game,int? gameLanguage,string? live=null)
    {
        if(game is not ("P1" or "P2"))return null;
        if(game=="P2"&&!string.IsNullOrWhiteSpace(live))return live;
        // Unknown/unsupported game languages must never silently read another language.
        return gameLanguage.HasValue?texts.Value.GetValueOrDefault(game+":"+gameLanguage.Value):null;
    }
}

using System.Globalization;
using System.Reflection;
using System.Text.Json;
namespace PataponAccess;
internal static class I18n
{
    public static readonly string[] Languages={"zh-TW","zh-CN","en","ja"};
    private static readonly Dictionary<string,Dictionary<string,string>> catalogs=new(StringComparer.OrdinalIgnoreCase);
    public static string Language {get;private set;}="zh-TW";
    private static readonly HashSet<string> missing=new();
    public static event Action<string>? Missing;
    public static string Normalize(string? language)=>Languages.FirstOrDefault(l=>l.Equals(language,StringComparison.OrdinalIgnoreCase))??"zh-TW";
    public static string LanguageName(string language)=>language switch{"zh-TW"=>"繁體中文","zh-CN"=>"简体中文","en"=>"English","ja"=>"日本語",_=>language};
    public static void SetLanguage(string language){Language=Normalize(language);}
    private static Dictionary<string,string> Catalog(string language)
    {
        if(catalogs.TryGetValue(language,out var cached))return cached;
        var assembly=typeof(I18n).Assembly;
        string? name=assembly.GetManifestResourceNames().FirstOrDefault(n=>n.EndsWith("locales."+language+".json",StringComparison.OrdinalIgnoreCase));
        using var stream=name==null?null:assembly.GetManifestResourceStream(name);
        var result=stream==null?new Dictionary<string,string>():JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??new();catalogs[language]=result;return result;
    }
    public static string In(string language,string key,string fallback,params object?[] args)
    {
        var catalog=Catalog(Normalize(language));
        string text=catalog.TryGetValue(key,out var value)&&!string.IsNullOrWhiteSpace(value)?value:fallback;
        if(text==fallback&&language!="zh-TW"&&!catalog.ContainsKey(key)&&missing.Add(language+":"+key))Missing?.Invoke(language+":"+key);
        if(args.Length==0)return text;
        try{return string.Format(CultureInfo.InvariantCulture,text,args);}catch(FormatException){Missing?.Invoke("format:"+language+":"+key);return string.Format(CultureInfo.InvariantCulture,fallback,args);}
    }
    public static string T(string key,string fallback,params object?[] args)=>In(Language,key,fallback,args);
}

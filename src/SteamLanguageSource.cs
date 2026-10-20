using System.Text.RegularExpressions;
namespace PataponAccess;
internal static class SteamLanguageSource
{
    public const string Fallback="zh-TW";
    public static string? Map(string? value)=>value?.ToLowerInvariant() switch {
        "tchinese"=>"zh-TW", "schinese"=>"zh-CN", "english"=>"en", "japanese"=>"ja",
        // Recognized game languages without a module catalog use English.
        "french" or "german" or "italian" or "spanish" or "koreana"=>"en", _=>null
    };
    public static (string language,string source)? Parse(string text)
    {
        if(text.Length>1024*1024)return null;
        var tokens=Regex.Matches(text,"\"(?:\\\\.|[^\"\\\\])*\"|[{}]|//[^\\r\\n]*")
            .Select(m=>m.Value).Where(t=>!t.StartsWith("//",StringComparison.Ordinal)).ToArray();
        int index=0;
        Dictionary<string,object> Object(int depth,bool nested)
        {
            if(depth>32)throw new FormatException("Manifest nesting limit");
            var result=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
            while(index<tokens.Length) {
                string key=tokens[index++];
                if(key=="}"){if(!nested)throw new FormatException();return result;}
                if(!key.StartsWith('"')||index>=tokens.Length)throw new FormatException();
                key=key[1..^1];string value=tokens[index++];
                if(value=="{")result[key]=Object(depth+1,true);
                else if(value.StartsWith('"'))result[key]=value[1..^1];
                else throw new FormatException();
            }
            if(nested)throw new FormatException();return result;
        }
        try {
            var root=Object(0,false);
            if(!root.TryGetValue("AppState",out var state)||state is not Dictionary<string,object> app)return null;
            if(!app.TryGetValue("appid",out var id)||id is not string appid||appid!="2383200")return null;
            // UserConfig is the selected language; MountedConfig may still be stale.
            foreach(string section in new[]{"UserConfig","MountedConfig"})
                if(app.TryGetValue(section,out var config)&&config is Dictionary<string,object> fields&&fields.TryGetValue("language",out var raw)&&raw is string value) {
                    // An unreadable explicit choice must not silently use an old mounted value.
                    return Map(value) is string language?(language,"Steam/"+section):null;
                }
        }catch(FormatException){ }
        return null;
    }
    public static (string language,string source)? Read(string? gameRoot)
    {
        if(string.IsNullOrWhiteSpace(gameRoot))return null;
        try {
            var directory=new DirectoryInfo(gameRoot);
            // Locate this installation's steamapps only; no account data or other libraries.
            for(int depth=0;directory!=null&&depth<8;depth++,directory=directory.Parent)
                if(directory.Name.Equals("steamapps",StringComparison.OrdinalIgnoreCase)) {
                    string path=Path.Combine(directory.FullName,"appmanifest_2383200.acf");
                    return File.Exists(path)&&new FileInfo(path).Length<=1024*1024?Parse(File.ReadAllText(path)):null;
                }
        }catch(IOException){ }catch(UnauthorizedAccessException){ }catch(ArgumentException){ }
        return null;
    }
}

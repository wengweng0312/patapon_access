using UnityEngine;
namespace PataponAccess;
internal static class BindingNames
{
    private static readonly Dictionary<string,string> spriteKeys=new();
    public static string FromSprite(string raw,string action)
    {
        var match=System.Text.RegularExpressions.Regex.Match(raw,"name=[\"']?([^\"' >]+)");
        if(!match.Success)return "";
        string sprite=match.Groups[1].Value;
        if(spriteKeys.TryGetValue(sprite,out var cached))return cached;
        var manager=UnityEngine.Object.FindObjectOfType<MultiPlatformInputManager>();
        if(!manager)return "";
        var candidates="Tab|Home|End|Page Up|Page Down|Enter|Escape|Space|Backspace|Delete|Insert|Left Arrow|Right Arrow|Up Arrow|Down Arrow|Backslash|\\|/|Left Shift|Right Shift|Left Ctrl|Right Ctrl|Left Alt|Right Alt".Split('|').Concat(Enumerable.Range('A',26).Select(i=>((char)i).ToString())).Concat(Enumerable.Range(0,10).Select(i=>i.ToString())).Concat(Enumerable.Range(1,12).Select(i=>"F"+i));
        foreach(string candidate in candidates) {
            string found=manager.GetKeyboardSpriteName(action,candidate);
            if(found!=sprite)continue;
            string name=candidate=="\\"?"反斜線鍵":candidate+" 鍵";
            spriteKeys[sprite]=name;return name;
        }
        return "";
    }
    public static List<string> Read(MultiPlatformInputManager.AllAction value)
    {
        var result=new List<string>();
        try {
            var manager=UnityEngine.Object.FindObjectOfType<MultiPlatformInputManager>();
            if(!manager || !manager.playerInput || !manager.playerInput.actions)return result;
            string full=value.ToString();int split=full.IndexOf('_');
            string leaf=split>=0?full.Substring(split+1):full;
            var asset=manager.playerInput.actions;
            var action=manager.GetInputAction<MultiPlatformInputManager.AllAction>(value)??asset.FindAction(full.Replace('_','/'),false)??asset.FindAction(leaf,false)??asset.FindAction(full,false);
            if(action==null)return result;
            var bindings=action.bindings;
            for(int i=0;i<bindings.Count;i++) {
                var b=bindings[i];string path=b.effectivePath??"";
                if(b.isComposite || !(path.Contains("Keyboard",StringComparison.OrdinalIgnoreCase)||path.Contains("Mouse",StringComparison.OrdinalIgnoreCase)))continue;
                string key=path.Substring(path.LastIndexOf('/')+1);
                string name=key.ToLowerInvariant() switch {
                    "uparrow"=>"方向鍵上","downarrow"=>"方向鍵下","leftarrow"=>"方向鍵左","rightarrow"=>"方向鍵右",
                    "backslash"=>"反斜線鍵","slash"=>"斜線鍵","space"=>"空白鍵","enter"=>"Enter 鍵","escape"=>"Escape 鍵",
                    "tab"=>"Tab 鍵","home"=>"Home 鍵","end"=>"End 鍵","pageup"=>"Page Up 鍵","pagedown"=>"Page Down 鍵",
                    "leftbutton"=>"滑鼠左鍵","rightbutton"=>"滑鼠右鍵","middlebutton"=>"滑鼠中鍵",
                    _=>key.Length==1?key.ToUpperInvariant()+" 鍵":key+" 鍵"
                };
                result.Add(name);
            }
        }catch(Exception e){Reader.Log.LogWarning("Binding names: "+e.Message);}
        return result;
    }
}



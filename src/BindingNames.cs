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
        string candidate=KeySpriteLabel.Resolve(sprite,action,candidates,(nativeAction,key)=>manager.GetKeyboardSpriteName(nativeAction,key));
        if(candidate.Length==0)return "";
        string name=candidate=="\\"?I18n.T("ui.dea3a5633154","反斜線鍵"):candidate+I18n.T("ui.ecb536912f61"," 鍵");
        spriteKeys[sprite]=name;return name;
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
                    "uparrow"=>I18n.T("ui.d5362001fe6b","方向鍵上"),"downarrow"=>I18n.T("ui.5d9e329a58e2","方向鍵下"),"leftarrow"=>I18n.T("ui.6484e7c7dc02","方向鍵左"),"rightarrow"=>I18n.T("ui.3e1aac5cdc1a","方向鍵右"),
                    "backslash"=>I18n.T("ui.dea3a5633154","反斜線鍵"),"slash"=>I18n.T("ui.abc3ad2eb12e","斜線鍵"),"space"=>I18n.T("ui.6fa9351208e3","空白鍵"),"enter"=>I18n.T("ui.e9790d6135a3","Enter 鍵"),"escape"=>I18n.T("ui.42600694dad8","Escape 鍵"),
                    "tab"=>I18n.T("ui.80a7f88e8347","Tab 鍵"),"home"=>I18n.T("ui.3aead55ef8b6","Home 鍵"),"end"=>I18n.T("ui.1a4a016b0816","End 鍵"),"pageup"=>I18n.T("ui.0bb384d6a26c","Page Up 鍵"),"pagedown"=>I18n.T("ui.692d0e2d045f","Page Down 鍵"),
                    "leftbutton"=>I18n.T("ui.e9f502636cfa","滑鼠左鍵"),"rightbutton"=>I18n.T("ui.dad0f65629a9","滑鼠右鍵"),"middlebutton"=>I18n.T("ui.0e8654b5022a","滑鼠中鍵"),
                    _=>key.Length==1?key.ToUpperInvariant()+I18n.T("ui.ecb536912f61"," 鍵"):key+I18n.T("ui.ecb536912f61"," 鍵")
                };
                result.Add(name);
            }
        }catch(Exception e){Reader.Log.LogWarning("Binding names: "+e.Message);}
        return result;
    }
}



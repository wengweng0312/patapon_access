using HarmonyLib;
using System.Reflection;
namespace PataponAccess;

internal static class NativeTipsAccess
{
    private static long seen;
    private static string current="",key="";
    private static readonly Dictionary<IntPtr,int> campIds=new();
    public static string RepeatText=>Environment.TickCount64-seen<500?current:"";
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.native-tips");
        foreach(string game in new[]{"P1","P2"})try{
            var type=typeof(LauncherMain).Assembly.GetType(game+".GameSystem.Tips.Viewer");
            h.Patch(type!.GetMethod("update"),postfix:new HarmonyMethod(typeof(NativeTipsAccess).GetMethod(game,BindingFlags.Static|BindingFlags.NonPublic)));
            Monitor.Record("native-tips-hook",new{game,installed=true});
            var scene=typeof(LauncherMain).Assembly.GetType(game+".Bases.Camp.Scene")!;
            h.Patch(scene.GetMethod("enableDrawTips"),postfix:new HarmonyMethod(typeof(NativeTipsAccess).GetMethod(nameof(CampSelected),BindingFlags.Static|BindingFlags.NonPublic)));
            h.Patch(scene.GetMethod("drawTips"),postfix:new HarmonyMethod(typeof(NativeTipsAccess).GetMethod("Camp"+game,BindingFlags.Static|BindingFlags.NonPublic)));
            Monitor.Record("native-camp-tips-hook",new{game,installed=true});
            if(game=="P1")h.Patch(scene.GetMethod("tipsLoadCallBackFunc"),postfix:new HarmonyMethod(typeof(NativeTipsAccess).GetMethod(nameof(CampLoadedP1),BindingFlags.Static|BindingFlags.NonPublic)));
            if(game=="P1")h.Patch(typeof(global::P1.GameSystem.Tips.TipsImage).GetMethod("getTexture"),postfix:new HarmonyMethod(typeof(NativeTipsAccess).GetMethod(nameof(TextureP1),BindingFlags.Static|BindingFlags.NonPublic)));
        }catch(Exception e){Reader.Log.LogWarning("Native tips hook: "+e.Message);}
    }
    private static void Offer(string game,int id,string title,string body)
    {
        string text=string.Join("。",new[]{DialogueText.Clean(title),DialogueText.Clean(body)}.Where(s=>s.Length>0));
        if(text.Length==0)return;
        long now=Environment.TickCount64;string next=game+":"+id+":"+text;
        bool changed=next!=key||now-seen>700;seen=now;key=next;current=text;
        if(!changed)return;
        // Camp/loading tips are automatic; never interrupt queued results.
        // Explicit viewer navigation remains immediate.
        CommonUi.QueueSelection(text,interrupt:!game.EndsWith("-camp",StringComparison.Ordinal));Monitor.RequestPage("tips-offered");
        Monitor.Record(game.EndsWith("-camp",StringComparison.Ordinal)?"native-camp-tips-text":"native-tips-text",new{game,id,title,body,source=game.EndsWith("-camp",StringComparison.Ordinal)?"Camp.Scene.drawTips visible image; LocalizeData.imageData":"active Tips.Viewer selected valid sprite; LocalizeData.imageData"});
    }
    private static void P1(global::P1.GameSystem.Tips.Viewer __instance)
    {
        try{
            var v=__instance;if(v.isFinish_||(v.state_!=global::P1.GameSystem.Tips.Viewer.State.State_Focus&&v.state_!=global::P1.GameSystem.Tips.Viewer.State.State_Select))return;
            var sprites=v.tipsAry_;int index=(int)v.selectId_;
            if(sprites==null||index<0||index>=sprites.Count)return;
            var sprite=sprites[index];if(sprite==null||!sprite.isValid_)return;
            int id=(int)sprite.tipsId_;var texts=global::P1.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData?.tipsText?.messages;
            if(texts!=null&&id>=0&&id<texts.Length)Offer("P1",id,"",texts[id]);
        }catch(Exception e){Error(e);}
    }
    private static void P2(global::P2.GameSystem.Tips.Viewer __instance)
    {
        try{
            var v=__instance;if(v.isFinish_||(v.state_!=global::P2.GameSystem.Tips.Viewer.State.State_Focus&&v.state_!=global::P2.GameSystem.Tips.Viewer.State.State_Select))return;
            var sprites=v.tipsAry_;int index=(int)v.selectId_;
            if(sprites==null||index<0||index>=sprites.Count)return;
            var sprite=sprites[index];if(sprite==null||!sprite.isValid_)return;
            int id=(int)sprite.tipsId_;var data=global::P2.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData;
            var titles=data?.tipsTitle?.messages;var texts=data?.tipsText?.messages;
            if(texts!=null&&id>=0&&id<texts.Length)Offer("P2",id,titles!=null&&id<titles.Length?titles[id]:"",texts[id]);
        }catch(Exception e){Error(e);}
    }
    private static void CampSelected(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase __instance,int __0)
    {
        if(campIds.Count>16)campIds.Clear();campIds[__instance.Pointer]=__0;
        Monitor.Record("native-tips-stage",new{stage="camp-id-selected",owner=__instance.Pointer.ToInt64(),id=__0});Monitor.RequestPage("tips-selected");
    }
    private static readonly Dictionary<IntPtr,int> generated=new();
    private static void TextureP1(int __0,UnityEngine.RenderTexture __result)
    {
        try {
            if(__result==null)return;
            if(generated.Count>256)generated.Clear();
            if(generated.TryGetValue(__result.Pointer,out int old)&&old==__0)return;
            generated[__result.Pointer]=__0;
            var body=global::P1.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData?.tipsText?.messages;
            string? raw=body!=null&&__0>=0&&__0<body.Length?body[__0]:null;
            Monitor.Record("native-tips-stage",new{game="P1",stage="texture-returned",id=__0,texture=__result.GetInstanceID(),name=__result.name,raw,textCount=body?.Length??0,spoken=false,reason="texture creation is not proof of foreground display"});Monitor.RequestPage("tips-texture");
        }catch(Exception e){Error(e);}
    }
    private static void CampLoadedP1(global::P1.Bases.Camp.Scene __instance,int __0,bool __result)
    {
        try {
            // Native tipsLoadCallBackFunc inlines enableDrawTips and applies
            // the image to localActiveScene_g, not the callback owner.
            var target=__instance.localActiveScene_g;
            if(!__result||target==null)return;
            CampSelected(target,__0);
            Monitor.Record("native-camp-tips-selected",new{game="P1",id=__0,source="tipsLoadCallBackFunc.localActiveScene_g"});
        }catch(Exception e){Error(e);}
    }
    private static void CampP1(P1.Bases.Camp.Scene __instance)
    {
        try{
            if(!__instance.enableDrawTips_||!__instance.isDrawnTips_)return;
            // drawTips binds texture slot zero. The actual displayed texture
            // is authoritative even when native inlining bypasses ID callbacks.
            var entries=__instance.textureBundle_?.textureAry_;
            int id;
            var handle=entries!=null&&entries.Count>0?entries[0]?.handle:null;
            if(handle!=null&&generated.TryGetValue(handle.Pointer,out int imageId)){
                id=imageId;campIds[__instance.Pointer]=id;
            }
            else if(!campIds.TryGetValue(__instance.Pointer,out id)){
                StageMissing(__instance.Pointer,"visible-without-selected-id");return;
            }
            var data=global::P1.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData;
            var body=data?.tipsText?.messages;
            if(body!=null&&id>=0&&id<body.Length)Offer("P1-camp",id,"",body[id]);
            else StageMissing(__instance.Pointer,"selected-id-without-native-text");
        }catch(Exception e){Error(e);}
    }
    private static readonly Dictionary<IntPtr,string> missing=new();
    private static void StageMissing(IntPtr owner,string reason)
    {
        if(missing.TryGetValue(owner,out var previous)&&previous==reason)return;
        if(missing.Count>32)missing.Clear();missing[owner]=reason;
        Monitor.Record("native-tips-stage",new{game="P1",owner=owner.ToInt64(),stage=reason,spoken=false});Monitor.RequestPage("tips-unavailable");
    }
    private static void CampP2(P2.Bases.Camp.Scene __instance)
    {
        try{
            if(!__instance.enableDrawTips_||!__instance.isDrawnTips_)return;
            int id=__instance.tipsId_;
            var data=global::P2.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData;
            var body=data?.tipsText?.messages;var titles=data?.tipsTitle?.messages;
            if(body!=null&&id>=0&&id<body.Length)Offer("P2-camp",id,titles!=null&&id<titles.Length?titles[id]:"",body[id]);
        }catch(Exception e){Error(e);}
    }
    private static long lastError;
    private static void Error(Exception e){if(Environment.TickCount64-lastError<5000)return;lastError=Environment.TickCount64;Monitor.Record("native-tips-error",new{error=e.Message});}
}

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
        }catch(Exception e){Reader.Log.LogWarning("Native tips hook: "+e.Message);}
    }
    private static void Offer(string game,int id,string title,string body)
    {
        string text=string.Join("。",new[]{DialogueText.Clean(title),DialogueText.Clean(body)}.Where(s=>s.Length>0));
        if(text.Length==0)return;
        long now=Environment.TickCount64;string next=game+":"+id+":"+text;
        bool changed=next!=key||now-seen>700;seen=now;key=next;current=text;
        if(!changed)return;
        CommonUi.QueueSelection(text);
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
    }
    private static void CampP1(P1.Bases.Camp.Scene __instance)
    {
        try{
            if(!__instance.enableDrawTips_||!__instance.isDrawnTips_||!campIds.TryGetValue(__instance.Pointer,out int id))return;
            var data=global::P1.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData;
            var body=data?.tipsText?.messages;
            if(body!=null&&id>=0&&id<body.Length)Offer("P1-camp",id,"",body[id]);
        }catch(Exception e){Error(e);}
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

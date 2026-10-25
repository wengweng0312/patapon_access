using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PataponAccess;

internal static class Monitor
{
    private static bool enabled;
    private static StreamWriter? writer;
    private static long bytes;
    private static int part;
    private static string directory="";
    private static string run="";
    private static float next;
    private static float nextSnapshot;
    private static string scene="";
    private static string focus="";
    private static Dictionary<int,string> texts=new();
    private static readonly Dictionary<string,(string value,long time)> recent=new();
    private static readonly object gate=new();
    private static readonly Dictionary<string,string> snapshots=new();
    private static int events;
    private static long windowStart;
    private static int dropped;
    private static long sequence;
    private static readonly System.Diagnostics.Stopwatch elapsed=System.Diagnostics.Stopwatch.StartNew();
    private static readonly PageSnapshotState page=new();
    private static LogBudget budget=new(50L*1024*1024,0,50L*1024*1024);
    public static void RequestPage(string reason){if(enabled)page.Request(reason);}
    public static void Initialize(bool value,bool autoClean,int retentionDays,int maxTotalMb)
    {
        enabled=value;
        if(!enabled) return;
        try {
            directory=System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,"accessibility-logs");
            Directory.CreateDirectory(directory);
            const long sessionLimit=50L*1024*1024;
            if(autoClean)LogRetention.Clean(directory,retentionDays,maxTotalMb,message=>Reader.Log.LogInfo(message),sessionLimit);
            long existing=new DirectoryInfo(directory).GetFiles().Sum(f=>f.Length);
            budget=new((long)Math.Clamp(maxTotalMb,50,10240)*1024*1024,existing,sessionLimit);
            run=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Environment.ProcessId;
            OpenPart();
            string assembly=typeof(Plugin).Assembly.Location;
            Record("session",new { plugin=Plugin.ModVersion, schema=2, unity=Application.unityVersion, process=Environment.ProcessId, runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, pluginSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly))) });
            var h=new Harmony("org.patapon.access.monitor");
            TitleAccess.Install(h);
            // Read-only observations of dialogue presentation and localization lookups.
            foreach(var ns in new[]{"P1","P2","P1S","P2S"}) {
                // Native message state setters are shared across game namespaces; do not detour them.


                var type=typeof(LauncherMain).Assembly.GetType(ns+".Localize.Manager");
                if(type!=null) foreach(var m in type.GetMethods().Where(m=>m.Name=="getStringFromIndex")) TryPatch(h,m,true);
            }
            // Agreement Open/Close methods share native addresses. Poll their UI instead.
            foreach(var type in new[]{typeof(MenuItemBase),typeof(SelectableMenuItem),typeof(LabelMenuItem),typeof(SettingTab),typeof(ScrollMenuBase)})
                foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>new[]{"UpdateFocus","UpdateIndex","UpdateCursor","UpdateSelectIndex","BeginEdit","EndEdit"}.Contains(m.Name))) TryPatch(h,m,false);
            Reader.Log.LogInfo("Local event monitor: "+directory);
        } catch(Exception e) { enabled=false; Reader.Log.LogError("Monitor initialization: "+e); }
    }
    private static void OpenPart()
    {
        writer?.Dispose();
        writer=new StreamWriter(System.IO.Path.Combine(directory,run+"-"+part+".jsonl"),false,new System.Text.UTF8Encoding(false)){AutoFlush=true};
        bytes=0;
    }
    public static void Record(string kind,object data)
    {
        if(!enabled) return;
        if(kind is "speech-dispatch" or "speech-focus" or "native-window-state")RequestPage(kind);
        lock(gate) {
            try {
                var now=Environment.TickCount64;
                if(now-windowStart>1000) { windowStart=now; events=0; if(dropped>0) {
                    if(!WriteLine(JsonSerializer.Serialize(new{schema=2,run,seq=++sequence,elapsedMs=elapsed.ElapsedMilliseconds,frame=Time.frameCount,time=DateTimeOffset.Now,kind="rate-limit",data=new{dropped}})))return;dropped=0;
                } }
                if(events++>250) {dropped++;return;}
                var line=JsonSerializer.Serialize(new { schema=2, run, pageRevision=page.Revision, mission=BattleMonitor.Mission, seq=++sequence, elapsedMs=elapsed.ElapsedMilliseconds, frame=Time.frameCount, time=DateTimeOffset.Now, kind, data });
                WriteLine(line);
            } catch(Exception e) { enabled=false; Reader.Log.LogWarning("Monitor stopped: "+e.Message); }
        }
    }
    private static bool WriteLine(string line)
    {
        long size=System.Text.Encoding.UTF8.GetByteCount(line)+System.Text.Encoding.UTF8.GetByteCount(Environment.NewLine);
        if((part>=4&&bytes>0&&bytes+size>10*1024*1024)||!budget.Take(size)){enabled=false;writer?.Dispose();writer=null;Reader.Log.LogWarning("Event monitor reached session or total log capacity limit; gameplay and speech continue.");return false;}
        if(bytes>0&&bytes+size>10*1024*1024){part++;OpenPart();}
        writer?.WriteLine(line);bytes+=size;return true;
    }
    public static void Finish()
    {
        lock(gate) {
            if(!enabled)return;
            Record("session-end",new{reason="application-quit",droppedPending=dropped});
            enabled=false;writer?.Dispose();writer=null;
        }
    }
    private static void Patch(Harmony harmony,string name,string[] methods)
    {
        var t=typeof(LauncherMain).Assembly.GetType(name);
        if(t==null) {Record("hook-missing",new {type=name});return;}
        foreach(var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>methods.Contains(m.Name))) TryPatch(harmony,m,false);
    }
    private static void TryPatch(Harmony h,MethodInfo m,bool result)
    {
        try {h.Patch(m,postfix:new HarmonyMethod(typeof(Monitor).GetMethod(result?nameof(LocalizationPostfix):nameof(EventPostfix),BindingFlags.Static|BindingFlags.NonPublic)));Record("hook-installed",new{method=m.DeclaringType+"."+m});}
        catch(Exception e) {Record("hook-failed",new{method=m.DeclaringType+"."+m,error=e.Message});}
    }
    private static string DescribeArg(object? arg) => arg switch {
        null=>"null", string s=>s.Length>16000?s.Substring(0,16000):s,
        bool or int or uint or float or double or Enum=>arg.ToString()??"",
        _=>"["+arg.GetType().FullName+"]"
    };
    private static bool Changed(string key,string value)
    {
        lock(gate) {
            long now=Environment.TickCount64;
            if(recent.TryGetValue(key,out var old) && old.value==value && now-old.time<2000) return false;
            if(recent.Count>4096) recent.Clear();
            recent[key]=(value,now);return true;
        }
    }
    private static void EventPostfix(MethodBase __originalMethod,object[] __args)
    {
        try {string method=__originalMethod.DeclaringType+"."+__originalMethod.Name; var args=__args.Select(DescribeArg).ToArray();if(Changed(method,string.Join("|",args))) {Record("game-event",new{method,args});RequestPage(method);}} catch { }
    }
    private static void LocalizationPostfix(MethodBase __originalMethod,object[] __args,string __result)
    {
        try {string method=__originalMethod.DeclaringType+"."+__originalMethod.Name;var args=__args.Select(DescribeArg).ToArray();if(Changed(method+string.Join("|",args),__result)) Record("localization",new{method,args,text=__result});} catch { }
    }
    public static string PathOf(Transform transform)
    {
        var parts=new List<string>();
        for(int i=0;transform && i<32;i++,transform=transform.parent) parts.Add(transform.name);
        parts.Reverse();return string.Join("/",parts);
    }
    public static void Tick()
    {
        if(!enabled)return;
        if(Time.unscaledTime<next){if(page.Pending)CapturePage();return;}
        next=Time.unscaledTime+0.5f;
            BattleMonitor.Health();
        try {
            string current=string.Join("|",Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).name));
            bool changed=current!=scene;
            if(changed) { scene=current; Record("scene",new { scenes=scene }); texts.Clear(); snapshots.Clear(); nextSnapshot=0;RequestPage("scene"); }
            if(Time.unscaledTime>=nextSnapshot) {nextSnapshot=Time.unscaledTime+3;Snapshot();}
            var go=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            string path=go?PathOf(go!.transform):"";
            if(path!=focus) {focus=path;Record("ui-focus",new{path});RequestPage("focus");}
            NativeWindowAudit.Tick();
            var seen=new HashSet<int>();
            foreach(var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>().Take(2000)) {
                if(!t || !t.isActiveAndEnabled) continue;
                int id=t.GetInstanceID(); seen.Add(id);
                string value=t.text??"";bool visible=t.maxVisibleCharacters!=0&&t.color.a>=0.05f;
                string signature=value+"\0"+visible;
                if(!texts.TryGetValue(id,out var old)||old!=signature) {texts[id]=signature;Record("text-change",new{id,path=PathOf(t.transform),component="TMP",text=value,maxVisibleCharacters=t.maxVisibleCharacters,visible,alpha=t.color.a,visibilityEvidence="active component, alpha and character limit; canvas occlusion unknown"});RequestPage("text");}
            }
            foreach(var t in UnityEngine.Object.FindObjectsOfType<Text>().Take(2000)) {
                if(!t || !t.isActiveAndEnabled) continue;
                int id=t.GetInstanceID();seen.Add(id);string value=t.text??"";
                if(!texts.TryGetValue(id,out var old)||old!=value) {texts[id]=value;Record("text-change",new{id,path=PathOf(t.transform),component="UI.Text",text=value});RequestPage("text");}
            }
            foreach(int id in texts.Keys.Where(id=>!seen.Contains(id)).ToArray()) {Record("text-hidden",new{id});texts.Remove(id);RequestPage("text-hidden");}
            if(page.Pending)CapturePage();
        } catch(Exception e) {Record("monitor-error",new{error=e.Message});next=Time.unscaledTime+2;}
    }
    private static void Snapshot()
    {
        RequestPage("bounded-periodic-check");
        foreach(var c in UnityEngine.Object.FindObjectsOfType<Selectable>().Take(400)) {
            var data=new {path=PathOf(c.transform),type=c.GetIl2CppType().FullName,interactable=c.IsInteractable(),components=c.gameObject.GetComponents<Component>().Select(x=>x?x.GetIl2CppType().FullName:"null").ToArray()};
            if(SnapshotChanged("control:"+c.GetInstanceID(),JsonSerializer.Serialize(data))) Record("control",data);
        }
        foreach(var image in UnityEngine.Object.FindObjectsOfType<Image>().Take(400))
            if(image && image.sprite) {
                var data=new{path=PathOf(image.transform),sprite=image.sprite.name};
                if(SnapshotChanged("image:"+image.GetInstanceID(),JsonSerializer.Serialize(data))) Record("image",data);
            }
    }
    private static void CapturePage()
    {
        try {
            var reasons=page.Reasons;
            var labels=UnityEngine.Object.FindObjectsOfType<TMP_Text>().Take(2000).Where(t=>t&&t.isActiveAndEnabled).OrderBy(t=>t.GetInstanceID()).Select(t=>new{id=t.GetInstanceID(),path=PathOf(t.transform),raw=t.text??"",characters=t.maxVisibleCharacters,alpha=t.color.a,captureOnly=PathOf(t.transform).StartsWith("Tips/",StringComparison.Ordinal)}).ToArray();
            var plain=UnityEngine.Object.FindObjectsOfType<Text>().Take(2000).Where(t=>t&&t.isActiveAndEnabled).OrderBy(t=>t.GetInstanceID()).Select(t=>new{id=t.GetInstanceID(),path=PathOf(t.transform),raw=t.text??"",alpha=t.color.a}).ToArray();
            var controls=UnityEngine.Object.FindObjectsOfType<Selectable>().Take(400).Where(c=>c&&c.isActiveAndEnabled).OrderBy(c=>c.GetInstanceID()).Select(c=>new{id=c.GetInstanceID(),path=PathOf(c.transform),type=c.GetIl2CppType().FullName,interactable=c.IsInteractable()}).ToArray();
            var images=UnityEngine.Object.FindObjectsOfType<Image>().Take(400).Where(i=>i&&i.isActiveAndEnabled).OrderBy(i=>i.GetInstanceID()).Select(i=>new{id=i.GetInstanceID(),path=PathOf(i.transform),sprite=i.sprite?i.sprite.name:"",alpha=i.color.a,culled=i.canvasRenderer.cull}).ToArray();
            var textures=UnityEngine.Object.FindObjectsOfType<RawImage>().Take(400).Where(i=>i&&i.isActiveAndEnabled).OrderBy(i=>i.GetInstanceID()).Select(i=>new{id=i.GetInstanceID(),path=PathOf(i.transform),texture=i.texture?i.texture.GetInstanceID():0,name=i.texture?i.texture.name:"",type=i.texture?i.texture.GetIl2CppType().FullName:"",alpha=i.color.a,culled=i.canvasRenderer.cull}).ToArray();
            var data=new{scenes=scene,focus,labels,plain,controls,images,textures,evidence="active components and own alpha/cull only; final occlusion and native-drawn content unknown",limits=new{textPerKind=2000,controls=400,imagesPerKind=400}};
            if(page.Commit(JsonSerializer.Serialize(data)))Record("page-snapshot",new{revision=page.Revision,reasons,data,capture="next Reader tick for known callbacks; 0.5s text/focus polling and 3s fallback for other changes"});
        }catch(Exception e){page.Commit("capture-error:"+e.GetType().Name);Record("page-snapshot-error",new{error=e.Message});}
    }
    private static bool SnapshotChanged(string key,string value)
    {
        if(snapshots.TryGetValue(key,out var old)&&old==value)return false;
        if(snapshots.Count>8192)snapshots.Clear();snapshots[key]=value;return true;
    }
}

internal static class TitleAccess
{
    public static string Current="";
    private static long lastSeen;
    public static bool IsActive => Current.Length>0 && Environment.TickCount64-lastSeen<800;
    public static void Install(Harmony harmony)
    {
        try {harmony.Patch(typeof(P1.Title.Display).GetMethod("updateSelect"),postfix:new HarmonyMethod(typeof(TitleAccess).GetMethod(nameof(ObserveP1),BindingFlags.NonPublic|BindingFlags.Static)));}
        catch(Exception e) {Monitor.Record("title-hook-failed",new{error=e.Message});}
        try {harmony.Patch(typeof(P2.Title.Display).GetMethod("updateSelect"),postfix:new HarmonyMethod(typeof(TitleAccess).GetMethod(nameof(ObserveP2),BindingFlags.NonPublic|BindingFlags.Static)));}
        catch(Exception e) {Monitor.Record("title-hook-failed",new{game="P2",error=e.Message});}
    }
    // Observe only the owning instance; never marshal the original ref argument.
    private static void ObserveP2(P2.Title.Display __instance)
    {
        try {
            int index=__instance.selectIndex_;
            var messages=P2.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData?.titleString?.messages;
            int nativeIndex=index switch{0=>0,1=>1,_=>-1};
            string label=messages!=null&&nativeIndex>=0&&nativeIndex<messages.Length?DialogueText.Clean(messages[nativeIndex]):"";
            if(index==2)label=ImageTextCatalog.TitleFallback("P2",index,(int?)P2.LaboCommon.pLaboCommonInstance_g?.getLanguageDataType());
            string text=label;
            if(text.Length==0){Current="";return;}
            if(text!=Current) Monitor.Record("native-title-focus",new{game="P2",index,state=__instance.state_.ToString(),label});
            Current=text;lastSeen=Environment.TickCount64;
        } catch(Exception e) {Monitor.Record("native-title-error",new{game="P2",error=e.Message});}
    }    private static void ObserveP1(P1.Title.Display __instance)
    {
        try {
            int index=__instance.selectIndex_;
            var messages=P1.LaboCommon.pLaboCommonInstance_g?.getLocalizeDataItem?.imageData?.titleString?.messages;
            int nativeIndex=index switch{0=>1,1=>2,_=>-1};
            string label=messages!=null&&nativeIndex>=0&&nativeIndex<messages.Length?DialogueText.Clean(messages[nativeIndex]):"";
            // title_language00.gxx uses the BACK region of title_labo02_0.
            // Verified in TC/SC/EN/JA source atlases; preserve the displayed English.
            if(index==2)label=ImageTextCatalog.TitleFallback("P1",index,(int?)P1.LaboCommon.pLaboCommonInstance_g?.getLanguageDataType());
            if(label.Length==0){Current="";return;}
            string text=label;
            if(text!=Current) Monitor.Record("native-title-focus",new{game="P1",index,state=__instance.state_.ToString(),label});
            Current=text;lastSeen=Environment.TickCount64;
        } catch(Exception e) {Monitor.Record("native-title-error",new{error=e.Message});}
    }
}






















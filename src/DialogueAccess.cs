using System.Reflection;
using HarmonyLib;

namespace PataponAccess;

internal static class DialogueAccess
{
    public static bool QuietCampChatter=true;
    private static readonly Dictionary<string,long> idleSpoken=new();
    private static readonly HashSet<string> knownIdle=new(StringComparer.Ordinal) {
        "在世界的盡頭 有【那個東西】存在！",
        "只要看到【那個東西】 就能變幸福♪",
        "看到【那個東西】的話 眼睛會瞎掉 這是真的嗎？",
        "【那個東西】是指什麼？", "…打嗝！",
        "今晚就 飲酒作樂 到天明吧♪", "我們去瞧瞧 世界的盡頭吧！"
    };
    private static readonly object gate=new();
    private static readonly DialogueQueue queue=new();
    private static string previous="";
    private static long previousAt;
    public static void Install()
    {
        var h=new Harmony("org.patapon.access.dialogue");
        AmbientDialogue.Install(h);
        BalloonChoiceAccess.Install(h);
        BattleChants.Install(h);
        foreach(string ns in new[]{"P1","P2","P1S","P2S"})
        foreach(string suffix in new[]{".Game.Window.Message",".GameSystem.MessageWindow"}) {
            var type=typeof(LauncherMain).Assembly.GetType(ns+suffix);
            if(type==null) continue;
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>new[]{"openW","callWindowW"}.Contains(m.Name) && m.GetParameters().Any(p=>p.ParameterType==typeof(string)))) {
                try {h.Patch(method,postfix:new HarmonyMethod(typeof(DialogueAccess).GetMethod(nameof(Observe),BindingFlags.NonPublic|BindingFlags.Static)));Reader.Log.LogInfo("Dialogue speech hook: "+type.FullName+"."+method.Name);}
                catch(Exception e) {Reader.Log.LogWarning("Dialogue hook failed: "+type.FullName+"."+method.Name+": "+e.Message);}
            }
        }
    }
    private static void Observe(object __instance,MethodBase __originalMethod,object[] __args)
    {
        try {
            Monitor.Record("game-event",new{method=__originalMethod.DeclaringType+"."+__originalMethod.Name,args=__args.Select(a=>a?.ToString()??"null").ToArray()});
            var parameters=__originalMethod.GetParameters();
            for(int i=0;i<parameters.Length;i++) if(parameters[i].ParameterType==typeof(string) && __args[i] is string raw) {
                BalloonChoiceAccess.Offer(__instance,raw);
                AttackCue.Observe(raw); // Audio preference never suppresses the original dialogue.
                string text=DialogueText.Clean(raw);
                if(text.Length==0) continue;
                CannonWarning.Observe(__originalMethod.DeclaringType+"."+__originalMethod.Name,text);
                if(BattleChants.Suppress(text)){
                    Monitor.Record("battle-chant-suppressed",new{source=__originalMethod.DeclaringType+"."+__originalMethod.Name,text,speaker=BattleChants.Source});
                    continue;
                }
                string hintKey="";
                bool protectedHint=QuietCampChatter&&AmbientDialogue.IsAmbient&&CampHintAccess.Identify(__originalMethod.DeclaringType?.Namespace?.Split('.')[0]??"",raw,out hintKey);
                if(protectedHint&&CampHintAccess.WasSpoken(hintKey)){
                    Monitor.Record("camp-hint-repeat-suppressed",new{key=hintKey,text,template=raw,reason="already-read-this-camp-visit"});continue;
                }
                if(QuietCampChatter && AmbientDialogue.IsAmbient&&!protectedHint) {
                    Monitor.Record("ambient-dialogue-suppressed",new{source=__originalMethod.DeclaringType+"."+__originalMethod.Name,text,ambientDepth=AmbientDialogue.Depth,appealDepth=AmbientDialogue.AppealDepth});
                    continue;
                }
                lock(gate) {
                    long now=Environment.TickCount64;
                    if(QuietCampChatter && __originalMethod.DeclaringType?.FullName=="P1.Game.Window.Message" && __originalMethod.Name=="openW" && knownIdle.Contains(text)) {
                        if(idleSpoken.TryGetValue(text,out var last) && now-last<120000) {
                            Monitor.Record("idle-chatter-repeat-suppressed",new{text,source=__originalMethod.DeclaringType.FullName+".openW"});
                            continue;
                        }
                        idleSpoken[text]=now;
                    }
                    string description=MapWeatherAccess.Capture(__instance);
                    string dedup=text+"\n"+description;
                    if(dedup==previous && now-previousAt<800) continue;
                    previous=dedup;previousAt=now;
                    queue.Add(text,DialogueText.HasVariableToken(raw)?raw:"",protectedHint?hintKey:"",__originalMethod.DeclaringType+"."+__originalMethod.Name,now,description);
                }
                if(protectedHint)Monitor.Record("camp-hint-preserved",new{key=hintKey,text,template=raw});
                Monitor.Record("dialogue-queued",new{source=__originalMethod.DeclaringType+"."+__originalMethod.Name,text,ambientDepth=AmbientDialogue.Depth,appealDepth=AmbientDialogue.AppealDepth,speaker=BattleChants.Source});
            }
        } catch(Exception e) {Reader.Log.LogWarning("Dialogue capture: "+e.Message);}
    }
    public static string Take()
    {
        lock(gate) {
            long now=Environment.TickCount64;
            var entry=queue.Peek(now);if(entry==null)return "";
            string text=entry.Text;
            if(entry.Template.Length>0) {
                foreach(var item in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) {
                    if(!item||!item.isActiveAndEnabled||item.color.a<0.05f||item.maxVisibleCharacters==0)continue;
                    string resolved=DialogueText.ResolveTemplate(entry.Template,item.text);
                    if(resolved.Length==0)continue;
                    text=resolved;Monitor.Record("dialogue-template-resolved",new{text,template=entry.Template,path=Monitor.PathOf(item.transform),hasLevel=DialogueText.HasLevelToken(entry.Template)});entry.Template="";break;
                }
                if(entry.Template.Length>0&&now-entry.CapturedAt<(DialogueText.HasLevelToken(entry.Template)?DialogueText.LevelResolutionTimeoutMs:DialogueText.NameResolutionTimeoutMs)){entry.ReadyAt=now+50;return "";}
                if(entry.Template.Length>0){
                    Monitor.Record("dialogue-template-unresolved",new{text,template=entry.Template,reason="rendered-match-not-found-before-timeout"});
                    if(DialogueText.HasLevelToken(entry.Template))text=System.Text.RegularExpressions.Regex.Replace(text,@"Lv\.\s*",I18n.T("ui.16b0234b5343","等級未取得 "));entry.Template="";
                }
            }
            text=MapWeatherText.Append(text,entry.Description);
            queue.Complete(entry);
            CampHintAccess.Offer(entry.Hint,text);
            Monitor.Record("dialogue-dequeued",new{text,source=entry.Source,waitMs=now-entry.CapturedAt,remaining=queue.Count});
            return text;
        }
    }
    public static void Clear(string reason="existing-context-clear")
    {
        lock(gate){int count=queue.Clear();if(count>0)Monitor.Record("dialogue-queue-cleared",new{reason,count});}
    }
}

internal static class AmbientDialogue
{
    [ThreadStatic] private static int depth;
    [ThreadStatic] private static int appealDepth;
    public static bool IsAmbient=>depth>0;
    public static int Depth=>depth;
    public static int AppealDepth=>appealDepth;
    public static void Install(Harmony h)
    {
        foreach(string ns in new[]{"P1","P2","P1S","P2S"}) {
            Patch(h,ns+".Bases.Camp.Person.Behavior.Idle","update",nameof(Enter),nameof(Leave));
            Patch(h,ns+".Bases.Camp.Person.Behavior.Stroll","update",nameof(Enter),nameof(Leave));
            // Direct appeal calls remain readable; calls nested in idle updates are ambient.
            Patch(h,ns+".Bases.Camp.Person.Behavior.Idle","drawAppealMsg",nameof(EnterAppeal),nameof(LeaveAppeal));
        }
    }
    private static void Patch(Harmony h,string type,string name,string enter,string leave)
    {
        try {
            var t=typeof(LauncherMain).Assembly.GetType(type);
            var method=t?.GetMethod(name,BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
            if(method==null)return;
            var prefix=new HarmonyMethod(typeof(AmbientDialogue).GetMethod(enter,BindingFlags.NonPublic|BindingFlags.Static));
            var restore=new HarmonyMethod(typeof(AmbientDialogue).GetMethod(leave,BindingFlags.NonPublic|BindingFlags.Static));
            h.Patch(method,prefix:prefix,postfix:restore,finalizer:restore);
            Reader.Log.LogInfo("Ambient speech scope: "+type+"."+name);
        }catch(Exception e){Reader.Log.LogWarning("Ambient speech scope: "+e.Message);}
    }
    private static void Enter(out int __state){__state=depth;depth++;}
    private static void Leave(int __state){depth=__state;}
    private static void EnterAppeal(out int __state){__state=appealDepth;appealDepth++;}
    private static void LeaveAppeal(int __state){appealDepth=__state;}
}





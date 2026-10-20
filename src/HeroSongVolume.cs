using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class HeroSongVolume
{
    private sealed record Voice(uint Generation,int Cue,string Bank,float RawVolume);
    private static readonly Dictionary<IntPtr,Voice> voices=new();
    private static ConfigEntry<int>? volume;
    private static ConfigFile? settings;
    private static int previewIndex;
    private static long lastError;
    [ThreadStatic] private static bool applying;
    public static int Level=>volume?.Value??5;
    public static void Initialize(ConfigFile config)
    {
        settings=config;
        volume=config.Bind("GameAudio","P2HeroSongVolume",5,new ConfigDescription("Only P2 hero_mode_kakudai intro and hero_mode_song1/song2. 0 mute, 5 half volume, 10 original, 20 double. F1 adjusts and previews.",new AcceptableValueRange<int>(0,20)));
        var h=new Harmony("org.patapon.access.hero-song-volume");
        Hook(h,typeof(CriSoundManager),"CreateSoundSource",nameof(Creating),nameof(Created));
        Hook(h,typeof(CriSoundManager.SoundSource),"SetVolume",nameof(Set),null);
        Hook(h,typeof(CriSoundManager.SoundSource),"SetFadeVolume",nameof(Set),null);
    }
    private static void Hook(Harmony h,Type type,string method,string? prefix,string? postfix)
    {
        try{var m=type.GetMethod(method)!;if(m.GetParameters().Any(p=>p.ParameterType.IsByRef))throw new InvalidOperationException("Unsupported native signature");
            HarmonyMethod? H(string? name)=>name==null?null:new HarmonyMethod(typeof(HeroSongVolume).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static));
            h.Patch(m,prefix:H(prefix),postfix:H(postfix));Monitor.Record("hero-song-volume-hook",new{type=type.FullName,method});
        }catch(Exception e){Error(e);}
    }
    private static void Error(Exception e){long now=Environment.TickCount64;if(now-lastError<5000)return;lastError=now;Reader.Log.LogWarning("Hero song volume: "+e.Message);}
    private static void Creating(CriSoundManager.SoundGameFileType __0,string __2,int __8,ref float __9,out float? __state)
    {
        __state=null;
        if(!HeroSongRules.IsTarget(__0.ToString(),__2,__8)||!float.IsFinite(__9)||__9<0)return;
        __state=__9;__9=HeroSongRules.Scale(__9,Level);
    }
    private static void Created(CriSoundManager.SoundSource? __result,float? __state,float __11,float __10,int __8)
    {
        if(__result==null)return;
        if(__state==null){lock(voices)voices.Remove(__result.Pointer);return;}
        try{
            lock(voices){if(voices.Count>256)voices.Clear();voices[__result.Pointer]=new(__result.globalCounter,__result.cueId,__result.acbFilePath??"",__state.Value);}
            Monitor.Record("hero-song-volume-applied",new{bank=__result.acbFilePath,cue=__result.cueId,level=Level,raw=__state.Value,scaled=HeroSongRules.Scale(__state.Value,Level),wave=__8,pitch=__11,pan=__10});
        }catch(Exception e){Error(e);}
    }
    private static void Set(CriSoundManager.SoundSource __instance,ref float __0)
    {
        if(applying||!float.IsFinite(__0)||__0<0)return;
        try{
            lock(voices){
                if(!voices.TryGetValue(__instance.Pointer,out var voice))return;
                if(!Current(__instance,voice)||__instance.returned){voices.Remove(__instance.Pointer);return;}
                voices[__instance.Pointer]=voice with{RawVolume=__0};__0=HeroSongRules.Scale(__0,Level);
            }
        }catch(Exception e){Error(e);}
    }
    private static bool Current(CriSoundManager.SoundSource source,Voice voice)=>voice.Generation==source.globalCounter&&voice.Cue==source.cueId&&string.Equals(voice.Bank,source.acbFilePath,StringComparison.OrdinalIgnoreCase);
    public static int Adjust(int delta)
    {
        if(volume==null)return Level;
        volume.Value=Math.Clamp(Level+delta,0,20);
        try{settings?.Save();}catch(Exception e){Error(e);}
        try{
            // Inspect current pool owners; retain only scalar identity and unscaled gain.
            var pool=CriSoundManager.poolManager?.PoolSoundSource;
            if(pool!=null)foreach(var source in pool){
                if(source==null||source.returned)continue;
                Voice? voice;lock(voices)voices.TryGetValue(source.Pointer,out voice);
                if(voice==null||!Current(source,voice))continue;
                applying=true;try{source.SetFadeVolume(HeroSongRules.Scale(voice.RawVolume,Level),0.06f);}finally{applying=false;}
            }
        }catch(Exception e){Error(e);}
        Monitor.Record("hero-song-volume-setting",new{level=Level});return Level;
    }
    public static void Preview()
    {
        try{
            if(P2.LaboCommon.pLaboCommonInstance_g==null){Speech.Speak(I18n.T("ui.89b093c0d560","請先進入二代，再試聽英雄模式歌聲。"));return;}
            var orchestra=P2.LaboCommon.getApplication()?.orchestra_;
            if(orchestra==null||orchestra.seMixer_==null){Speech.Speak(I18n.T("ui.f7b9b8cd785e","二代音效系統尚未準備好。"));return;}
            // Use exactly the game's request, mixer, bank program, note transposition,
            // envelope and category. Raw ACB playback changes pitch/rate and is forbidden here.
            uint seIndex=HeroSongRules.PreviewSeIndex(previewIndex++);
            orchestra.playSEIndex(seIndex,1f,CriSoundManager.MixingCategory.MixingCategory_Hero,0f);
            Monitor.Record("hero-song-preview",new{seIndex,route="native-Orchestra.playSEIndex",level=Level});
        }catch(Exception e){Error(e);Speech.Speak(I18n.T("ui.2e7cbe3dcb68","目前無法試聽英雄模式歌聲。"));}
    }
}

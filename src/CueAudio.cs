using BepInEx.Configuration;
using System.Runtime.InteropServices;
namespace PataponAccess;
internal static class CueAudio
{
    private static readonly Dictionary<string,ConfigEntry<int>> levels=new();
    private static readonly Dictionary<string,string> rendered=new();
    private static ConfigFile? settings;
    [ThreadStatic] private static double? lastApiCallMs,lastApiReturnMs;
    public static double? LastApiCallMs=>lastApiCallMs;
    public static double? LastApiReturnMs=>lastApiReturnMs;
    public static long LastPlaybackAt {get;private set;}
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern bool PlaySound(string file,IntPtr module,uint flags);
    public static void Initialize(ConfigFile config)
    {
        settings=config;
        foreach(string name in new[]{"enemy-range","enemy-range-leave","object-range-enter","object-range-leave","cart-range-enter","cart-range-leave","boss-attack-start","vegetable-catch","vegetable-note","vegetable-rest","vegetable-rest-two","vegetable-rest-three","attack-ready","mechanism-near","mechanism-ready","miracle-ending","dance-rapid","dance-note"})
            levels[name]=config.Bind("CueVolume",name,10,new ConfigDescription("Individual cue volume, 0 silent, 10 original, 20 maximum. One step is 10 percent.",new AcceptableValueRange<int>(0,20)));
    }
    public static int Level(string file)=>levels.TryGetValue(Path.GetFileNameWithoutExtension(file),out var entry)?entry.Value:10;
    public static int Adjust(string file,int delta)
    {
        if(!levels.TryGetValue(Path.GetFileNameWithoutExtension(file),out var entry))return 10;
        entry.Value=Math.Clamp(entry.Value+delta,0,20);
        try{settings?.Save();}catch(Exception e){Reader.Log.LogWarning("Cue volume save: "+e.Message);}
        return entry.Value;
    }
    public static bool Play(string file)
    {
        bool success=PlayCore(file);
        if(success&&Level(file)>0)LastPlaybackAt=Environment.TickCount64;
        Monitor.Record("cue-playback",new{sound=Path.GetFileName(file),volume=Level(file),muted=Level(file)==0,success});
        return success;
    }
    public static void Prepare(string file)
    {
        try{ResolveFile(file);}catch(Exception e){Reader.Log.LogWarning("Cue audio preparation: "+e.Message);}
    }
    private static string ResolveFile(string file)
    {
            int level=Level(file);if(level is 0 or 10)return file;
            string key=file+":"+File.GetLastWriteTimeUtc(file).Ticks+":"+level;
            if(!rendered.TryGetValue(key,out var scaled)) {
                string folder=Path.Combine(BepInEx.Paths.BepInExRootPath,"cache","PataponAccess-audio");Directory.CreateDirectory(folder);
                string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
                scaled=Path.Combine(folder,Path.GetFileNameWithoutExtension(file)+"-"+hash.Substring(0,12)+"-"+level+".wav");
                if(!File.Exists(scaled))File.WriteAllBytes(scaled,WaveVolume.Scale(File.ReadAllBytes(file),level));
                rendered[key]=scaled;
            }
            return scaled;
    }
    private static bool PlayCore(string file)
    {
        lastApiCallMs=lastApiReturnMs=null;
        try {
            if(Level(file)==0)return true;
            string resolved=ResolveFile(file);
            lastApiCallMs=MinigameClock.NowMs;
            bool success=PlaySound(resolved,IntPtr.Zero,0x20000|1|2);
            lastApiReturnMs=MinigameClock.NowMs;
            return success;
        }
        catch(Exception e){Reader.Log.LogWarning("Cue audio: "+e.Message);return false;}
    }
}


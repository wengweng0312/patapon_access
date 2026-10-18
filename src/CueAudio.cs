using BepInEx.Configuration;
using System.Runtime.InteropServices;
namespace PataponAccess;
internal static class CueAudio
{
    private static readonly Dictionary<string,ConfigEntry<int>> levels=new();
    private static readonly Dictionary<string,string> rendered=new();
    private static ConfigFile? settings;
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern bool PlaySound(string file,IntPtr module,uint flags);
    public static void Initialize(ConfigFile config)
    {
        settings=config;
        foreach(string name in new[]{"enemy-range","enemy-range-leave","cart-range-enter","cart-range-leave","boss-attack-start","vegetable-catch","attack-ready"})
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
        Monitor.Record("cue-playback",new{sound=Path.GetFileName(file),volume=Level(file),muted=Level(file)==0,success});
        return success;
    }
    private static bool PlayCore(string file)
    {
        try {
            int level=Level(file);if(level==0)return true;
            if(level==10)return PlaySound(file,IntPtr.Zero,0x20000|1|2);
            string key=file+":"+File.GetLastWriteTimeUtc(file).Ticks+":"+level;
            if(!rendered.TryGetValue(key,out var scaled)) {
                string folder=Path.Combine(BepInEx.Paths.BepInExRootPath,"cache","PataponAccess-audio");Directory.CreateDirectory(folder);
                string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
                scaled=Path.Combine(folder,Path.GetFileNameWithoutExtension(file)+"-"+hash.Substring(0,12)+"-"+level+".wav");
                if(!File.Exists(scaled))File.WriteAllBytes(scaled,WaveVolume.Scale(File.ReadAllBytes(file),level));
                rendered[key]=scaled;
            }
            return PlaySound(scaled,IntPtr.Zero,0x20000|1|2);
        }catch(Exception e){Reader.Log.LogWarning("Cue audio: "+e.Message);return false;}
    }
}

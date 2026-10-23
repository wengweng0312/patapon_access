using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using System.Reflection;
namespace PataponAccess;
internal static class MapWeatherAccess
{
    [ThreadStatic] private static object? director;
    private static long lastError;
    private static object? Get(object? o,string name)=>o?.GetType().GetProperty(name)?.GetValue(o);
    private static IntPtr Ptr(object? o)=>o is Il2CppObjectBase native?native.Pointer:IntPtr.Zero;
    public static void Install(){
        var h=new Harmony("org.patapon.access.map-weather");
        foreach(string game in new[]{"P1","P2"})try{
            var t=typeof(LauncherMain).Assembly.GetType(game+".Bases.Camp.WorldMap.Director")!;
            h.Patch(t.GetMethod("update"),prefix:new HarmonyMethod(typeof(MapWeatherAccess).GetMethod(nameof(Enter),BindingFlags.Static|BindingFlags.NonPublic)),postfix:new HarmonyMethod(typeof(MapWeatherAccess).GetMethod(nameof(Leave),BindingFlags.Static|BindingFlags.NonPublic)),finalizer:new HarmonyMethod(typeof(MapWeatherAccess).GetMethod(nameof(Leave),BindingFlags.Static|BindingFlags.NonPublic)));
        }catch(Exception e){Reader.Log.LogWarning("Map weather hook: "+e.Message);}
    }
    private static void Enter(object __instance,out object? __state){__state=director;director=__instance;}
    private static void Leave(object? __state)=>director=__state;
    public static string Capture(object window){
        try{
            // Snapshot at the actual native map-window openW call. The cursor has
            // already moved, so this cannot attach the previous chip's forecast.
            var owner=director;
            if(owner==null||Get(owner,"isUpdate_") is not true||Ptr(Get(owner,"wnd_"))!=Ptr(window))return "";
            var scroll=Get(owner,"srl_");var mission=Get(scroll,"cursorMission_");var symbol=Get(scroll,"symbol_");
            if(mission==null||symbol==null)return "";
            var chips=Get(symbol,"chips_");if(chips==null)return "";
            int count=Convert.ToInt32(Get(chips,"Count"));if(count<1||count>256)return "";
            var item=chips.GetType().GetProperty("Item");
            for(int i=0;i<count;i++){
                var chip=item?.GetValue(chips,new object[]{i});
                if(chip==null||Get(chip,"hide_") is true||Get(chip,"enable_") is not true)continue;
                var labels=Get(chip,"misstionLabel_");if(labels==null)continue;
                int length=Convert.ToInt32(Get(labels,"Length"));if(length<1||length>32)continue;
                var element=labels.GetType().GetProperty("Item");bool selected=false;
                for(int j=0;j<length;j++)if(Ptr(element?.GetValue(labels,new object[]{j}))==Ptr(mission)){selected=true;break;}
                if(!selected)continue;
                uint weather=Convert.ToUInt32(Get(chip,"weather_"));string kind=MapWeatherText.Kind(weather);if(kind.Length==0)return "";
                string fallback=weather switch{0=>"無特殊天氣圖示",1=>"雨天",2=>"颳風",3=>"雷電",4=>"暴風雪",5=>"霧",_=>"沙塵暴"};
                string text=I18n.T("weather.caption","天氣：{0}",I18n.T("weather."+kind,fallback));
                Monitor.Record("map-weather-readout",new{game=owner.GetType().Namespace?.Split('.')[0],missionId=Get(mission,"id"),weather,kind,text,source="selected native map chip; verified graphical weather table",meaning="Accessibility description of the map icon, not a live battle-weather forecast."});
                return text;
            }
        }catch(Exception e){long now=Environment.TickCount64;if(now-lastError>5000){lastError=now;Monitor.Record("map-weather-error",new{error=e.Message});}}
        return "";
    }
}

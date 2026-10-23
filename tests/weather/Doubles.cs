using System.Reflection;
namespace Il2CppInterop.Runtime.InteropTypes { public class Il2CppObjectBase { public IntPtr Pointer {get;set;} } }
namespace HarmonyLib {
 public class Harmony {public Harmony(string name){} public void Patch(MethodInfo? m,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null,HarmonyMethod? finalizer=null){} }
 public class HarmonyMethod {public HarmonyMethod(MethodInfo? m){} }
}
namespace PataponAccess {
 internal static class Monitor {public static void Record(string kind,object data){} }
 internal static class I18n {public static string T(string key,string fallback,params object[] values)=>values.Length==0?fallback:string.Format(fallback,values);}
 internal static class Reader { public static DummyLog Log=new(); }
 internal class DummyLog {public void LogWarning(string message){} }
}
public class LauncherMain {}
public class Native:Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase {public Native(int id){Pointer=(IntPtr)id;} }
public class NativeArray<T>{private T[] values;public NativeArray(params T[] data){values=data;}public int Length=>values.Length;public T this[int index]=>values[index];}
public class Mission:Native {public Mission(int id):base(id){} public int id=>Pointer.ToInt32();}
public class Chip:Native {
 public Chip(int id,Mission mission,uint weather):base(id){misstionLabel_=new(mission);weather_=weather;}
 public NativeArray<Mission> misstionLabel_ {get;set;}
 public bool hide_ {get;set;}
 public bool enable_ {get;set;}=true;
 public uint weather_ {get;set;}
}
public class Symbol {public List<Chip> chips_ {get;set;}=new();}
public class Scroll {public Mission? cursorMission_ {get;set;}public Symbol symbol_ {get;set;}=new();}
public class Director:Native {
 public Director(int id):base(id){}
 public bool isUpdate_ {get;set;}=true;
 public Native wnd_ {get;set;}=new(99);
 public Scroll srl_ {get;set;}=new();
}

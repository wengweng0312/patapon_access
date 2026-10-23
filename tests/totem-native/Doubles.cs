using System.Reflection;
namespace HarmonyLib {
 public class Harmony {public Harmony(string id){}public void Patch(MethodInfo? m,HarmonyMethod? postfix=null){} }
 public class HarmonyMethod {public HarmonyMethod(MethodInfo? m){} }
}
namespace P2.Game.Talk {public class CommandGame {public void getFlagUnitPosX(){} } }
namespace P2.Pyd.Script.Talk {
 public class Value {public int i;}
 public class Variables {public Value[]? integerArray_;}
 public class Manager {public Variables? variable_;}
 public class Buffer {
  public IntPtr Pointer= (IntPtr)123;
  public byte[] Bytes=Array.Empty<byte>();
  public int Length=>Bytes.Length;
  public byte[] ToArray()=>(byte[])Bytes.Clone();
 }
 public class Context {public Buffer? buffer;}
 public class Controller {
  public Context[]? context_;
  public int contextIndex_;
  public Variables? variable_;
  public Manager? manager_;
  public IntPtr Pointer=(IntPtr)12;
 }
}
namespace PataponAccess {
 public static class BattleMonitor {public static bool Active=true;}
 public static class Reader {public static Logger Log=new();}
 public class Logger {public void LogWarning(string s){} }
 public static class Monitor {public static void Record(string kind,object data){} }
 public static class DrumPuzzleAccess {
  public static int accepted,attempts,lastStage;
  public static uint lastBody;
  public static bool ObserveNative(uint body,int stage,float min,float max,long now){attempts++;lastBody=body;lastStage=stage;if(body!=17)return false;accepted++;return true;}
 }
}

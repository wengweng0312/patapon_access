// Managed doubles: test locale routing without loading Unity or installing any detours.
namespace BepInEx.Configuration {
 public class ConfigEntry<T>{public T Value{get;set;} public ConfigEntry(T value){Value=value;}}
 public class ConfigDescription{public ConfigDescription(string text,object values){}}
 public class AcceptableValueList<T>{public AcceptableValueList(params T[] values){}}
 public class ConfigFile{
  public string Mode="auto";
  public ConfigEntry<T> Bind<T>(string section,string key,T value,ConfigDescription description)=>new((T)(object)Mode);
 }
}
namespace PataponAccess {
 internal static class Reader{public static FakeLog Log=new();}
 internal class FakeLog{public void LogWarning(string value){} public void LogInfo(string value){}}
 internal static class Monitor{public static void Record(string name,object value){}}
}
namespace P1.Localize {
 public enum LanguageType{LanguageType_tc,LanguageType_sc,LanguageType_us,LanguageType_jp,LanguageType_Current}
 public class Manager{public LanguageType languageType_;public byte[][]? msgPacArray_;}
}
namespace P2.Localize {
 public enum LanguageType{LanguageType_tc,LanguageType_sc,LanguageType_us,LanguageType_jp,LanguageType_Current}
 public class Manager{public LanguageType languageType_;}
}
namespace P1 {
 public class LaboCommon{
  public static LaboCommon? pLaboCommonInstance_g;
  public bool isSetupLocalizeResource_;
  public Localize.Manager? localizeManager_;
 }
}
namespace P2 {
 public class LaboCommon{
  public static LaboCommon? pLaboCommonInstance_g;
  public bool isSetupLocalizeResource_;
  public Localize.Manager? localizeManager_;
 }
}

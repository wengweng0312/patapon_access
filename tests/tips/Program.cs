using System.Reflection;
using PataponAccess;
int checks=0;void Check(bool ok,string m){checks++;if(!ok)throw new Exception(m);}
void Call(string method,params object[] args)=>typeof(NativeTipsAccess).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
var target=new P1.Bases.Camp.Scene{Pointer=(IntPtr)12,enableDrawTips_=true,isDrawnTips_=true};
var owner=new P1.Bases.Camp.Scene{Pointer=(IntPtr)34,localActiveScene_g=target};
P1.LaboCommon.pLaboCommonInstance_g=new(){getLocalizeDataItem=new(){imageData=new(){tipsText=new(){messages=new[]{"遊戲原文/第二行","另一則原文"}}}}};
Call("CampP1",target);Check(CommonUi.Output.Count==0,"do not guess unobserved tip ID");
Call("CampLoadedP1",owner,0,true);Call("CampP1",target);Check(CommonUi.Output.Single()=="遊戲原文/第二行","actual inline callback identifies target scene and outputs original game text");
Call("CampP1",target);Check(CommonUi.Output.Count==1,"same visible tip is announced only once");Check(!CommonUi.Interrupts.Last(),"reported result191208 then camp tips196445 must queue rather than interrupt");
Check(NativeTipsAccess.RepeatText=="遊戲原文/第二行","F3 original full text retained");
Call("CampLoadedP1",owner,1,false);Call("CampP1",target);Check(CommonUi.Output.Count==1,"failed callback cannot change selection");
Call("CampLoadedP1",owner,1,true);target.enableDrawTips_=false;Call("CampP1",target);Check(CommonUi.Output.Count==1,"loaded but hidden tip remains silent");
target.enableDrawTips_=true;Call("CampP1",target);Check(CommonUi.Output.Last()=="另一則原文","new visible tip read once");
P2.LaboCommon.pLaboCommonInstance_g=P1.LaboCommon.pLaboCommonInstance_g;
Call("CampP2",new P2.Bases.Camp.Scene{Pointer=(IntPtr)56,enableDrawTips_=true,isDrawnTips_=true,tipsId_=0});Check(CommonUi.Output.Last()=="遊戲原文/第二行","P2 existing field-based reader preserved");

var tipText=new string[25];tipText[16]="～狂熱３連發！ 箭士PON之卷～/箭士PON在FEVER狀態時才能顯出真價值！";
P1.LaboCommon.pLaboCommonInstance_g.getLocalizeDataItem!.imageData!.tipsText!.messages=tipText;
var displayed=new UnityEngine.RenderTexture{Pointer=(IntPtr)888};Call("TextureP1",16,displayed);
int beforeVisible=CommonUi.Output.Count;Check(beforeVisible==3,"texture creation alone is not spoken");
var actualScene=new P1.Bases.Camp.Scene{Pointer=(IntPtr)99,enableDrawTips_=true,isDrawnTips_=true,textureBundle_=new(){textureAry_=new(){new(){handle=displayed}}}};
Call("CampP1",actualScene);Check(CommonUi.Output.Last()==tipText[16],"real dev2 failure: visible texture16 resolves without ID callback");
Call("CampP1",actualScene);Check(CommonUi.Output.Count==beforeVisible+1,"texture-based result read once");
Call("P1",new P1.GameSystem.Tips.Viewer{state_=P1.GameSystem.Tips.Viewer.State.State_Select,selectId_=0,tipsAry_=new(){new(){isValid_=true,tipsId_=16}}});Check(CommonUi.Interrupts.Last(),"explicit viewer cursor remains immediate");
Console.WriteLine($"{checks} real-output Tips regressions passed; native runtime still requires gameplay.");
public class LauncherMain{}
namespace HarmonyLib{public class Harmony{public Harmony(string n){}public void Patch(MethodInfo? m,HarmonyMethod? postfix=null){}}public class HarmonyMethod{public HarmonyMethod(MethodInfo? m){}}}
namespace Il2CppInterop.Runtime.InteropTypes{public class Il2CppObjectBase{public IntPtr Pointer;}}
namespace PataponAccess{static class CommonUi{public static List<string> Output=new();public static List<bool> Interrupts=new();public static void QueueSelection(string s,bool interrupt=true){Output.Add(s);Interrupts.Add(interrupt);}}static class DialogueText{public static string Clean(string s)=>s??"";}static class Monitor{public static void RequestPage(string reason){}public static void Record(string kind,object data){}}static class Reader{public static Logger Log=new();}class Logger{public void LogWarning(string s){}}}
public class Localize{public Item? getLocalizeDataItem;public class Item{public Data? imageData;}public class Data{public Message? tipsText,tipsTitle;}public class Message{public string[]? messages;}}
namespace P1{public static class LaboCommon{public static Localize? pLaboCommonInstance_g;}}
namespace P2{public static class LaboCommon{public static Localize? pLaboCommonInstance_g;}}
namespace P1.Bases.Camp{public class Scene:Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase{public bool enableDrawTips_,isDrawnTips_;public Scene? localActiveScene_g;public Bundle? textureBundle_;public class Bundle{public List<Entry>? textureAry_;}public class Entry{public UnityEngine.RenderTexture? handle;}}}
namespace P2.Bases.Camp{public class Scene:Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase{public bool enableDrawTips_,isDrawnTips_;public int tipsId_;}}
namespace P1.GameSystem.Tips{public class Viewer{public bool isFinish_;public State state_;public List<Sprite>? tipsAry_;public uint selectId_;public enum State{State_Focus,State_Select}public class Sprite{public bool isValid_;public uint tipsId_;}}}
namespace P2.GameSystem.Tips{public class Viewer{public bool isFinish_;public State state_;public List<Sprite>? tipsAry_;public uint selectId_;public enum State{State_Focus,State_Select}public class Sprite{public bool isValid_;public uint tipsId_;}}}

namespace UnityEngine{public class RenderTexture{public IntPtr Pointer;public string name="";public int GetInstanceID()=>1;}}
namespace P1.GameSystem.Tips{public class TipsImage{public UnityEngine.RenderTexture? getTexture(int id)=>null;}}

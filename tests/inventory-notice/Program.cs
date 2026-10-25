using System.Reflection;
using PataponAccess;
int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
void Add(string method,object op,int id,int count)=>typeof(RewardAccess).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new[]{op,(object)id,count});
var gains=(Dictionary<(string,int),(uint,int,long)>)typeof(RewardAccess).GetField("gains",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
void Settle(){foreach(var k in gains.Keys.ToArray()){var g=gains[k];gains[k]=(g.Item1,g.Item2,Environment.TickCount64-600);}}
Add("Added",new P1.GameSystem.Item.Operator(),29,1);Settle();RewardAccess.Scan();
Check(Speech.Texts.Single().text=="物品入庫，軟綿綿的肉，1","actual reported P1 item29 count1 reaches output, not just a batch helper");Check(!Speech.Texts.Single().interrupt,"inventory notification queues");
RewardAccess.Scan();Check(Speech.Texts.Count==1,"read once");
MiniGameRewardAccess.HandlesCurrentGrant=true;Add("AddedP2",new P2.GameSystem.Item.Operator(),85,1);Check(gains.Count==0,"native mini-game reward is not duplicated by general notification");MiniGameRewardAccess.HandlesCurrentGrant=false;
MiningRewardAccess.HandlesCurrentGrant=true;Add("AddedP2",new P2.GameSystem.Item.Operator(),74,4);Check(gains.Count==0,"mining reward also excluded from duplicate announcement");MiningRewardAccess.HandlesCurrentGrant=false;
BattleMonitor.Active=true;Add("Added",new P1.GameSystem.Item.Operator(),29,3);Check(gains.Count==0,"battle grants retain original result route");BattleMonitor.Active=false;
Add("Added",new P1.GameSystem.Item.Operator(),29,0);Check(gains.Count==0,"zero/negative gains not announced");
Add("Added",new P1.GameSystem.Item.Operator(),29,2);Add("Added",new P1.GameSystem.Item.Operator(),29,3);Settle();RewardAccess.Scan();Check(Speech.Texts.Last().text.EndsWith("，5"),"actual consecutive grants sum once");
foreach(var lang in I18n.Languages){I18n.SetLanguage(lang);Check(I18n.T("reward.inventory","?","軟綿綿的肉，1").Contains("軟綿綿的肉，1"),"module prefix translated but original item name preserved");}
Console.WriteLine($"{checks} inventory notification output checks passed; native gameplay pending.");
namespace HarmonyLib{public class Harmony{public Harmony(string n){}public void Patch(MethodInfo? m,HarmonyMethod? postfix=null){}}public class HarmonyMethod{public HarmonyMethod(MethodInfo? m){}}}
namespace UnityEngine{public class Object{public static T[] FindObjectsOfType<T>()=>Array.Empty<T>();}}
namespace TMPro{public class TMP_Text{public bool isActiveAndEnabled=true;public int maxVisibleCharacters=1;public string text="";public object transform=new();public Color color=new();public static implicit operator bool(TMP_Text? t)=>t!=null;public class Color{public float a=1;}}}
namespace P1.GameSystem.Item{public class Operator{public Param getItemParam(int id)=>new(){nameId=229};public void addItem(int id,int count){}}public class Param{public uint nameId;}}
namespace P2.GameSystem.Item{public class Operator{public Param getItemParam(int id)=>new(){nameId=(uint)id};public void addItem(int id,int count){}}public class Param{public uint nameId;}}
namespace PataponAccess{
static class Monitor{public static void Record(string k,object d){}public static string PathOf(object t)=>"";}
static class BattleMonitor{public static bool Active;}
static class UnitNames{public static string FetchLocalized(string game,uint id)=>"軟綿綿的肉";}
static class Speech{public static List<(string text,bool interrupt)> Texts=new();public static void Speak(string t,bool interrupt)=>Texts.Add((t,interrupt));}
static class CommonUi{public static bool CommandWindowActive;}
static class P2TreasureAccess{public static bool IsActive;}
static class NativeResultAccess{public static RewardResultState State=new();public static void Install(){}public static void Reset()=>State.Reset();}
static class NativeResultGuideAccess{public static string Take()=>"";}
static class MiningRewardAccess{public static bool HandlesCurrentGrant;public static void Install(HarmonyLib.Harmony h){}public static void Added(uint n,int c){}public static void Scan(){}}
static class MiniGameRewardAccess{public static bool HandlesCurrentGrant;public static void Install(HarmonyLib.Harmony h){}public static void Added(uint n,int c){}public static void Scan(){}}
}

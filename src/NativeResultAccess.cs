using HarmonyLib;
using System.Reflection;
namespace PataponAccess;

internal static class NativeResultAccess
{
    internal static readonly RewardResultState State=new();
    private static long nextRead,lastError;
    public static void Reset(){State.Reset();nextRead=0;lastSnapshot="";}
    public static void Install()
    {
        var harmony=new Harmony("org.patapon.access.native-result");
        Hook(harmony,typeof(P1.Game.Result.Controller),nameof(P1));
        Hook(harmony,typeof(P2.Game.Result.Single.Controller),nameof(P2));
    }
    private static void Hook(Harmony harmony,Type type,string callback)
    {
        try {
            // Substantial native routines with uint/bool arguments, inspected before hooking.
            // Display.update has a by-ref argument and is deliberately not patched.
            var method=type.GetMethod("updateDisplay",new[]{typeof(uint),typeof(bool)})??throw new MissingMethodException(type.FullName,"updateDisplay");
            harmony.Patch(method,postfix:new HarmonyMethod(typeof(NativeResultAccess).GetMethod(callback,BindingFlags.Static|BindingFlags.NonPublic)));
            Monitor.Record("native-result-hook",new{type=type.FullName,method=method.Name,installed=true});
        }catch(Exception e){Error(e);}
    }
    private static bool Due(bool initial)
    {
        if(initial)Reset();
        long now=Environment.TickCount64;if(now<nextRead)return false;nextRead=now+100;return true;
    }
    private static void P1(global::P1.Game.Result.Controller __instance,bool __1)
    {
        try {
            if(!Due(__1))return;
            var display=__instance.display_;if(display==null||!display.isStart_||display.isEnd_)return;
            var rows=display.infoItemList_;if(rows==null||rows.Count>512)return;
            var names=new List<string>();int itemRows=0,unresolved=0;
            foreach(var row in rows){
                var item=row?.TryCast<global::P1.Game.Result.ItemInfoItem>();if(item==null)continue;itemRows++;
                string name=DialogueText.Clean(item.nameMessage_?.getDrawInfoMessage()??"");
                if(name.Length>0)names.Add(name);else unresolved++;
            }
            Observe("P1:"+display.Pointer,names,display.isControllable_&&unresolved==0,itemRows,unresolved);
        }catch(Exception e){Error(e);}
    }
    private static void P2(global::P2.Game.Result.Single.Controller __instance,bool __1)
    {
        try {
            if(!Due(__1))return;
            var display=__instance.display_;if(display==null||!display.isStart_||display.isEnd_)return;
            var rows=display.infoItemList_;if(rows==null||rows.Count>512)return;
            var names=new List<string>();int itemRows=0,unresolved=0;
            foreach(var row in rows){
                var item=row?.TryCast<global::P2.Game.Result.ItemInfoItem>();if(item==null)continue;itemRows++;
                string name=DialogueText.Clean(item.nameMessage_?.getDrawInfoMessage()??"");
                if(name.Length>0)names.Add(name);else unresolved++;
            }
            Observe("P2:"+display.Pointer,names,display.isControllable_&&unresolved==0,itemRows,unresolved);
        }catch(Exception e){Error(e);}
    }
    private static string lastSnapshot="";
    private static void Observe(string owner,List<string> names,bool ready,int itemRows,int unresolved)
    {
        State.Observe(owner,names,ready,Environment.TickCount64);
        string snapshot=owner+"|"+ready+"|"+unresolved+"|"+string.Join("\u001f",names);
        if(snapshot==lastSnapshot)return;lastSnapshot=snapshot;
        Monitor.Record("native-result-rows",new{owner,items=names.ToArray(),itemRows,unresolved,ready,countsKnown=false,source="Result.Display.infoItemList.ItemInfoItem.nameMessage"});
    }
    private static void Error(Exception e)
    {
        long now=Environment.TickCount64;if(now-lastError<5000)return;lastError=now;
        Monitor.Record("native-result-error",new{error=e.ToString()});
    }
}

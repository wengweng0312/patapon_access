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
            NativeResultGuideAccess.Observe(display.keyGuide_);
            var rows=display.infoItemList_;if(rows==null||rows.Count>512)return;
            var names=new List<string>();var itemIndexes=new List<int>();var uniqueRows=new HashSet<IntPtr>();int itemRows=0,unresolved=0;
            foreach(var row in rows){
                if(row==null||!uniqueRows.Add(row.Pointer))continue;
                void Add(string? value){string text=DialogueText.Clean(value??"");if(text.Length>0)names.Add(text);}
                Add(row.chapterMessage_?.getDrawInfoMessage());
                var item=row.TryCast<global::P1.Game.Result.ItemInfoItem>();
                if(item!=null){itemRows++;string value=item.nameMessage_?.getDrawInfoMessage()??"";if(!string.IsNullOrWhiteSpace(value))itemIndexes.Add(names.Count);Add(value);if(string.IsNullOrWhiteSpace(value))unresolved++;}
                var script=row.TryCast<global::P1.Game.Result.ScriptInfoItem>();
                if(script?.messageArray_!=null)foreach(var part in script.messageArray_)Add(part?.msg?.getDrawInfoMessage());
            }
            Observe("P1:"+display.Pointer,names,display.isControllable_&&unresolved==0,itemRows,unresolved,itemIndexes);
        }catch(Exception e){Error(e);}
    }
    private static void P2(global::P2.Game.Result.Single.Controller __instance,bool __1)
    {
        try {
            if(!Due(__1))return;
            var display=__instance.display_;if(display==null||!display.isStart_||display.isEnd_)return;
            NativeResultGuideAccess.Observe(display.keyGuide_);
            var rows=display.infoItemList_;if(rows==null||rows.Count>512)return;
            var names=new List<string>();var itemIndexes=new List<int>();var uniqueRows=new HashSet<IntPtr>();int itemRows=0,unresolved=0;
            foreach(var row in rows){
                if(row==null||!uniqueRows.Add(row.Pointer))continue;
                void Add(string? value){string text=DialogueText.Clean(value??"");if(text.Length>0)names.Add(text);}
                Add(row.chapterMessage_?.getDrawInfoMessage());
                var item=row.TryCast<global::P2.Game.Result.ItemInfoItem>();
                if(item!=null){itemRows++;string value=item.nameMessage_?.getDrawInfoMessage()??"";if(!string.IsNullOrWhiteSpace(value))itemIndexes.Add(names.Count);Add(value);if(string.IsNullOrWhiteSpace(value))unresolved++;}
                var script=row.TryCast<global::P2.Game.Result.ScriptInfoItem>();
                if(script?.messageArray_!=null)foreach(var part in script.messageArray_)Add(part?.msg?.getDrawInfoMessage());
                var injury=row.TryCast<global::P2.Game.Result.InjuryInfoItem>();if(injury!=null)Add(injury.message_?.getDrawInfoMessage());
            }
            Observe("P2:"+display.Pointer,names,display.isControllable_&&unresolved==0,itemRows,unresolved,itemIndexes);
        }catch(Exception e){Error(e);}
    }
    private static string lastSnapshot="";
    private static void Observe(string owner,List<string> names,bool ready,int itemRows,int unresolved,List<int> itemIndexes)
    {
        State.Observe(owner,names,ready,Environment.TickCount64,itemIndexes);
        string snapshot=owner+"|"+ready+"|"+unresolved+"|"+string.Join("\u001f",names);
        if(snapshot==lastSnapshot)return;lastSnapshot=snapshot;
        Monitor.Record("native-result-rows",new{owner,items=names.ToArray(),itemIndexes=itemIndexes.ToArray(),itemRows,unresolved,ready,countsKnown=false,source="Result.Display.infoItemList: chapterMessage + ScriptInfoItem.messageArray + ItemInfoItem.nameMessage + P2 injury"});
    }
    private static void Error(Exception e)
    {
        long now=Environment.TickCount64;if(now-lastError<5000)return;lastError=now;
        Monitor.Record("native-result-error",new{error=e.ToString()});
    }
}

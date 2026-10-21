namespace PataponAccess;
// Diagnostic snapshots only: never infer interaction, HP, loot or a visible name.
internal static class WorldObjectLog
{
    private static bool enabled;
    private static readonly Dictionary<(string,IntPtr),(long at,string state)> objects=new();
    private static int emitted;
    public static void Configure(bool value){enabled=value;Reset();}
    public static void Reset(){objects.Clear();emitted=0;}
    private static bool Reserve(string game,IntPtr id,long now)
    {
        if(!enabled||!BattleMonitor.Active||emitted>=256)return false;
        if(objects.TryGetValue((game,id),out var old)){if(now-old.at<500)return false;objects[(game,id)]=(now,old.state);}
        else{if(objects.Count>=128)return false;objects[(game,id)]=(now,"");}
        return true;
    }
    private static void Write(string game,IntPtr id,uint layoutId,string parameter,string name,bool active,bool viewable,bool hit,bool inside,int targetType,int targetId)
    {
        var key=(game,id);var old=objects[key];
        string state=$"{parameter}:{name}:{active}:{viewable}:{hit}:{inside}:{targetType}:{targetId}";
        if(state==old.state)return;objects[key]=(old.at,state);emitted++;
        Monitor.Record("world-object-state",new{game,instance=id.ToInt64(),layoutId,parameter,name,active,viewable,hit,inside,targetType,targetId,meaning="Native raw scenery identity/state, not confirmed interactability, visibility or destructibility."});
    }
    public static void Observe(P1.Game.Gimmick.Gimmick obj)
    {
        if(!Reserve("P1",obj.Pointer,Environment.TickCount64))return;
        try{var layout=obj.layoutParam_;Write("P1",obj.Pointer,obj.id_,layout?.paramName??"","",obj.isEnable_,obj.isViewable_,obj.isHit_,obj.isInnerTarget_,obj.prevTargetType_,obj.prevTargetId_);}
        catch(Exception e){Error("P1",e);}
    }
    public static void Observe(P2.Game.Gimmick.Gimmick obj)
    {
        if(!Reserve("P2",obj.Pointer,Environment.TickCount64))return;
        try{var layout=obj.layoutParam_;Write("P2",obj.Pointer,obj.id_,layout?.paramName??"",layout?.name??"",obj.isEnable_,obj.isViewable_,obj.isHit_,obj.isInnerTarget_,obj.prevTargetType_,obj.prevTargetId_);}
        catch(Exception e){Error("P2",e);}
    }
    public static void Script(P2.Game.Gimmick.Gimmick obj,int label,bool result)
    {
        if(!enabled||!BattleMonitor.Active||emitted>=256)return;
        // Record labels only for objects already sampled; do not walk script buffers.
        if(!objects.ContainsKey(("P2",obj.Pointer)))return;
        emitted++;Monitor.Record("world-object-script",new{game="P2",instance=obj.Pointer.ToInt64(),scriptLabel=label,result});
    }
    private static void Error(string game,Exception error){enabled=false;Monitor.Record("world-object-error",new{game,error=error.Message,observationDisabled=true});}
}

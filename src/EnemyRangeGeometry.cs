using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class EnemyRangeGeometry
{
    private static bool active;
    private static readonly Dictionary<IntPtr,(float x,float y,long seen)> enemies=new();
    private static readonly Dictionary<IntPtr,(CartBox box,bool collision,long seen)> views=new();
    private static readonly Dictionary<IntPtr,(string name,CartBox box,long seen)> special=new();
    private static long nextLog;
    private static bool? last;
    public static void Install()
    {
        try{new Harmony("org.patapon.access.enemy-position").Patch(typeof(P1.Game.Unit.UnitBase).GetMethod("update"),postfix:new HarmonyMethod(typeof(EnemyRangeGeometry).GetMethod(nameof(ObserveEnemy),BindingFlags.NonPublic|BindingFlags.Static)));}
        catch(Exception e){Reader.Log.LogWarning("Enemy position hook unavailable: "+e.Message);}
    }
    public static void Begin(){active=true;enemies.Clear();views.Clear();special.Clear();last=null;nextLog=0;}
    public static void End(){active=false;enemies.Clear();views.Clear();special.Clear();}
    public static void SpecialTarget(IntPtr key,string name,CartBox box)
    {
        if(!active||!box.Valid)return;
        if(!special.ContainsKey(key))Monitor.Record("special-boss-target",new{name,box});
        special[key]=(name,box,Environment.TickCount64);
    }
    public static void RemoveSpecial(IntPtr key)=>special.Remove(key);
    private static void ObserveEnemy(P1.Game.Unit.UnitBase __instance)
    {
        if(!active)return;
        try {
            var key=__instance.Pointer;
            if(!__instance.isActive_||__instance.isDead_){enemies.Remove(key);return;}
            var param=__instance.getCharaParam();
            if(param==null||param.troopType!=(int)P1.Game.Unit.TroopType.TroopType_Enemy)return;
            var model=__instance.unitModel_;if(model==null)return;
            var pos=model.pos_;
            if(!float.IsFinite(pos.x)||!float.IsFinite(pos.y))return;
            enemies[key]=(pos.x,pos.y,Environment.TickCount64);
        }catch(Exception e){if(Environment.TickCount64>=nextLog){nextLog=Environment.TickCount64+5000;Monitor.Record("enemy-position-error",new{error=e.Message});}}
    }
    public static void View(IntPtr key,CartBox box,bool collision)
    {if(active&&box.Valid)views[key]=(box,collision,Environment.TickCount64);}
    public static bool Inside(long now)
    {
        if(!active)return false;
        foreach(var key in enemies.Where(e=>now-e.Value.seen>600).Select(e=>e.Key).ToArray())enemies.Remove(key);
        foreach(var key in views.Where(e=>now-e.Value.seen>600).Select(e=>e.Key).ToArray())views.Remove(key);
        foreach(var key in special.Where(e=>now-e.Value.seen>600).Select(e=>e.Key).ToArray())special.Remove(key);
        bool inside=enemies.Values.Any(e=>views.Values.Any(v=>v.box.ContainsX(e.x)&&(!v.collision||(e.y>=v.box.Bottom&&e.y<=v.box.Top))))||
            special.Values.Any(e=>views.Values.Any(v=>SpecialBossRules.InRange(v.box,e.box,v.collision)));
        if(last!=inside||now>=nextLog){last=inside;nextLog=now+1000;Monitor.Record("enemy-position-range",new{inside,enemies=enemies.Values.Select(e=>new{e.x,e.y}).ToArray(),special=special.Values.Select(e=>new{e.name,e.box}).ToArray(),views=views.Values.Select(v=>v.box).ToArray(),meaning="enemy origin or identified hostile object bounds within native attack view, not hit confirmation",experimental=true});}
        return inside;
    }
}

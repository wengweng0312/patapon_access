using HarmonyLib;
using System.Reflection;
using System.Runtime.InteropServices;
using P1.Game.Unit;

namespace PataponAccess;

internal static class CartRange
{
    private record Cart(float X,CartBox Box,long Seen,string Name);
    private record View(CartBox Box,bool Collision,long Seen);
    private static readonly Dictionary<IntPtr,Cart> carts=new();
    private static readonly Dictionary<IntPtr,View> views=new();
    private static readonly Dictionary<int,float> ranges=new();
    private static readonly Dictionary<IntPtr,long> nextCart=new(),nextView=new();
    private static CartRangeLatch latch=new();
    private static bool active,enabled,failed;
    private static long lastLog,nextScan,queuedAt;
    private static int queuedDirection;
    private static string last="";
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern bool PlaySound(string path,IntPtr module,uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    public static void Install(bool value)
    {
        enabled=value;
        var h=new Harmony("org.patapon.access.cart-range");
        Patch(h,typeof(P1.Game.Gimmick.Gimmick),"update",nameof(CartUpdate));
        Patch(h,typeof(UnitSquad),"isInsideView",nameof(PointView));
        Patch(h,typeof(UnitSquad),"isInsideView_Col",nameof(CollisionView));
    }
    private static void Patch(Harmony h,Type type,string name,string callback)
    {
        try {
            h.Patch(type.GetMethod(name),postfix:new HarmonyMethod(typeof(CartRange).GetMethod(callback,BindingFlags.Static|BindingFlags.NonPublic)));
            Monitor.Record("cart-hook-installed",new{method=type.FullName+"."+name});
        }catch(Exception e){Fail(e);}
    }
    public static void Begin()
    {
        active=true;carts.Clear();views.Clear();ranges.Clear();nextCart.Clear();nextView.Clear();latch=new();last="";nextScan=0;queuedDirection=0;
    }
    public static void End(){active=false;carts.Clear();views.Clear();latch=new();queuedDirection=0;}
    private static void Fail(Exception e)
    {
        failed=true;Monitor.Record("cart-range-disabled",new{error=e.Message});
        Reader.Log.LogWarning("Cart range disabled for this run: "+e.Message);
    }
    private static void CartUpdate(P1.Game.Gimmick.Gimmick __instance)
    {
        SpecialBossAccess.ObserveTarget(__instance);
        if(!active||failed)return;
        try {
            var key=__instance.Pointer;long now=Environment.TickCount64;
            if(nextCart.TryGetValue(key,out var next)&&now<next)return;nextCart[key]=now+100;
            if(!__instance.isActive()||!__instance.checkCollisionEnable()){carts.Remove(key);return;}
            var param=__instance.getParam();
            if(param==null)return;
            // Enemy wagon variants only: never the prisoner MEDEN or friendly cart.
            if(param.name is not ("EN_VOITURE_000" or "EN_VOITURE_001" or "EN_VOITURE_002"))return;
            var body=__instance.getCollisionBody();
            if(body==null||!body.isEnable()){carts.Remove(key);return;}
            var p=__instance.getPosition();var b=body.shapeBuffer_.bbox;
            var box=new CartBox(b.min.x,b.min.y,b.max.x,b.max.y);
            if(!box.Valid)return;
            if(!carts.ContainsKey(key))Monitor.Record("cart-identified",new{name=param.name,param.id,instance=key.ToInt64()});
            carts[key]=new Cart(p.x,box,now,param.name);
        }catch(Exception e){Fail(e);}
    }
    private static void PointView(UnitSquad __instance,SquadCtrl.SquadCtrlType __0,int __1,bool __result)
    {DirectEnemySight.Observe(__instance,__0,__1,false,__result);CaptureView(__instance,__0,__1,false);}
    private static void CollisionView(UnitSquad __instance,SquadCtrl.SquadCtrlType __0,int __1,bool __result)
    {DirectEnemySight.Observe(__instance,__0,__1,true,__result);CaptureView(__instance,__0,__1,true);}
    private static void CaptureView(UnitSquad squad,SquadCtrl.SquadCtrlType ctrl,int target,bool collision)
    {
        if(!active||failed||(target!=3&&target!=5)||ctrl!=SquadCtrl.SquadCtrlType.SquadCtrlType_Attack)return;
        try {
            long now=Environment.TickCount64;var key=squad.Pointer;
            // Observe only a squad currently executing the game's sight query.
            if(!squad.isActive_)return;
            var troop=squad.pUnitTroop_;
            if(troop?.troopInfo_?.troopType!=TroopType.TroopType_Player)return;
            if(nextView.TryGetValue(key,out long next)&&now<next)return;nextView[key]=now+100;
            var control=squad.squadCtrl_?.tpdSquadCtrlTarget_;
            var ids=control?.targetParam?.aSquadCtrlId;
            if(ids==null||(int)ctrl<0||(int)ctrl>=ids.Length)return;
            int id=ids[(int)ctrl];if(id<0)return;
            if(!ranges.TryGetValue(id,out float range)) {
                var media=P1.Media.MediaManager.Instance;
                var table=media?.LoadingGroupData?.gameData?.paramList?.squadActivityParam?.baseParam;
                if(table==null||id>=table.Count)return;
                var activity=table[id]?.activityParam;if(activity==null)return;
                // Same formula as native UnitSquad.isInsideView[_Col]:
                // (attackRange + attackMoveRange) * 15, in game coordinates.
                range=activity.getViewRnage()*15f;
                if(!float.IsFinite(range)||range<=0||range>100000)return;
                ranges[id]=range;
            }
            CartBox box;
            if(collision) {
                if(squad.testBody==null)return;
                var b=squad.testBody.shapeBuffer_.bbox;
                box=new CartBox(b.min.x,b.min.y,b.max.x,b.max.y);
            }else {
                var pos=troop.getTroopBasePos();if(pos==null||pos.Length==0)return;
                float x=pos[0];float end=x+(troop.troopInfo_.directionType==DirectionType.DirectionType_Left?-range:range);
                box=new CartBox(Math.Min(x,end),-100000,Math.Max(x,end),100000);
            }
            if(!box.Valid)return;
            EnemyRangeGeometry.View(key,box,collision);
            views[key]=new View(box,collision,now);
        }catch(Exception e){Fail(e);}
    }
    public static void Scan()
    {
        if(!active||failed)return;
        long now=Environment.TickCount64;PlayPending(now);if(now<nextScan)return;nextScan=now+100;
        foreach(var key in carts.Where(p=>now-p.Value.Seen>600).Select(p=>p.Key).ToArray())carts.Remove(key);
        foreach(var key in views.Where(p=>now-p.Value.Seen>600).Select(p=>p.Key).ToArray())views.Remove(key);
        // Missing squad observations mean unknown, never a manufactured range exit.
        if(views.Count==0){latch.Invalidate();return;}
        bool inside=carts.Values.Any(c=>views.Values.Any(v=>v.Collision?v.Box.Overlaps(c.Box):v.Box.ContainsX(c.X)));
        string status=carts.Count+":"+views.Count+":"+inside;
        if(status!=last||now-lastLog>=1000) {
            last=status;lastLog=now;
            Monitor.Record("cart-range-state",new{inside,carts=carts.Values.Select(c=>new{c.Name,c.X,c.Box}).ToArray(),views=views.Values.Select(v=>new{v.Box,v.Collision}).ToArray(),experimental=true});
        }
        int direction=latch.Observe(inside,now);if(direction==0)return;
        queuedDirection=direction;queuedAt=now;
    }
    private static void PlayPending(long now)
    {
        if(queuedDirection==0)return;
        if(now-queuedAt>1000){queuedDirection=0;return;}
        if(now-RangeCue.LastPlaybackAt<240)return;
        int direction=queuedDirection;queuedDirection=0;
        GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,direction>0?"cart-range-enter.wav":"cart-range-leave.wav");
        bool played=enabled&&pid==Environment.ProcessId&&File.Exists(file)&&CueAudio.Play(file);
        Monitor.Record("cart-range-cue",new{direction=direction>0?"enter":"leave",played,experimental=true});
    }
}


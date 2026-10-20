using HarmonyLib;
using System.Reflection;

namespace PataponAccess;

internal static class SpecialBossAccess
{
    private static readonly Dictionary<IntPtr,long> next=new();
    private static long lastError;
    public static void Install()
    {
        try {
            new Harmony("org.patapon.access.special-boss").Patch(typeof(P1.Game.Gimmick.Model).GetMethod("playMotion"),
                postfix:new HarmonyMethod(typeof(SpecialBossAccess).GetMethod(nameof(Motion),BindingFlags.Static|BindingFlags.NonPublic)));
            Monitor.Record("special-boss-observer",new{target="KARMA",model="gimc530",scope="gimmick target bounds and verified preparation motions"});
        }catch(Exception e){Reader.Log.LogWarning("Special boss observer unavailable: "+e.Message);}
    }
    public static void ObserveTarget(P1.Game.Gimmick.Gimmick owner)
    {
        if(!BattleMonitor.Active)return;
        try {
            long now=Environment.TickCount64;var key=owner.Pointer;
            if(next.TryGetValue(key,out long due)&&now<due)return;
            if(next.Count>512)next.Clear();next[key]=now+100;
            var param=owner.getParam();if(param==null||!SpecialBossRules.IsTarget(param.name??""))return;
            if(!owner.isActive()||!owner.checkCollisionEnable()||owner.getHitPoint()<=0){EnemyRangeGeometry.RemoveSpecial(key);return;}
            var body=owner.getCollisionBody();if(body==null||!body.isEnable()){EnemyRangeGeometry.RemoveSpecial(key);return;}
            var bounds=body.shapeBuffer_.bbox;
            EnemyRangeGeometry.SpecialTarget(key,param.name??"",new CartBox(bounds.min.x,bounds.min.y,bounds.max.x,bounds.max.y));
        }catch(Exception e){Error(e);}
    }
    private static void Motion(P1.Game.Gimmick.Model __instance,uint __0,float __1,bool __2,bool __3)
    {
        if(!BattleMonitor.Active)return;
        try {
            string model=__instance.modelDesc_?.mainModelName??"";
            if(model!="gimc530")return;
            bool preparation=SpecialBossRules.IsPreparation(model,__0,__1,__2);
            Monitor.Record("special-boss-motion",new{target="KARMA",model,motion=__0,delay=__1,loop=__2,clearQueue=__3,preparation});
            if(preparation)BossAttackCue.Special(__instance.Pointer,"KARMA",(int)__0,"karma-script-preparation");
        }catch(Exception e){Error(e);}
    }
    private static void Error(Exception e)
    {
        long now=Environment.TickCount64;if(now-lastError<5000)return;lastError=now;
        Monitor.Record("special-boss-error",new{error=e.Message});
    }
}

namespace PataponAccess;

internal static class SpecialBossRules
{
    // P1 mission 0140/0150: the hostile KARMA object uses model gimc530.
    public static bool IsTarget(string name)=>name=="KARMA";
    public static bool InRange(CartBox view,CartBox target,bool collision)=>view.Valid&&target.Valid&&
        (collision?view.Overlaps(target):view.Left<=target.Right&&view.Right>=target.Left);
    public static bool IsPreparation(string model,uint motion,float delay,bool loop)=>
        model=="gimc530"&&!loop&&float.IsFinite(delay)&&
        ((motion==9&&delay==0)||(motion==7&&delay>0));
}

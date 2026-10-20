namespace PataponAccess;

// Native attack sight flags, not a search of rendered scenery. Keep P1 policy separate.
internal static class P2RangeTargets
{
    public static bool Inside(int enemySquads,int gimmickSquads)
        =>enemySquads is >=0 and <=4&&gimmickSquads is >=0 and <=4
          &&(enemySquads>0||gimmickSquads>0);
}

namespace PataponAccess;

internal readonly record struct CartBox(float Left,float Bottom,float Right,float Top)
{
    public bool Valid=>float.IsFinite(Left)&&float.IsFinite(Bottom)&&float.IsFinite(Right)&&float.IsFinite(Top)&&Left<=Right&&Bottom<=Top;
    public bool ContainsX(float x)=>Valid&&float.IsFinite(x)&&x>=Left&&x<=Right;
    public bool Overlaps(CartBox b)=>Valid&&b.Valid&&Left<=b.Right&&Right>=b.Left&&Bottom<=b.Top&&Top>=b.Bottom;
}

internal sealed class CartRangeLatch
{
    public bool Inside {get;private set;}
    private long? since;
    public void Invalidate()=>since=null;
    public int Observe(bool value,long now)
    {
        if(value==Inside){since=null;return 0;}
        since??=now;
        if(now-since.Value<(value?200:500))return 0;
        Inside=value;since=null;return value?1:-1;
    }
}

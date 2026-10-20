namespace PataponAccess;
internal sealed class MiracleThreshold
{
    private float previous=float.NaN;
    private bool warned;
    public void Reset(){previous=float.NaN;warned=false;}
    public bool Observe(bool active,float remaining)
    {
        if(!active||!float.IsFinite(remaining)||remaining<=0){Reset();return false;}
        bool fire=!warned&&float.IsFinite(previous)&&previous>10&&remaining<=10;
        previous=remaining;
        if(fire)warned=true;
        return fire;
    }
}

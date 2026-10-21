namespace PataponAccess;
// Pure native-flag debounce; separate instances must be used for separate roles.
internal sealed class RangeTransition
{
    public bool Inside {get;private set;}
    private long? positive,negative;
    public void Reset(){Inside=false;positive=negative=null;}
    public int Observe(bool value,long now)
    {
        if(value){negative=null;positive??=now;if(!Inside&&now-positive.Value>=200){Inside=true;return 1;}}
        else{positive=null;negative??=now;if(Inside&&now-negative.Value>=500){Inside=false;return -1;}}
        return 0;
    }
}

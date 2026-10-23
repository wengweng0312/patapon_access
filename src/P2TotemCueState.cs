namespace PataponAccess;
internal enum TotemCue { None,Ahead,Ready }
internal sealed class P2TotemCueState
{
    // One native interaction-window width before its far edge; an early notice,
    // not a claim that input is accepted. Keep distinct from native limits.
    public const float PreviewMargin=150;
    private readonly DrumPuzzleState glyphs=new();
    private long bodyAt,playerAt,nativeAt,aheadAt;
    private float bodyX,flagX,minimum,maximum;private uint bodyId;
    private bool enabled,hasBody,hasPlayer,ahead,ready;
    private int stage;private string readySequence="";
    public void Clear(){glyphs.Clear();bodyAt=playerAt=nativeAt=aheadAt=0;enabled=hasBody=hasPlayer=ahead=ready=false;readySequence="";}
    public void Observe(string name,bool value,float x,float y,uint id,long now){
        glyphs.Observe(name,value,x,y,now);
        if(name!="OEP_0_BODY")return;
        if(!hasBody||bodyId!=id||bodyX!=x){ahead=ready=false;readySequence="";nativeAt=0;}
        hasBody=true;bodyId=id;bodyX=x;bodyAt=now;enabled=value&&float.IsFinite(x)&&float.IsFinite(y)&&x>=0;
    }
    public void ObservePlayer(float flag,float front,long now){
        glyphs.ObservePlayer(flag,front,now);
        if(!float.IsFinite(flag)||!float.IsFinite(front)||front<flag)return;
        flagX=flag;playerAt=now;hasPlayer=true;
    }
    public bool ObserveNative(uint id,int value,float min,float max,long now){
        if(!hasBody||id!=bodyId||!float.IsFinite(min)||!float.IsFinite(max)||min<=0||max<=min)return false;
        stage=value;minimum=min;maximum=max;nativeAt=now;return true;
    }
    public float? FlagDistance=>hasBody&&hasPlayer?bodyX-flagX:null;
    public bool HasEncounter=>ahead;
    private bool Fresh(long now)=>hasBody&&hasPlayer&&enabled&&now-bodyAt<=600&&now-playerAt<=600&&glyphs.Current.Length>0;
    private bool NativeFresh(long now)=>nativeAt>0&&now-nativeAt<=600;
    public string CurrentAt(long now)=>Fresh(now)&&NativeFresh(now)&&stage is >=1 and <=5&&FlagDistance>=minimum&&FlagDistance<=maximum?glyphs.Current:"";
    public TotemCue Take(long now){
        if(!Fresh(now))return TotemCue.None;
        float distance=FlagDistance!.Value;
        // Do not infer either boundary from the old approach constant.
        if(!NativeFresh(now))return TotemCue.None;
        if(distance>maximum+PreviewMargin+50){ahead=ready=false;readySequence="";return TotemCue.None;}
        if(distance<minimum||stage is <1 or >5)return TotemCue.None;
        if(!ahead&&distance<=maximum+PreviewMargin){ahead=true;aheadAt=now;return TotemCue.Ahead;}
        // Only announce at the start of a pattern; never distract later hits.
        string sequence=CurrentAt(now);
        if(ahead&&stage is 1 or 2&&sequence.Length>0&&(!ready||readySequence!=sequence)&&now-aheadAt>=650){ready=true;readySequence=sequence;return TotemCue.Ready;}
        return TotemCue.None;
    }
}

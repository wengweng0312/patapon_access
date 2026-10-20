namespace PataponAccess;
internal sealed class DrumPuzzleState
{
    private readonly Dictionary<string,(bool enabled,float x,float y)> states=new();
    private string candidate="",announced="";private long changedAt,playerAt,bodyAt,nearSince;
    private float playerX,frontX;private bool hasPlayer,near;
    // Warn from the native troop front, before the flag bearer reaches the
    // OEP script dismissal boundary. Neither value confirms accepted rhythm input.
    public const float ApproachDistance=150;
    public const float DismissDistance=150;
    public void Clear(){states.Clear();candidate=announced="";changedAt=playerAt=bodyAt=nearSince=0;hasPlayer=near=false;}
    public void ObservePlayer(float flag,float front,long now){if(!float.IsFinite(flag)||!float.IsFinite(front)||front<flag)return;playerX=flag;frontX=front;playerAt=now;hasPlayer=true;}
    public void Observe(string name,bool enabled,float x,float y,long now)
    {
        if(!MechanismNames.IsDrumPuzzle(name))return;
        states[name]=(enabled,x,y);if(name=="OEP_0_BODY")bodyAt=now;
        string next=Sequence();if(next!=candidate){candidate=next;changedAt=now;}
    }
    public void ObserveBody(bool enabled,float x,float y,string sequence,long now)
    {
        states["OEP_0_BODY"]=(enabled,x,y);bodyAt=now;
        string next=enabled&&float.IsFinite(x)&&float.IsFinite(y)&&x>=0?sequence:"";
        if(next!=candidate){candidate=next;changedAt=now;}
    }
    private string Sequence()
    {
        if(!states.TryGetValue("OEP_0_BODY",out var body)||!body.enabled||!float.IsFinite(body.x)||body.x<0)return "";
        var drums=new List<string>();
        for(int slot=1;slot<=4;slot++){
            var found=new List<string>();
            foreach(string drum in new[]{"PON","PATA","CHAKA","DON"})
                if(states.TryGetValue($"OEP_{slot}_{drum}",out var s)&&s.enabled&&Math.Abs(s.x-body.x)<1&&Math.Abs(s.y-body.y)<1)found.Add(drum);
            if(found.Count!=1)return "";drums.Add(found[0]);
        }
        return string.Join(" ",drums);
    }
    public float? Distance=>hasPlayer&&states.TryGetValue("OEP_0_BODY",out var body)?body.x-frontX:null;
    public float? FlagDistance=>hasPlayer&&states.TryGetValue("OEP_0_BODY",out var body)?body.x-playerX:null;
    private bool UpdateNear(long now)
    {
        bool fresh=hasPlayer&&now-playerAt<=600&&now-bodyAt<=600&&states.TryGetValue("OEP_0_BODY",out var body)&&body.enabled;
        float distance=Distance??float.PositiveInfinity;
        if(!fresh||FlagDistance<=DismissDistance||distance>(near?200:ApproachDistance)){near=false;nearSince=0;announced="";return false;}
        if(candidate.Length==0)return false;
        if(!near){if(nearSince==0)nearSince=now;if(now-nearSince<200)return false;near=true;}
        return true;
    }
    public string CurrentAt(long now)=>UpdateNear(now)?candidate:"";
    public string Current=>candidate;
    public string Take(long now)
    {
        if(!UpdateNear(now)||candidate.Length==0||candidate==announced||now-changedAt<300)return "";
        announced=candidate;return candidate;
    }
}

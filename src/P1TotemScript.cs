namespace PataponAccess;
internal sealed record P1TotemProfile(uint LayoutId,string Sequence);
internal static class P1TotemScript
{
    // Recognize the native totem state machine, not a mission address or a saved solution.
    public static List<P1TotemProfile> Read(byte[] b)
    {
        var result=new List<P1TotemProfile>();
        if(b.Length<256||b.Length>2_000_000)return result;
        uint U(int i)=>i>=0&&i<=b.Length-4?BitConverter.ToUInt32(b,i):uint.MaxValue;
        float F(int i)=>i>=0&&i<=b.Length-4?BitConverter.ToSingle(b,i):float.NaN;
        for(int pos=0;pos<=b.Length-28;pos+=4){
            if(U(pos)!=0x00130125||U(pos+4)!=2||U(pos+12)!=0x20||U(pos+20)!=0x20)continue;
            uint id=U(pos+8);var branches=new Dictionary<int,(uint variable,int target)>();
            for(int j=Math.Max(0,pos-512);j<=pos-24;j+=4){
                if(U(j)!=0x00150025||U(j+4)!=8||U(j+12)!=0x10)continue;
                float v=F(j+16);if(v!=1&&v!=3&&v!=4&&v!=5)continue;
                uint dest=U(j+20);if(dest>=b.Length||dest%4!=0)continue;
                branches[(int)v]=(U(j+8),(int)dest);
            }
            if(branches.Count!=4||branches.Values.Select(v=>v.variable).Distinct().Count()!=1)continue;
            var drums=new List<string>();uint? required=null,check=null;bool valid=true;
            foreach(int stage in new[]{1,3,4,5}){
                int target=branches[stage].target;bool found=false;
                for(int j=target;j<=Math.Min(target+100,b.Length-28);j+=4){
                    if(U(j)!=0x00070025||U(j+4)!=8||U(j+12)!=0x10||U(j+20)!=0x00030025)continue;
                    string drum=F(j+16) switch{1=>"PON",2=>"PATA",3=>"DON",4=>"CHAKA",_=>""};
                    uint req=U(j+8),call=U(j+24);
                    if(drum.Length==0||call>=b.Length||(required.HasValue&&required!=req)||(check.HasValue&&check!=call))break;
                    required=req;check=call;drums.Add(drum);found=true;break;
                }
                if(!found){valid=false;break;}
            }
            if(!valid||!check.HasValue||U((int)check.Value)!=0x00090c25)continue;
            bool compare=false;
            for(int j=(int)check.Value;j<=Math.Min((int)check.Value+128,b.Length-24);j+=4)
                if(U(j)==0x00160025&&U(j+4)==8&&U(j+12)==8&&U(j+16)==required)compare=true;
            if(compare&&!result.Any(p=>p.LayoutId==id))result.Add(new(id,string.Join(" ",drums)));
        }
        return result;
    }
}

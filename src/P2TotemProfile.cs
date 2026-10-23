namespace PataponAccess;
internal sealed record P2TotemProfile(int BodyVariable,int StateVariable,float Minimum,float Maximum);
internal static class P2TotemProfiles
{
    // Recognize the native OEP distance helpers and their state dispatcher.
    // No mission IDs, saved coordinates or fixed instruction addresses.
    public static List<P2TotemProfile> Read(byte[] b)
    {
        var result=new List<P2TotemProfile>();
        if(b.Length<256||b.Length>2_000_000)return result;
        uint U(int p)=>p>=0&&p<=b.Length-4?BitConverter.ToUInt32(b,p):uint.MaxValue;
        float F(int p)=>p>=0&&p<=b.Length-4?BitConverter.ToSingle(b,p):float.NaN;
        bool Position(int p)=>U(p)==0x00130125&&U(p+4)==8&&U(p+12)==0x20&&U(p+20)==0x20
            &&U(p+28)==0x00a00425&&U(p+32)==0x20&&U(p+36)==U(p+24)
            &&U(p+40)==0x00090025&&U(p+44)==0x20&&U(p+48)==U(p+16)&&U(p+52)==0x20&&U(p+56)==U(p+24)
            &&U(p+60)==0x00190025&&U(p+64)==0x20&&U(p+68)==U(p+16)&&U(p+72)==0x10&&F(p+76)==0
            &&U(p+88)==0x20&&U(p+92)==U(p+16)&&U(p+96)==0x10&&U(p+80)==U(p+104);
        for(int a=0;a<=b.Length-140;a+=4){
            if(!Position(a)||U(a+84)!=0x001a0025||!float.IsFinite(F(a+100))||F(a+100)<=0
                ||U(a+80)!=(uint)(a+124)||U(a+108)!=0x00040025||U(a+112)!=2||U(a+116)!=1||U(a+120)!=0x00010025
                ||U(a+124)!=0x00040025||U(a+128)!=2||U(a+132)!=0||U(a+136)!=0x00010025)continue;
            // The actual hit-comparison routine must call this range helper.
            bool hitCaller=false;
            for(int c=Math.Max(24,a-512);c<=Math.Min(a+512,b.Length-8);c+=4)
                if(U(c)==0x00030025&&U(c+4)==a&&U(c-24)==0x00160025&&U(c-20)==8&&U(c-12)==8)hitCaller=true;
            if(!hitCaller)continue;
            for(int d=a+140;d<=Math.Min(a+1024,b.Length-140);d+=4){
                if(!Position(d)||U(d+8)!=U(a+8)||U(d+84)!=0x00190025||!float.IsFinite(F(d+100))||F(d+100)<=0||F(d+100)>=F(a+100)
                    ||U(d+80)!=(uint)(d+124)||U(d+108)!=0x00040025||U(d+112)!=2||U(d+116)!=0||U(d+120)!=0x00010025
                    ||U(d+124)!=0x00160125||U(d+128)!=8||U(d+132)!=U(a+8))continue;
                // Dispatcher: stage 9, stage 8, dismissal call, stages 7..1.
                for(int c=48;c<b.Length-176;c+=4){
                    if(U(c)!=0x00030025||U(c+4)!=d)continue;
                    uint state=U(c-40);bool valid=state<4096&&U(a+8)<4096;
                    foreach(var (offset,stage) in new[]{(-48,9),(-24,8),(8,7),(32,6),(56,5),(80,4),(104,3),(128,2),(152,1)}){
                        int p=c+offset;
                        valid&=U(p)==0x00150025&&U(p+4)==8&&U(p+8)==state&&U(p+12)==0x10&&F(p+16)==stage&&U(p+20)<b.Length&&U(p+20)%4==0;
                    }
                    if(valid&&!result.Any(x=>x.BodyVariable==U(a+8)&&x.StateVariable==state))result.Add(new((int)U(a+8),(int)state,F(d+100),F(a+100)));
                }
            }
        }
        return result;
    }
}

namespace PataponAccess;
internal static class ObjectRangeWave
{
    public static byte[] Build(bool enter)
    {
        const int rate=48000,count=15840;
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
        for(int i=0;i<count;i++){
            double time=(double)i/rate,t=time%0.16;int pulse=(int)(time/0.16);
            double freq=enter?(pulse==0?780:1170):(pulse==0?1170:780);
            double value=pulse<2&&t<0.12?(Math.Sin(2*Math.PI*freq*t)+0.35*Math.Sin(2*Math.PI*freq*2.7*t))*Math.Exp(-t*36)*Math.Min(1,t/0.003):0;
            w.Write((short)Math.Clamp(value*14000,short.MinValue,short.MaxValue));
        }
        return stream.ToArray();
    }
}

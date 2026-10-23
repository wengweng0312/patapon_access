namespace PataponAccess;
internal static class MechanismReadyWave
{
    public static byte[] Build(){
        const int rate=48000,count=14400;
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
        for(int i=0;i<count;i++){
            double time=(double)i/rate;int pulse=(int)(time/0.09);double t=time-pulse*0.09;
            double frequency=pulse switch{0=>900,1=>1200,_=>1600};
            double value=pulse<3&&t<0.075?Math.Sin(2*Math.PI*frequency*t)*Math.Min(1,t/0.012)*Math.Exp(-t*35):0;
            w.Write((short)(value*16000));
        }
        return stream.ToArray();
    }
}

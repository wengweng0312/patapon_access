namespace PataponAccess;
internal static class StickCueWave
{
    // Original synthesized wood-stick transient; no borrowed game recording.
    public static byte[] Build(uint tempo=146,int pulses=3,double gain=3,bool rapid=false)
    {
        const int rate=48000;
        double spacing=30.0/Math.Clamp(tempo,40u,300u);
        int count=(int)Math.Ceiling((spacing*(pulses-1)+0.03)*rate);
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
        uint random=9137;double last=0;
        for(int i=0;i<count;i++) {
            double absolute=(double)i/rate;
            int pulse=Math.Min(pulses-1,(int)(absolute/spacing));double t=absolute-pulse*spacing;
            int local=(int)(t*rate);
            random=1664525*random+1013904223;double noise=(random/(double)uint.MaxValue)*2-1;
            double attack=Math.Min(1,local/12.0),fade=Math.Clamp((1440-local)/80.0,0,1);
            double frequency=rapid?3200:2350;
            double tone=(0.6*Math.Sin(2*Math.PI*frequency*t)+0.25*Math.Sin(2*Math.PI*(rapid?5300:4100)*t))*Math.Exp(-t*230);
            double tap=(noise-last)*0.12*Math.Exp(-t*850);last=noise;
            writer.Write((short)Math.Clamp((tone+tap)*attack*fade*19000*gain,short.MinValue,short.MaxValue));
        }
        return stream.ToArray();
    }
}

namespace PataponAccess;
internal static class WaveVolume
{
    public static byte[] Scale(byte[] source,int level)
    {
        var data=(byte[])source.Clone();
        if(data.Length<12||System.Text.Encoding.ASCII.GetString(data,0,4)!="RIFF"||System.Text.Encoding.ASCII.GetString(data,8,4)!="WAVE")throw new InvalidDataException("Invalid WAV");
        bool pcm=false;
        for(int p=12;p+8<=data.Length;) {
            int length=BitConverter.ToInt32(data,p+4);if(length<0||length>data.Length-p-8)throw new InvalidDataException("Invalid WAV chunk");
            string tag=System.Text.Encoding.ASCII.GetString(data,p,4);int start=p+8;
            if(tag=="fmt ")pcm=length>=16&&BitConverter.ToInt16(data,start)==1&&BitConverter.ToInt16(data,start+14)==16;
            if(tag=="data") {
                if(!pcm||length%2!=0)throw new InvalidDataException("Expected 16-bit PCM");
                double gain=Math.Clamp(level,0,20)/10.0;
                for(int i=start;i<start+length;i+=2){short sample=(short)Math.Clamp(Math.Round(BitConverter.ToInt16(data,i)*gain),short.MinValue,short.MaxValue);data[i]=(byte)(sample&255);data[i+1]=(byte)((sample>>8)&255);}
            }
            p=start+length+(length&1);
        }
        return data;
    }
}

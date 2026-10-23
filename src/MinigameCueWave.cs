namespace PataponAccess;
internal static class MinigameCueWave
{
    // Listening calibration reported by the user, not a measured CRI latency.
    // Silence in the generated PCM avoids frame-quantized sleeps/timers and
    // shifts the entire pattern without changing its pulse spacing or gain.
    public const int DelayMs=35;
    public static byte[] Align(byte[] wave)
    {
        if(wave.Length<44||System.Text.Encoding.ASCII.GetString(wave,0,4)!="RIFF"||
            System.Text.Encoding.ASCII.GetString(wave,8,8)!="WAVEfmt "||
            BitConverter.ToInt32(wave,16)!=16||BitConverter.ToInt16(wave,20)!=1||
            BitConverter.ToInt16(wave,22)!=1||BitConverter.ToInt32(wave,24)!=48000||
            BitConverter.ToInt16(wave,34)!=16||System.Text.Encoding.ASCII.GetString(wave,36,4)!="data"||
            BitConverter.ToInt32(wave,40)!=wave.Length-44)
            throw new ArgumentException("Expected generated 48kHz mono PCM16 wave",nameof(wave));
        int padding=48000*DelayMs/1000*2;
        var delayed=new byte[wave.Length+padding];
        Buffer.BlockCopy(wave,0,delayed,0,44);
        Buffer.BlockCopy(wave,44,delayed,44+padding,wave.Length-44);
        Buffer.BlockCopy(BitConverter.GetBytes(delayed.Length-8),0,delayed,4,4);
        Buffer.BlockCopy(BitConverter.GetBytes(delayed.Length-44),0,delayed,40,4);
        return delayed;
    }
}

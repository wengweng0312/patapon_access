using PataponAccess;
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
var files=Directory.GetFiles("assets","*.wav");
foreach(var file in files){var bytes=File.ReadAllBytes(file);Check(bytes.SequenceEqual(WaveVolume.Scale(bytes,10)),Path.GetFileName(file)+" original volume unchanged");Check(bytes.SequenceEqual(File.ReadAllBytes(file)),"original file preserved");WaveVolume.Scale(bytes,0);WaveVolume.Scale(bytes,20);}
var b=File.ReadAllBytes(files[0]);int p=12;while(System.Text.Encoding.ASCII.GetString(b,p,4)!="data")p+=8+BitConverter.ToInt32(b,p+4)+(BitConverter.ToInt32(b,p+4)&1);
int start=p+8,n=BitConverter.ToInt32(b,p+4);var silent=WaveVolume.Scale(b,0);Check(silent.Skip(start).Take(n).All(x=>x==0),"mute is silent");
b[start]=0x30;b[start+1]=0x75;var loud=WaveVolume.Scale(b,20);Check(BitConverter.ToInt16(loud,start)==32767,"boost clips safely without integer wrap");
var half=WaveVolume.Scale(b,5);Check(BitConverter.ToInt16(half,start)==15000,"individual gain scales PCM");

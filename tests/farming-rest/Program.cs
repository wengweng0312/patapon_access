using PataponAccess;
int checks=0;
void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
foreach(double gap in new[]{0.5,1,1.5,3.5,5,8,double.NaN,double.PositiveInfinity})Check(FarmingRestTiming.RestCount(gap)==0,"ordinary/subdivision/long/unknown gap");
Check(FarmingRestTiming.RestCount(2)==1,"one rest");Check(FarmingRestTiming.RestCount(3)==2,"two rests");
foreach(int rests in new[]{1,2,3}){
 var timing=new FarmingRestTiming();timing.Register(7,rests+1);
 Check(!timing.Poll(7,rests+1.2,out _),"do not stop during prior note");
 Check(timing.Poll(7,rests+0.98,out int count)&&count==rests,"one beat ahead of rest");
 Check(!timing.Poll(7,rests,out _),"one cue per gap");
 Check(!timing.Poll(7,1,out _),"no repeats during remaining rests");
}
var late=new FarmingRestTiming();late.Register(1,2);Check(!late.Poll(1,0.7,out _),"stall skips late stop");Check(!late.Poll(1,0.5,out _),"late remains consumed");
var seek=new FarmingRestTiming();seek.Register(1,2);seek.Poll(1,2.2,out _);seek.Poll(1,3.2,out _);Check(!seek.Poll(1,1.1,out _),"seek does not replay");
var clear=new FarmingRestTiming();clear.Register(1,2);clear.Clear();Check(!clear.Poll(1,1.1,out _),"tutorial/exit reset");clear.Register(1,2);clear.Remove(1);Check(!clear.Poll(1,1.1,out _),"recycled visual key");
Check(Math.Abs(DanceNoteTiming.RemainingBeats(400,300,-50)-1)<0.001,"native negative travel direction");
Console.WriteLine($"{checks} farming rest timing checks passed. Synthetic visual-note replay; game runtime still unverified.");

// Real raw observations from run 20261023-180927-30252: native named half
// length -28.133333 made one beat look like 1.5; do not use it as a quarter.
var scale=new FarmingBeatScale();
scale.Observe(7,222.42676,63979);scale.Observe(7,194.85352,64113);scale.Observe(7,167.28027,64246);
double width=Math.Abs(scale.BeatLength(147));
Check(width>83&&width<86,"real motion/song beat calibration");
Check(FarmingRestTiming.RestCount(3.0015303760977394*56.266666/width)==1,"reported first pattern is one rest, not two");
Check(FarmingRestTiming.RestCount(4.532923696314209*56.266666/width)==2,"reported missed second pattern is two rests");
Check(FarmingRestTiming.RestCount(1.5*56.266666/width)==0,"ordinary musical beat is not rest");
scale.Clear();Check(double.IsNaN(scale.BeatLength(147)),"pause/scene discards calibration");
Console.WriteLine($"{checks} total checks including recorded missed second pattern.");

Check(FarmingRestTiming.RestCount((42.513-40.874)*147/60)==3,"video 40.874 to 42.513: three empty beats");
Check(FarmingRestTiming.RestCount((39.672-38.447)*147/60)==2,"video 38.447 to 39.672: two empty beats");
Check(FarmingRestTiming.RestCount((29.006-28.208)*147/60)==1,"video 28.208 to 29.006: one empty beat");
Console.WriteLine($"{checks} total checks including user's video.");

var adjusted=new DanceNoteTiming();adjusted.Register(9);
Check(!adjusted.Poll(9,1.05,0.02),"do not over-advance dance");
Check(adjusted.Poll(9,1.009,0.02),"nearest frame instead of recorded late0.96856");
Check(!adjusted.Poll(9,0.96856,0.02),"nearest frame emits only once");
var syncopated=new DanceNoteTiming();syncopated.Register(1);syncopated.Register(2);
Check(syncopated.Poll(1,0.99),"first visible note");
Check(!syncopated.Poll(2,1.66),"later syncopated note not emitted early");
Check(syncopated.Poll(2,0.99),"later syncopated note also gets its own cue");
Console.WriteLine($"{checks} total checks including nearest-frame dance and syncopated farming notes.");

var stableScale=new FarmingBeatScale();stableScale.Observe(88,1000,0);
stableScale.Observe(88,958.24,200);stableScale.Observe(88,916.48,400);stableScale.Observe(88,874.72,600);
double stableWidth=stableScale.BeatLength(147);stableScale.Observe(88,831.49,800);
Check(Math.Abs(stableScale.BeatLength(147)-stableWidth)<0.1,"scheduler jitter does not shift later cues");
Console.WriteLine($"{checks} total farming/dance timing checks.");

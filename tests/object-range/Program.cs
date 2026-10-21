using PataponAccess;
void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
var enemy=new RangeTransition();var obj=new RangeTransition();
Check(obj.Observe(false,0)==0,"no initial leave");
Check(enemy.Observe(true,0)==0&&enemy.Observe(true,200)==1,"enemy enter");
Check(obj.Observe(true,300)==0&&obj.Observe(true,500)==1&&enemy.Inside,"object enters while enemy stays");
Check(obj.Observe(false,600)==0&&obj.Observe(true,900)==0,"brief false sample does not leave");
Check(obj.Observe(false,1000)==0&&obj.Observe(false,1500)==-1&&enemy.Inside,"object leaves independently");
Check(obj.Observe(true,1600)==0&&obj.Observe(true,1800)==1,"object reentry");
obj.Reset();Check(!obj.Inside&&obj.Observe(false,2000)==0,"mission reset has no stale leave");
var one=new RangeTransition();Check(one.Observe(true,0)==0&&one.Observe(false,100)==0&&one.Observe(false,700)==0,"single short spike silent");
foreach(bool enter in new[]{true,false}){
 var wave=ObjectRangeWave.Build(enter);Check(wave.Length==31724&&BitConverter.ToInt32(wave,40)==31680,"valid wave length "+enter);
 Check(BitConverter.ToInt32(wave,24)==48000,"48kHz "+enter);
 Check(wave.Skip(44).Any(x=>x!=0),"audible data "+enter);
}
Check(!ObjectRangeWave.Build(true).SequenceEqual(ObjectRangeWave.Build(false)),"enter and leave differ");

WorldObjectLog.Configure(false);WorldObjectLog.Observe(new P1.Game.Gimmick.Gimmick{Pointer=new IntPtr(1)});
Check(PataponAccess.Monitor.Events.Count==0,"disabled diagnostics silent");
WorldObjectLog.Configure(true);
for(int i=1;i<=140;i++)WorldObjectLog.Observe(new P1.Game.Gimmick.Gimmick{Pointer=new IntPtr(i),id_=(uint)i});
Check(PataponAccess.Monitor.Events.Count==128,"object observation cap");
WorldObjectLog.Observe(new P1.Game.Gimmick.Gimmick{Pointer=new IntPtr(1)});
Check(PataponAccess.Monitor.Events.Count==128,"same-frame snapshot deduplicated");
WorldObjectLog.Reset();WorldObjectLog.Observe(new P1.Game.Gimmick.Gimmick{Pointer=new IntPtr(1)});
WorldObjectLog.Observe(new P2.Game.Gimmick.Gimmick{Pointer=new IntPtr(1)});
Check(PataponAccess.Monitor.Events.Count==130,"mission reset and game identities separated");
BattleMonitor.Active=false;WorldObjectLog.Observe(new P2.Game.Gimmick.Gimmick{Pointer=new IntPtr(2)});
Check(PataponAccess.Monitor.Events.Count==130,"outside mission silent");

if(args.Length>0){Directory.CreateDirectory(args[0]);File.WriteAllBytes(Path.Combine(args[0],"object-range-enter.wav"),ObjectRangeWave.Build(true));File.WriteAllBytes(Path.Combine(args[0],"object-range-leave.wav"),ObjectRangeWave.Build(false));}

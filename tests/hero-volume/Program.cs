using System.Text.Json;
using PataponAccess;
int count=0;void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
foreach(string game in new[]{"P1","P1S","Common","Unknown"})Check(!HeroSongRules.IsTarget(game,"ptp_battle_hero.acb",13),"other game untouched "+game);
foreach(string path in new[]{"ptp_makai_darkhero.acb","ptp_btl_bgm001_voice.acb","other/ptp_battle_hero_extra.acb",""})Check(!HeroSongRules.IsTarget("P2",path,13),"other bank untouched "+path);
Check(HeroSongRules.IsTarget("P2",@"C:\Audio\PTP_BATTLE_HERO.acb",14),"Windows bank path and case supported");
Check(HeroSongRules.Scale(.8f,5)==.4f,"half gain preserves game base gain");
Check(HeroSongRules.Scale(.8f,0)==0,"mute");
Check(HeroSongRules.Scale(.8f,10)==.8f,"original gain restored");
Check(HeroSongRules.Scale(.8f,20)==1.6f,"maximum gain");
float raw=.8f;foreach(int level in new[]{5,6,2,10,0,10})Check(HeroSongRules.Scale(raw,level)==raw*level/10f,"slider computes from original gain without accumulating "+level);
Console.WriteLine($"{count} hero voice selection and gain checks passed; game playback is not verified.");
Check(HeroSongRules.PreviewSeIndex(0)==HeroSongRules.PreviewSeIndex(3),"preview cycles through intro plus two songs");
Check(!HeroSongRules.IsTarget("P2","ptp_battle_hero.acb",11),"charge/tame cue remains unchanged");
Check(!HeroSongRules.IsTarget("P2","ptp_battle_hero.acb",15)&&!HeroSongRules.IsTarget("P2","ptp_battle_hero.acb",16),"death and rebirth remain unchanged");

using PataponAccess;
int checks=0;void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
foreach(string lang in new[]{"TC","SC","JA","EN"}){
 string text=BossRushManualText.Read("sp_manual",lang);
 Check(text.Length>450,"complete manual "+lang);
 Check(text.Contains("BOSS RUSH CHALLENGE"),"stylized title transcribed "+lang);
 Check(text.Contains("STAGE")||text.Contains("stage"),"stage information "+lang);
 Check(!text.Contains("HALLN")&&!text.Contains("RUSA"),"OCR logo fragments removed by visual check "+lang);
 Check(BossRushManualText.Read("sp_manual",lang.ToLowerInvariant())==text,"native language case "+lang);
}
Check(BossRushManualText.Read("other","TC")=="","unmatched image not assigned a manual");
Check(BossRushManualText.Read("sp_manual","FR")=="","unsupported language not translated");
Check(BossRushManualText.Read("sp_manual","TC").Contains("只有此時可以保存遊玩資料"),"TC saving restriction preserved");
Check(BossRushManualText.Read("sp_manual","EN").Contains("It is only after a clearing a stage"),"source wording including original grammar retained");
Console.WriteLine($"PASS {checks} manual source and language checks");

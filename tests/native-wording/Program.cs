using PataponAccess;
int count=0;void Check(bool b,string label){if(!b)throw new Exception(label);count++;}
foreach(var locale in new[]{"zh-TW","zh-CN","en","ja"}){
 I18n.SetLanguage(locale);
 Check(I18n.T("ui.120d60cc3c42","")=="PATAPON 1","launcher 1 "+locale);
 Check(I18n.T("ui.fac22c2337e1","")=="PATAPON 2","launcher 2 "+locale);
 foreach(var data in new[]{("升級條件","普通 Lv 2 石塊 (12／2)","普通 Lv 1","普通的PATAPON"),("升级条件","普通 Lv 2 石块 (12／2)","普通 Lv 1","原本的能力说明"),("Upgrade requirements","Normal Lv 2 Iron (12/2)","Normal Lv 1","Original ability description"),("レベルアップ条件","ノーマル Lv 2 鉄 (12/2)","ノーマル Lv 1","ゲームの説明")}){
  var tracker=new EvolutionWindowTracker();tracker.Observe(data.Item3,data.Item4,1000,"description");
  var r=tracker.Observe(data.Item1,data.Item2,1001,"requirements");
  Check(r.handled&&r.text==data.Item3+"。"+data.Item4+"。"+data.Item1+"。"+data.Item2,"native text unchanged "+locale);
  Check(tracker.Observe(data.Item1,data.Item2,1002,"requirements").text=="","repeat suppression "+locale);
 }
 var reward=new RewardResultState();reward.Observe("result",new[]{"Clear time","00:02:44","Charin","96","Fang","5"},true,1000);reward.Observe("result",new[]{"Clear time","00:02:44","Charin","96","Fang","5"},true,1300);
 Check(reward.Take(1300)=="Clear time。00:02:44。Charin。96。Fang。5","result keeps native rows "+locale);
}
Check(DialogueText.Clean("Iron (12/2)/Fang (0 / 1)")=="Iron (12/2) Fang (0 / 1)","numeric ratios survive native line separators");
Check(DialogueText.Clean("鉄（１２/２）/次の行")=="鉄（１２/２） 次の行","fullwidth digits survive");
Console.WriteLine($"PASS {count} native wording checks; offline only.");

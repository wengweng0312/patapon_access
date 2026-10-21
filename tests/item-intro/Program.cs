using PataponAccess;
int n=0;void Check(bool v,string name){if(!v)throw new Exception(name);n++;}
var t=new ItemSelectionReadout();
Check(t.Observe("one","道具選擇","肉，持有99個")=="道具選擇，肉，持有99個","first includes header");
Check(t.Observe("one","道具選擇","肉，持有99個")==null,"stationary selection silent");
Check(t.Observe("one","道具選擇","石頭，持有99個")=="石頭，持有99個","navigation retains item and count only");
Check(t.Observe("one","道具選擇","石頭，持有98個")=="石頭，持有98個","changed count remains readable");
Check(t.Observe("one","道具選擇","肉，持有99個")=="肉，持有99個","return to previous item is read");
t.Close("other");Check(t.Observe("one","道具選擇","肉，持有99個")==null,"inactive unrelated window cannot reset visit");
t.Close("one");Check(t.Observe("one","道具選擇","肉，持有99個")=="道具選擇，肉，持有99個","reopen introduces once again");
Check(t.Observe("two","道具選擇","肉，持有99個")=="道具選擇，肉，持有99個","new owner independent");
var empty=new ItemSelectionReadout();Check(empty.Observe("x","道具選擇","")==null&&empty.Observe("x","道具選擇","石頭")=="道具選擇，石頭","unready content does not consume introduction");
Console.WriteLine($"{n} item intro cases passed.");

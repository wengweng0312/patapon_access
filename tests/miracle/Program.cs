using PataponAccess;
int n=0;void Check(bool x){n++;if(!x)throw new Exception("Check "+n);}
var s=new MiracleThreshold();
Check(!s.Observe(true,120));Check(!s.Observe(true,11));Check(s.Observe(true,10));Check(!s.Observe(true,9));Check(!s.Observe(true,8));Check(!s.Observe(false,0));
Check(!s.Observe(true,50));Check(s.Observe(true,9.9f));Check(!s.Observe(true,9.8f));s.Reset();Check(!s.Observe(true,9));Check(!s.Observe(true,8));s.Reset();Check(!s.Observe(true,20));Check(!s.Observe(false,19));Check(!s.Observe(true,5));s.Reset();Check(!s.Observe(true,20));Check(!s.Observe(true,0));Check(!s.Observe(true,float.NaN));Check(!s.Observe(true,30));Check(s.Observe(true,9.99f));Console.WriteLine($"PASS {n} miracle threshold checks");
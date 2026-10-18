using PataponAccess;
static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
var view=new CartBox(100,-50,300,200);
Check(view.ContainsX(100)&&view.ContainsX(300)&&!view.ContainsX(301),"native point-view boundaries");
Check(view.Overlaps(new CartBox(299,0,399,80)),"cart collision edge inside");
Check(!view.Overlaps(new CartBox(301,0,401,80)),"cart collision outside");
Check(!view.Overlaps(new CartBox(150,201,250,281)),"vertical separation");
Check(!new CartBox(float.NaN,0,1,1).Overlaps(view),"invalid native sample rejected");
var latch=new CartRangeLatch();
Check(latch.Observe(true,0)==0&&latch.Observe(true,199)==0&&latch.Observe(true,200)==1,"200ms stable entry");
Check(latch.Observe(true,300)==0,"no repeated entry");
Check(latch.Observe(false,400)==0&&latch.Observe(true,600)==0&&latch.Inside,"brief loss does not exit");
Check(latch.Observe(false,700)==0&&latch.Observe(false,1199)==0&&latch.Observe(false,1200)==-1,"500ms stable exit");
Check(latch.Observe(false,1300)==0,"no repeated exit");
Check(latch.Observe(true,1400)==0&&latch.Observe(true,1600)==1,"reentry after exit");
latch.Observe(false,1700); latch.Invalidate(); Check(latch.Observe(false,2300)==0&&latch.Inside,"unknown samples restart stability timer");

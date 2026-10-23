using PataponAccess;
using P2.Pyd.Script.Talk;
using System.Reflection;
int n=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);n++;}
Variables Vars(int body,int stage){var v=new Variables{integerArray_=Enumerable.Range(0,256).Select(i=>new Value()).ToArray()};v.integerArray_[0xce].i=body;v.integerArray_[0xc8].i=stage;return v;}
var controller=new Controller{
 context_=new[]{new Context{buffer=new P2.Pyd.Script.Talk.Buffer{Bytes=File.ReadAllBytes("work/totem-investigation/missionid_10006.pac")}}},
 variable_=Vars(99,0),manager_=new Manager{variable_=Vars(17,1)}
};
var observe=typeof(P2TotemNative).GetMethod("Observe",BindingFlags.Static|BindingFlags.NonPublic)!;
observe.Invoke(null,new object[]{controller});
Check(DrumPuzzleAccess.accepted==1&&DrumPuzzleAccess.lastBody==17&&DrumPuzzleAccess.lastStage==1,"production hook reads kind8 globals despite differing locals");
controller.variable_=Vars(17,1);controller.manager_!.variable_!.integerArray_![0xc8].i=7;
observe.Invoke(null,new object[]{controller});
Check(DrumPuzzleAccess.accepted==2&&DrumPuzzleAccess.lastStage==7,"global wait phase cannot become local ready phase");
controller.variable_=null;controller.manager_.variable_.integerArray_![0xc8].i=2;
observe.Invoke(null,new object[]{controller});
Check(DrumPuzzleAccess.accepted==3&&DrumPuzzleAccess.lastStage==2,"missing locals do not block real global mechanism state");
controller.manager_=null;controller.variable_=Vars(17,1);int previous=DrumPuzzleAccess.attempts;
observe.Invoke(null,new object[]{controller});Check(DrumPuzzleAccess.attempts==previous,"missing globals never fall back to unrelated locals");
P2TotemNative.Clear();
Console.WriteLine($"PASS {n} production native-observer global/local storage regressions. Wrappers are test doubles, not live gameplay.");

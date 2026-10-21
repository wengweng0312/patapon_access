namespace PataponAccess {
internal static class BattleMonitor { public static bool Active=true; }
internal static class Monitor { public static readonly List<(string kind,object data)> Events=new();public static void Record(string kind,object data)=>Events.Add((kind,data)); }
}
namespace P1.Game.Gimmick {
internal class Layout {public string paramName="BOX";}
internal class Gimmick {public IntPtr Pointer;public uint id_; public Layout layoutParam_=new();public bool isEnable_=true,isViewable_=true,isHit_,isInnerTarget_;public int prevTargetType_,prevTargetId_;}
}
namespace P2.Game.Gimmick {
internal class Layout {public string paramName="BOX",name="test-box";}
internal class Gimmick {public IntPtr Pointer;public uint id_; public Layout layoutParam_=new();public bool isEnable_=true,isViewable_=true,isHit_,isInnerTarget_;public int prevTargetType_,prevTargetId_;}
}

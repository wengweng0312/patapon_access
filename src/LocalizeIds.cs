namespace PataponAccess;
internal static class LocalizeIds
{
    public static uint Pack(uint category,uint index)=>((category&0xffff)<<16)|(index&0xffff);
    public static uint Unit(uint id)=>id<=0xffff?Pack(1,id):id;
}

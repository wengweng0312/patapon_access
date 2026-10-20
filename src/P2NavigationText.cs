namespace PataponAccess;
internal static class P2NavigationText
{
    public static string UnitPage(int slot,string form,int level,bool entered)
    {
        string current=string.IsNullOrWhiteSpace(form)?"":"，目前型態 "+form+(level>0?"，等級 "+level:"");
        if(slot<0)return "型態欄"+current+"，按確認切換型態";
        return "裝備欄，第 "+(slot+1)+" 格"+(entered?current+"。切換型態，請用上下鍵移到型態欄，再按確認":"");
    }
    public static string Item(string context,string name,int index,int total,int? count,bool equipped,string? explain)
        =>context+"，"+name+"，第 "+(index+1)+" 項，共 "+total+" 項"+(count.HasValue?"，持有 "+count.Value+" 件":"")+(equipped?"，目前使用":"")+(string.IsNullOrWhiteSpace(explain)?"":"。"+explain);
    public static string Slot(string name,int index,int total,int rare,int level,bool enabled)
    {
        string place=$"{name}，第 {index+1} 位，共 {total} 位";
        // Native renderSelectItem uses evolutionClass < 0 for invalidUnitIconModelName.
        if(rare<0)return place+"，空位"+(enabled?"，按確認製造士兵":"，尚未開放");
        return place+(level>0?$"，等級 {level}":"，等級資料未取得")+(enabled?"":"，尚未開放");
    }
    public static string Blocked(int x,int y,int direction)
    {
        bool edge=direction switch{1=>x==0,2=>y==0,3=>x==6,4=>y==4,_=>false};
        return edge?"已到地圖邊界":"這個方向沒有可移動的節點";
    }
}

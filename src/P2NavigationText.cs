namespace PataponAccess;
internal static class P2NavigationText
{
    public static string UnitPosition(int index,int total)=>index<0?I18n.T("ui.244422cdeb4d","士兵位置未取得"):total>index?I18n.T("ui.4ca32be47900","第 {0} 位士兵，共 {1} 位",index+1,total):I18n.T("ui.664d73dac7e0","第 {0} 位士兵",index+1);
    public static string UnitPage(int slot,string form,int level,bool entered)
    {
        string current=string.IsNullOrWhiteSpace(form)?"":I18n.T("ui.05c86b853408","，目前型態 ")+form+(level>0?I18n.T("ui.37b3ed821f50","，等級 ")+level:"");
        if(slot<0)return I18n.T("ui.ce1602800d76","型態欄")+current+I18n.T("ui.39876505a999","，按確認切換型態");
        return I18n.T("ui.3e757adc2788","裝備欄，第 ")+(slot+1)+I18n.T("ui.6d1f8ad23ef0"," 格")+(entered?current+I18n.T("ui.4c81a125b048","。切換型態，請用上下鍵移到型態欄，再按確認"):"");
    }
    public static string Item(string context,string name,int index,int total,int? count,bool equipped,string? explain)
        =>context+"，"+name+I18n.T("ui.fab2a038b6d0","，第 ")+(index+1)+I18n.T("ui.b850d8d5860c"," 項，共 ")+total+I18n.T("ui.06189562e746"," 項")+(count.HasValue?I18n.T("ui.39502a5111bf","，持有 ")+count.Value+I18n.T("ui.421f465daee8"," 件"):"")+(equipped?I18n.T("ui.3f438bc00953","，目前使用"):"")+(string.IsNullOrWhiteSpace(explain)?"":"。"+explain);
    public static string Slot(string name,int index,int total,int rare,int level,bool enabled)
    {
        string place=I18n.T("ui.82891cd411c9","{0}，第 {1} 位，共 {2} 位",name,index+1,total);
        // Native renderSelectItem uses evolutionClass < 0 for invalidUnitIconModelName.
        if(rare<0)return place+I18n.T("ui.ab94e7960938","，空位")+(enabled?I18n.T("ui.d8975c82e8cd","，按確認製造士兵"):I18n.T("ui.60c962835629","，尚未開放"));
        return place+(level>0?I18n.T("ui.9025a3568058","，等級 {0}",level):I18n.T("ui.be3d8cf47119","，等級資料未取得"))+(enabled?"":I18n.T("ui.60c962835629","，尚未開放"));
    }
    public static string Blocked(int x,int y,int direction)
    {
        bool edge=direction switch{1=>x==0,2=>y==0,3=>x==6,4=>y==4,_=>false};
        return edge?I18n.T("ui.1b13f1832c34","已到地圖邊界"):I18n.T("ui.20329e076ac2","這個方向沒有可移動的節點");
    }
}

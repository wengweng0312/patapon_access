namespace PataponAccess;
internal static class BattleChantText
{
    // Exact, confirmed command chants only. Never filter on words such as
    // attack/march/defend, which also occur in story and mission instructions.
    private static readonly HashSet<string> chants=new(StringComparer.Ordinal){
        "邊唱 邊跳 就能前進！","前進 前進 前進～！","邁向 世界盡頭！","行軍♪",
        "ＯＫ！！ 謹慎向前！","快上 快上 大家快上ー！","PATA PATA PATA PON！",
        "看我的～～～～！","進攻囉 繼續～！！","跟著節奏 攻擊囉！！",
        "上吧！ 毀滅吧PON！！","好～的！ 進攻！！","就這樣進攻吧～！",
        "一點一點 進攻吧","呼～今天狀況 也超好！","攻～擊囉♪",
        "PON PON PATA PON","打敗他們！！","給他好看♪","上囉！ 毀滅吧！！",
        "嗚哈喝！ 大家上吧！♪","PATAPON 來了來了 來了來了 PATAPON",
        "要守住唷！","要守住喔♪"
    };
    private static readonly HashSet<string> tactical=new(StringComparer.Ordinal){
        "什麼都沒有","你是要 進攻哪裡？","不前進嗎…？","無法 做出任何 攻擊？",
        "找不到 戰鬥的對象！","神明大人哪！ 什麼都沒有啊！",
        "現在的攻擊很強烈 要防禦才行唷～","守住！！ 說不定很危險…",
        "CHAKA CHAKA 撐著點！","快逃！ 快逃！ 快逃～！！"
    };
    private static readonly HashSet<string> localizedChants=new(StringComparer.Ordinal);
    private static readonly HashSet<string> localizedTactical=new(StringComparer.Ordinal);
    public static void RegisterNative(IEnumerable<string> commandLines,IEnumerable<string> tacticalLines)
    {
        localizedChants.Clear();localizedTactical.Clear();
        foreach(var text in commandLines)if(!string.IsNullOrWhiteSpace(text))localizedChants.Add(text);
        foreach(var text in tacticalLines)if(!string.IsNullOrWhiteSpace(text))localizedTactical.Add(text);
    }
    public static bool IsChant(string text)=>chants.Contains(text)||localizedChants.Contains(text);
    public static bool ShouldSuppress(bool enabled,bool battle,bool flagUnitSource,string text)
        =>!enabled&&battle&&flagUnitSource&&!tactical.Contains(text)&&!localizedTactical.Contains(text)&&IsChant(text);
}

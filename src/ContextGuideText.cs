namespace PataponAccess;
internal sealed class SquadIntroTracker
{
    private bool spoken;
    public string Say(string text){string prefix=I18n.T("ui.64dcb5c6b4f6","出擊部隊選擇。");string body=text.StartsWith(prefix,StringComparison.Ordinal)?text.Substring(prefix.Length):text;bool first=!spoken;spoken=true;return first?prefix+body:body;}
    public void Reset()=>spoken=false;
}
internal static class ContextGuideText
{
    public static string Village(string previous,string next,string confirm,string save)=>I18n.T("ui.7e8e1dcdc0b6","村落。{0}、{1} 切換設施，也可使用左右方向鍵。{2} 查看，{3} 保存，Escape 返回標題畫面。",previous,next,confirm,save);
    public static string Describe(string context,string confirm,string cancel,string left,string right,string hero)=>context switch{
        "help"=>I18n.T("ui.d1af18c7233b","模組幫助。左右切換分類，上下鍵逐項瀏覽，Enter 試聽或切換設定，Shift 加上下調整目前音效音量。F3 重讀，Escape 或 F1 關閉。遊戲時間仍會繼續。"),
        "preparation"=>I18n.T("ui.058319d8c7bb","任務準備。方向鍵選擇部隊，{0} 配置，R 開啟裝備，F 讓士兵退出出擊隊伍，Tab 出發。F3 重讀目前部隊與能力。",confirm),
        "squad"=>I18n.T("ui.d6020466188c","部隊選擇。方向鍵切換要出擊的兵種，{0} 確認，{1} 返回。F3 重讀目前部隊與能力。",confirm,cancel),
        "save-confirm" or "gift-confirm"=>I18n.T("ui.16e04e527223","確認視窗。上下選擇是或否，{0} 確認，{1} 返回。F3 重讀問題與選擇。",confirm,cancel),
        "evolution"=>I18n.T("ui.c2c8a7d86a16","進化圖。方向鍵移動到不同型態或等級，{0} 確認，{1} 返回。F3 重讀能力介紹與素材需求；未解鎖與素材不足請依目前項目提示判斷。",confirm,cancel),
        "training"=>I18n.T("ui.bc94e27e9fdc","生命之樹。方向鍵選擇要培養的兵種，{0} 開啟，{1} 返回。F3 重讀目前兵種、空位和素材資訊。",confirm,cancel),
        "equipment-list"=>I18n.T("ui.97f7fecbb6c4","裝備或型態清單。方向鍵選擇，{0} 更換，{1} 返回個別士兵頁。F3 重讀名稱與持有數量。",confirm,cancel),
        "unit"=>I18n.T("ui.e8675c9b36b6","個別士兵裝備頁。{0}、{1} 切換士兵，上下選擇裝備或型態欄，{2} 開啟清單，{3} 返回。英雄頁切換兵種使用 {4}。Tab 可能是永久退役，請先聽確認內容。",left,right,confirm,cancel,hero),
        "inventory"=>I18n.T("ui.f8d727683610","道具清單。方向鍵選擇，F3 重讀名稱與數量，{0} 執行目前選項，{1} 返回。",confirm,cancel),
        "settings"=>I18n.T("ui.fa1bde9842aa","設定。上下選擇項目，左右調整，Q、E 切換分頁，{0} 確認，{1} 返回。F3 重讀目前設定。",confirm,cancel),
        "popup"=>I18n.T("ui.8d81ab3f4327","設定確認或按鍵重綁視窗。請依 F3 重讀的問題操作，{0} 返回；重綁時輸入想綁定的按鍵。",cancel),
        "name"=>I18n.T("ui.656f4051202a","神明名稱輸入。直接輸入文字，Backspace 刪除，使用遊戲確認鍵完成。F3 重讀目前名稱。"),
        "opening"=>I18n.T("ui.5152f95dda8a","誓約書。方向鍵移動羽毛，Tab 移到同意，再按遊戲確認鍵。F3 重讀內文。"),
        "treasure"=>I18n.T("ui.409e249380f8","寶箱結算。{0} 繼續，F3 重讀目前獲得的物品。",confirm),
        "title"=>I18n.T("ui.7a67e9fd47ea","標題選單。上下選擇，{0} 確認，{1} 返回。F3 重讀目前項目。",confirm,cancel),
        "battle"=>I18n.T("ui.4a8f9a7e3bb9","戰鬥中。依節奏輸入太鼓指令；綁定按鍵可在 F1 逐項查看。F3 重聽目前提示，F8 查詢射程。"),
        "menu"=>I18n.T("ui.19a4f55a7cd8","選單。方向鍵選擇，{0} 確認，{1} 返回。F3 重讀目前項目。",confirm,cancel),
        _=>I18n.T("ui.74225b87cbe2","目前尚未辨識到可提供專用引導的介面。F3 重讀目前內容，F1 查看完整操作說明。")
    };
}

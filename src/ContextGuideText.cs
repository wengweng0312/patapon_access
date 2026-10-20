namespace PataponAccess;
internal sealed class SquadIntroTracker
{
    private bool spoken;
    public string Say(string text){const string prefix="出擊部隊選擇。";string body=text.StartsWith(prefix,StringComparison.Ordinal)?text.Substring(prefix.Length):text;bool first=!spoken;spoken=true;return first?prefix+body:body;}
    public void Reset()=>spoken=false;
}
internal static class ContextGuideText
{
    public static string Village(string previous,string next,string confirm,string save)=>$"村落。{previous}、{next} 切換設施，也可使用左右方向鍵。{confirm} 查看，{save} 保存，Escape 返回標題畫面。";
    public static string Describe(string context,string confirm,string cancel,string left,string right,string hero)=>context switch{
        "help"=>"模組幫助。上下鍵逐項瀏覽，Enter 試聽或切換設定，Shift 加上下調整目前音效音量。F3 重讀，Escape 或 F1 關閉。遊戲時間仍會繼續。",
        "preparation"=>$"任務準備。方向鍵選擇部隊，{confirm} 配置，R 開啟裝備，F 讓士兵退出出擊隊伍，Tab 出發。F3 重讀目前部隊與能力。",
        "squad"=>$"部隊選擇。方向鍵切換要出擊的兵種，{confirm} 確認，{cancel} 返回。F3 重讀目前部隊與能力。",
        "save-confirm" or "gift-confirm"=>$"確認視窗。上下選擇是或否，{confirm} 確認，{cancel} 返回。F3 重讀問題與選擇。",
        "evolution"=>$"進化圖。方向鍵移動到不同型態或等級，{confirm} 確認，{cancel} 返回。F3 重讀能力介紹與素材需求；未解鎖與素材不足請依目前項目提示判斷。",
        "training"=>$"生命之樹。方向鍵選擇要培養的兵種，{confirm} 開啟，{cancel} 返回。F3 重讀目前兵種、空位和素材資訊。",
        "equipment-list"=>$"裝備或型態清單。方向鍵選擇，{confirm} 更換，{cancel} 返回個別士兵頁。F3 重讀名稱與持有數量。",
        "unit"=>$"個別士兵裝備頁。{left}、{right} 切換士兵，上下選擇裝備或型態欄，{confirm} 開啟清單，{cancel} 返回。英雄頁切換兵種使用 {hero}。Tab 可能是永久退役，請先聽確認內容。",
        "inventory"=>$"道具清單。方向鍵選擇，F3 重讀名稱與數量，{confirm} 執行目前選項，{cancel} 返回。",
        "settings"=>$"設定。上下選擇項目，左右調整，Q、E 切換分頁，{confirm} 確認，{cancel} 返回。F3 重讀目前設定。",
        "popup"=>$"設定確認或按鍵重綁視窗。請依 F3 重讀的問題操作，{cancel} 返回；重綁時輸入想綁定的按鍵。",
        "name"=>"神明名稱輸入。直接輸入文字，Backspace 刪除，使用遊戲確認鍵完成。F3 重讀目前名稱。",
        "opening"=>"誓約書。方向鍵移動羽毛，Tab 移到同意，再按遊戲確認鍵。F3 重讀內文。",
        "treasure"=>$"寶箱結算。{confirm} 繼續，F3 重讀目前獲得的物品。",
        "title"=>$"標題選單。上下選擇，{confirm} 確認，{cancel} 返回。F3 重讀目前項目。",
        "battle"=>"戰鬥中。依節奏輸入太鼓指令；綁定按鍵可在 F1 逐項查看。F3 重聽目前提示，F8 查詢射程。",
        "menu"=>$"選單。方向鍵選擇，{confirm} 確認，{cancel} 返回。F3 重讀目前項目。",
        _=>"目前尚未辨識到可提供專用引導的介面。F3 重讀目前內容，F1 查看完整操作說明。"
    };
}

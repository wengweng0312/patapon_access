using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
namespace PataponAccess;

internal static class ModuleHelp
{
    public static bool Active {get;private set;}
    private static bool closing;
    private static int index;
    private static readonly List<(string text,string file)> items=new();
    private static readonly List<InputActionMap> maps=new();
    private static readonly int[] keys={0x26,0x28,0x0D,0x1B,0x24,0x23};
    private static readonly bool[] down=new bool[6];
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern bool PlaySound(string? file,IntPtr module,uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private static string Key(MultiPlatformInputManager.AllAction action)
    {var names=BindingNames.Read(action);return names.Count>0?string.Join("、",names.Distinct()):"目前未取得綁定，請查看遊戲按鍵設定";}
    private static void Add(string text,string file="")=>items.Add((text,file));
    private static void Build()
    {
        items.Clear();
        Add("戰鬥口號朗讀："+(BattleChants.Enabled?"開啟":"關閉")+"。按 Enter 切換；劇情和關鍵提示保留。","@battle-chants");
        Add("機關鼓點報讀："+DrumPuzzleAccess.Mode+"。按 Enter 切換鼓聲名稱或綁定按鍵，F2 重聽鼓點。","@drum-puzzle");
        Add("Tab：村落主選單開啟保存畫面。");
        Add("Tab：任務準備畫面出發。");
        Add("F：讓士兵退出出擊隊伍。");
        Add("R：叫出裝備選單。");
        Add("二代英雄裝備頁，切換兵種："+Key(MultiPlatformInputManager.AllAction.Menu_West)+"。英雄模式招式隨兵種改變。");
        Add("二代一般士兵切換型態：先離開裝備清單，回到個別士兵裝備頁，用上下鍵移到型態欄，按確認開啟型態清單，再選擇並確認。");
        Add("注意，個別士兵的相應頁面按 Tab 是永久退役，士兵會消失，請留意遊戲確認內容。");
        Add("方向鍵：操作遊戲選單。");
        Add("Enter：確認。");Add("Escape：返回上層選單。");
        Add("設定畫面：Q、E 切換分頁。");
        Add("放棄任務或投降："+Key(MultiPlatformInputManager.AllAction.InGame_Retire));
        Add("PATA 太鼓："+Key(MultiPlatformInputManager.AllAction.InGame_Pata));
        Add("PON 太鼓："+Key(MultiPlatformInputManager.AllAction.InGame_Pon));
        Add("DON 太鼓："+Key(MultiPlatformInputManager.AllAction.InGame_Don));
        Add("CHAKA 太鼓："+Key(MultiPlatformInputManager.AllAction.InGame_Chaka));
        Add("戰鬥視野往左："+Key(MultiPlatformInputManager.AllAction.InGame_CameraLeft));
        Add("戰鬥視野往右："+Key(MultiPlatformInputManager.AllAction.InGame_CameraRight));
        Add("模組 F1：開啟或關閉幫助。");Add("模組 F2：重讀目前項目。");
        Add("一般遊戲中 F3：讀取畫面文字。");Add("一般遊戲中 F4：停止朗讀。");
        Add("一般遊戲中 F6：測試攻擊預備音。");Add("一般遊戲中 F7：查詢射程。");
        Add("二代英雄模式歌聲。Enter 依序試聽起始段與兩段循環歌聲，Shift 加上下鍵調整音量。","@hero-song");
        Add("音效試聽：奇蹟剩十秒，三聲提示，一代二代共用。按 Enter 播放。","miracle-ending.wav");
        Add("音效試聽：靠近鼓點機關，柔和雙音。按 Enter 播放。","mechanism-near.wav");
        Add("音效試聽：一般敵人進入射程，兩聲上行。按 Enter 播放。","enemy-range.wav");
        Add("音效試聽：一般敵人離開射程，兩聲下行。按 Enter 播放。","enemy-range-leave.wav");
        Add("音效試聽：馬車進入射程，三聲鐘音上行。按 Enter 播放。","cart-range-enter.wav");
        Add("音效試聽：馬車離開射程，三聲鐘音下行。按 Enter 播放。","cart-range-leave.wav");
        Add("音效試聽：Boss 攻擊動作開始，單聲粗短音。按 Enter 播放。","boss-attack-start.wav");
        Add("音效試聽：蔬菜小遊戲接菜提示。按 Enter 播放。","vegetable-catch.wav");
        Add("音效試聽：攻擊預備文字提示，預設關閉。按 Enter 播放。","attack-ready.wav");
        Add("射程提示不保證命中；Boss 提示表示偵測到攻擊動作。");
        Add("幫助不暫停遊戲時間。不同畫面的操作用途，以遊戲提示為準。");
    }
    public static void Toggle()
    {
        if(Active){Close();return;}
        try {
            Build();maps.Clear();
            var manager=UnityEngine.Object.FindObjectOfType<MultiPlatformInputManager>();
            if(!manager||!manager.playerInput||!manager.playerInput.actions){Speech.Speak("目前無法隔離遊戲操作，請稍後再開啟幫助。");return;}
            foreach(var map in manager.playerInput.actions.actionMaps)if(map.enabled)maps.Add(map);
            foreach(var map in maps)map.Disable();
            Active=true;closing=false;index=0;
            for(int i=0;i<keys.Length;i++)down[i]=(GetAsyncKeyState(keys[i])&0x8000)!=0;
            Speech.Speak("模組幫助。上下鍵逐項瀏覽，Enter 試聽，Home 第一項，End 最後一項，F2 重讀，Escape 或 F1 關閉。幫助不暫停遊戲。"+Current());
        }catch(Exception e){Restore();Reader.Log.LogWarning("Help input: "+e.Message);Speech.Speak("無法開啟幫助，已恢復遊戲操作。");}
    }
    private static string Current()=>items[index].text+(items[index].file=="@hero-song"?" 音量 "+HeroSongVolume.Level+"，10 為原音量。":"")+(items[index].file.Length>0&&!items[index].file.StartsWith("@")?" 音量 "+CueAudio.Level(items[index].file)+"，Shift 加上下鍵調整並試聽。":"")+"，第 "+(index+1)+" 項，共 "+items.Count+" 項。";
    public static void Read()=>Speech.Speak(Current());
    private static void Move(int value){index=Math.Clamp(value,0,items.Count-1);Read();}
    private static void Adjust(int delta)
    {
        if(items[index].file=="@hero-song"){int songLevel=HeroSongVolume.Adjust(delta);Speech.Speak("音量 "+songLevel+(songLevel==0?"，靜音":""));HeroSongVolume.Preview();return;}
        if(items[index].file.Length==0||items[index].file.StartsWith("@")){Speech.Speak("請先移到音效試聽項目。");return;}
        int level=CueAudio.Adjust(items[index].file,delta);
        Speech.Speak("音量 "+level+(level==0?"，靜音":level==20?"，最大":""));
        CueAudio.Play(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,items[index].file));
    }
    public static void Close(){if(!Active)return;closing=true;PlaySound(null,IntPtr.Zero,0);Speech.Stop();}
    private static void Restore(){foreach(var map in maps)try{map.Enable();}catch(Exception e){Reader.Log.LogWarning("Help restore: "+e.Message);}maps.Clear();Active=false;closing=false;}
    private static void Play()
    {
        if(items[index].file=="@hero-song"){Speech.Stop();HeroSongVolume.Preview();return;}
        if(items[index].file=="@drum-puzzle"){string message=DrumPuzzleAccess.Toggle();Build();Speech.Speak(message);return;}
        if(items[index].file=="@battle-chants"){string message=BattleChants.Toggle();Build();Speech.Speak(message+"，劇情和關鍵提示保留。");return;}
        if(items[index].file.Length==0){Read();return;}
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,items[index].file);
        Speech.Stop();if(File.Exists(file))CueAudio.Play(file);else Speech.Speak("找不到這個音效檔案。");
    }
    public static void Handle(int key)
    {
        if(closing)return;
        if(key==1)Read();if(key==2)Move(index+1);if(key==3)Close();if(key==4)Move(index-1);if(key==5)Play();
    }
    public static void Tick()
    {
        if(closing){
            if(keys.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0)||(GetAsyncKeyState(0x70)&0x8000)!=0||(GetAsyncKeyState(0x73)&0x8000)!=0)return;
            Restore();DialogueAccess.Clear();CommonUi.Clear();Speech.Speak("已關閉模組幫助。");return;
        }
        // Keep the remembered maps disabled if the game re-enables one while help is open.
        foreach(var map in maps)if(map.enabled)map.Disable();
        for(int i=0;i<keys.Length;i++){
            bool pressed=(GetAsyncKeyState(keys[i])&0x8000)!=0;
            if(pressed&&!down[i]){bool shift=(GetAsyncKeyState(0x10)&0x8000)!=0;if(i==0){if(shift)Adjust(1);else Move(index-1);}if(i==1){if(shift)Adjust(-1);else Move(index+1);}if(i==2)Play();if(i==3)Close();if(i==4)Move(0);if(i==5)Move(items.Count-1);}
            down[i]=pressed;
        }
    }
}


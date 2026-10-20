using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
namespace PataponAccess;

internal static class ModuleHelp
{
    public static bool Active {get;private set;}
    private static bool closing;
    private static int index,category,section;
    private static readonly List<(int category,string text,string file)> catalog=new();
    private static readonly List<(string text,string file)> items=new();
    private static readonly List<InputActionMap> maps=new();
    private static readonly int[] keys={0x26,0x28,0x0D,0x1B,0x24,0x23,0x25,0x27};
    private static readonly bool[] down=new bool[8];
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] private static extern bool PlaySound(string? file,IntPtr module,uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private static string Key(MultiPlatformInputManager.AllAction action)
    {var names=BindingNames.Read(action);return names.Count>0?string.Join("、",names.Distinct()):I18n.T("ui.9dbf034b0801","目前未取得綁定，請查看遊戲按鍵設定");}
    private static void Add(string text,string file="")=>catalog.Add((section,text,file));
    private static void Build()
    {
        catalog.Clear();section=0;
        Add(I18nRuntime.HelpLabel,"@language");
        Add(I18n.T("ui.699375bfcebf","戰鬥口號朗讀：")+(BattleChants.Enabled?I18n.T("ui.f290f9167ad0","開啟"):I18n.T("ui.c7fdddf79eaa","關閉"))+I18n.T("ui.22ad37f82624","。按 Enter 切換；劇情和關鍵提示保留。"),"@battle-chants");
        Add(I18n.T("ui.b87d9c50189d","機關鼓點報讀：")+DrumPuzzleAccess.Mode+I18n.T("ui.5c0488f42d25","。按 Enter 切換鼓聲名稱或綁定按鍵，F3 重聽鼓點。"),"@drum-puzzle");
        section=1;
        Add(I18n.T("ui.6a3a0149a352","Tab：村落主選單開啟保存畫面。"));
        section=2;
        Add(I18n.T("ui.c65bb998008d","Tab：任務準備畫面出發。"));
        Add(I18n.T("ui.e2220938a3e4","F：讓士兵退出出擊隊伍。"));
        Add(I18n.T("ui.52e434884b61","R：叫出裝備選單。"));
        Add(I18n.T("ui.1d37748cfc88","二代英雄裝備頁，切換兵種：")+Key(MultiPlatformInputManager.AllAction.Menu_West)+I18n.T("ui.efdc6ff74039","。英雄模式招式隨兵種改變。"));
        Add(I18n.T("ui.3762ec547618","二代逐一換裝：先退出武器或型態清單，回到個別士兵裝備頁，按 ")+Key(MultiPlatformInputManager.AllAction.Menu_SelectLeft)+I18n.T("ui.60ab743988bb"," 或 ")+Key(MultiPlatformInputManager.AllAction.Menu_SelectRight)+I18n.T("ui.e5c113730a72"," 切換士兵，上下選裝備欄，確認開啟清單。"));
        Add(I18n.T("ui.7651f7ae1a09","二代一般士兵切換型態：先離開裝備清單，回到個別士兵裝備頁，用上下鍵移到型態欄，按確認開啟型態清單，再選擇並確認。"));
        Add(I18n.T("ui.47092f33eb11","注意，個別士兵的相應頁面按 Tab 是永久退役，士兵會消失，請留意遊戲確認內容。"));
        section=1;
        Add(I18n.T("ui.49b6888d4d5a","方向鍵：操作遊戲選單。"));
        Add(I18n.T("ui.e8e5b2e12eb3","Enter：確認。"));Add(I18n.T("ui.653ccace17b6","Escape：返回上層選單。"));
        Add(I18n.T("ui.c095713e5f84","設定畫面：Q、E 切換分頁。"));
        section=3;
        Add(I18n.T("ui.6f4d55ec38d4","放棄任務或投降：")+Key(MultiPlatformInputManager.AllAction.InGame_Retire));
        Add(I18n.T("ui.36eebfb6a276","PATA 太鼓：")+Key(MultiPlatformInputManager.AllAction.InGame_Pata));
        Add(I18n.T("ui.c29908de2818","PON 太鼓：")+Key(MultiPlatformInputManager.AllAction.InGame_Pon));
        Add(I18n.T("ui.c57e135f8be2","DON 太鼓：")+Key(MultiPlatformInputManager.AllAction.InGame_Don));
        Add(I18n.T("ui.8612906a37f4","CHAKA 太鼓：")+Key(MultiPlatformInputManager.AllAction.InGame_Chaka));
        Add(I18n.T("ui.09f26ed256bb","戰鬥視野往左：")+Key(MultiPlatformInputManager.AllAction.InGame_CameraLeft));
        Add(I18n.T("ui.2bba767681b0","戰鬥視野往右：")+Key(MultiPlatformInputManager.AllAction.InGame_CameraRight));
        section=1;
        Add(I18n.T("ui.6f3eac48e21e","模組 F1：開啟或關閉幫助。"));Add(I18n.T("ui.2d2aeaf571ea","模組 F2：目前介面的操作引導。"));
        Add(I18n.T("ui.eee5a2d92f87","模組 F3：重讀目前項目。"));Add(I18n.T("ui.e48318c23e89","一般遊戲中 F4：讀取畫面文字。"));Add(I18n.T("ui.2a7738f3a3e8","一般遊戲中 F5：停止朗讀。"));
        Add(I18n.T("ui.debb776ea38c","一般遊戲中 F7：測試攻擊預備音。"));Add(I18n.T("ui.b013158db30d","一般遊戲中 F8：查詢射程。"));
        section=4;
        Add(I18n.T("dance.notePreview","音效試聽：舞蹈一般音符，提前一拍的一聲短敲棒音。聽到後等一拍再按。"),DanceCueAccess.NoteFile);
        Add(I18n.T("dance.preview","音效試聽：舞蹈連按開始，三聲短敲棒音。聽到後立即快速連按，不是只按三下。"),DanceCueAccess.File);
        Add(I18n.T("ui.b4cacbeeef5c","二代英雄模式歌聲。Enter 依序試聽起始段與兩段循環歌聲，Shift 加上下鍵調整音量。"),"@hero-song");
        Add(I18n.T("ui.96d9e19ab2d6","音效試聽：奇蹟剩十秒，三聲提示，一代二代共用。按 Enter 播放。"),"miracle-ending.wav");
        Add(I18n.T("ui.5b9c5cfedcff","音效試聽：靠近鼓點機關，柔和雙音。按 Enter 播放。"),"mechanism-near.wav");
        Add(I18n.T("ui.df6701f9db99","音效試聽：一般敵人進入射程，兩聲上行。按 Enter 播放。"),"enemy-range.wav");
        Add(I18n.T("ui.6c48fb523e51","音效試聽：一般敵人離開射程，兩聲下行。按 Enter 播放。"),"enemy-range-leave.wav");
        Add(I18n.T("ui.399f68b42f52","音效試聽：馬車進入射程，三聲鐘音上行。按 Enter 播放。"),"cart-range-enter.wav");
        Add(I18n.T("ui.8f7d93c32c19","音效試聽：馬車離開射程，三聲鐘音下行。按 Enter 播放。"),"cart-range-leave.wav");
        Add(I18n.T("ui.7ba7a80c9ab0","音效試聽：Boss 攻擊動作開始，單聲粗短音。按 Enter 播放。"),"boss-attack-start.wav");
        Add(I18n.T("ui.f92ba1af928f","音效試聽：蔬菜小遊戲接菜提示。按 Enter 播放。"),"vegetable-catch.wav");
        Add(I18n.T("ui.58d2a1993b43","音效試聽：攻擊預備文字提示，預設關閉。按 Enter 播放。"),"attack-ready.wav");
        section=3;
        Add(I18n.T("ui.075d69d73094","射程提示不保證命中；Boss 提示表示偵測到攻擊動作。"));
        section=1;
        Add(I18n.T("ui.e913fe294cdd","幫助不暫停遊戲時間。不同畫面的操作用途，以遊戲提示為準。"));
        items.Clear();items.AddRange(catalog.Where(item=>item.category==category).Select(item=>(item.text,item.file)));
        index=Math.Clamp(index,0,Math.Max(0,items.Count-1));
    }
    private static string CategoryName()=>category switch {
        0=>I18n.T("help.settings","模組設定"),1=>I18n.T("help.general","基本操作"),2=>I18n.T("help.units","部隊與裝備"),3=>I18n.T("help.battle","戰鬥按鍵"),_=>I18n.T("help.sounds","音效試聽")};
    private static void Category(int delta){category=(category+delta+5)%5;index=0;Build();Speech.Speak(CategoryName()+"。"+Current());}
    public static void Toggle()
    {
        if(Active){Close();return;}
        try {
            Build();maps.Clear();
            var manager=UnityEngine.Object.FindObjectOfType<MultiPlatformInputManager>();
            if(!manager||!manager.playerInput||!manager.playerInput.actions){Speech.Speak(I18n.T("ui.250529d0c7f9","目前無法隔離遊戲操作，請稍後再開啟幫助。"));return;}
            foreach(var map in manager.playerInput.actions.actionMaps)if(map.enabled)maps.Add(map);
            foreach(var map in maps)map.Disable();
            Active=true;closing=false;index=0;category=0;Build();
            for(int i=0;i<keys.Length;i++)down[i]=(GetAsyncKeyState(keys[i])&0x8000)!=0;
            Speech.Speak(I18n.T("help.navigation","模組幫助。左右切換分類，上下選擇項目。Enter 試聽或切換設定，Shift 加上下調整音量。Home、End 到目前分類的第一項、最後一項。F3 重讀，Escape 或 F1 關閉。幫助不暫停遊戲。")+CategoryName()+"。"+Current());
        }catch(Exception e){Restore();Reader.Log.LogWarning("Help input: "+e.Message);Speech.Speak(I18n.T("ui.556ce97a93cf","無法開啟幫助，已恢復遊戲操作。"));}
    }
    private static string Current()=>items[index].text+(items[index].file=="@hero-song"?I18n.T("ui.d5f9754fed9c"," 音量 ")+HeroSongVolume.Level+I18n.T("ui.91ea4c8feebb","，10 為原音量。"):"")+(items[index].file.Length>0&&!items[index].file.StartsWith("@")?I18n.T("ui.d5f9754fed9c"," 音量 ")+CueAudio.Level(items[index].file)+I18n.T("ui.b30e14322353","，Shift 加上下鍵調整並試聽。"):"")+I18n.T("ui.fab2a038b6d0","，第 ")+(index+1)+I18n.T("ui.b850d8d5860c"," 項，共 ")+items.Count+I18n.T("ui.fc497d3162e9"," 項。");
    public static void Read()=>Speech.Speak(Current());
    private static void Move(int value){index=Math.Clamp(value,0,items.Count-1);Read();}
    private static void Adjust(int delta)
    {
        if(items[index].file=="@hero-song"){int songLevel=HeroSongVolume.Adjust(delta);Speech.Speak(I18n.T("ui.744e10238e2b","音量 ")+songLevel+(songLevel==0?I18n.T("ui.5a39ff9069e2","，靜音"):""));HeroSongVolume.Preview();return;}
        if(items[index].file.Length==0||items[index].file.StartsWith("@")){Speech.Speak(I18n.T("ui.236fa05cca24","請先移到音效試聽項目。"));return;}
        int level=CueAudio.Adjust(items[index].file,delta);
        Speech.Speak(I18n.T("ui.744e10238e2b","音量 ")+level+(level==0?I18n.T("ui.5a39ff9069e2","，靜音"):level==20?I18n.T("ui.854e59549e5f","，最大"):""));
        CueAudio.Play(Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,items[index].file));
    }
    public static void Close(){if(!Active)return;closing=true;PlaySound(null,IntPtr.Zero,0);Speech.Stop();}
    private static void Restore(){foreach(var map in maps)try{map.Enable();}catch(Exception e){Reader.Log.LogWarning("Help restore: "+e.Message);}maps.Clear();Active=false;closing=false;}
    private static void Play()
    {
        if(items[index].file=="@language"){string message=I18nRuntime.Cycle();Build();Speech.Speak(message);return;}
        if(items[index].file=="@hero-song"){Speech.Stop();HeroSongVolume.Preview();return;}
        if(items[index].file=="@drum-puzzle"){string message=DrumPuzzleAccess.Toggle();Build();Speech.Speak(message);return;}
        if(items[index].file=="@battle-chants"){string message=BattleChants.Toggle();Build();Speech.Speak(message+I18n.T("ui.3c861b381ce3","，劇情和關鍵提示保留。"));return;}
        if(items[index].file.Length==0){Read();return;}
        string file=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,items[index].file);
        Speech.Stop();if(File.Exists(file))CueAudio.Play(file);else Speech.Speak(I18n.T("ui.d4e850ded47e","找不到這個音效檔案。"));
    }
    public static void Handle(int key)
    {
        if(closing)return;
        if(key==2)Read();if(key==3)Move(index+1);if(key==4)Close();if(key==5)Move(index-1);if(key==6)Play();
    }
    public static void Tick()
    {
        if(closing){
            if(keys.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0)||(GetAsyncKeyState(0x70)&0x8000)!=0||(GetAsyncKeyState(0x74)&0x8000)!=0)return;
            Restore();DialogueAccess.Clear();CommonUi.Clear();Speech.Speak(I18n.T("ui.9e7695356b84","已關閉模組幫助。"));return;
        }
        // Keep the remembered maps disabled if the game re-enables one while help is open.
        foreach(var map in maps)if(map.enabled)map.Disable();
        for(int i=0;i<keys.Length;i++){
            bool pressed=(GetAsyncKeyState(keys[i])&0x8000)!=0;
            if(pressed&&!down[i]){bool shift=(GetAsyncKeyState(0x10)&0x8000)!=0;if(i==0){if(shift)Adjust(1);else Move(index-1);}if(i==1){if(shift)Adjust(-1);else Move(index+1);}if(i==2)Play();if(i==3)Close();if(i==4)Move(0);if(i==5)Move(items.Count-1);if(i==6)Category(-1);if(i==7)Category(1);}
            down[i]=pressed;
        }
    }
}


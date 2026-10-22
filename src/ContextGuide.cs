using UnityEngine;
namespace PataponAccess;
internal static class ContextGuide
{
    private static string Key(MultiPlatformInputManager.AllAction action,string fallback){var names=BindingNames.Read(action);return names.Count>0?string.Join("、",names.Distinct()):fallback;}
    public static void Speak()
    {
        if(BossRushDescriptionAccess.RepeatText.Length>0){Speech.Speak(BossRushDescriptionAccess.RepeatText);return;}
        if(SpecialTitleHintAccess.Open())return;
        string context=Resolve();
        string message=ContextGuideText.Describe(context,Key(MultiPlatformInputManager.AllAction.Menu_South,I18n.T("ui.5df3b328c161","遊戲確認鍵")),Key(MultiPlatformInputManager.AllAction.Menu_East,I18n.T("ui.b66fe4e6d8ec","遊戲返回鍵")),Key(MultiPlatformInputManager.AllAction.Menu_SelectLeft,I18n.T("ui.37ce894c368a","左鍵")),Key(MultiPlatformInputManager.AllAction.Menu_SelectRight,I18n.T("ui.147ec6c37f3b","右鍵")),Key(MultiPlatformInputManager.AllAction.Menu_West,I18n.T("ui.8b06d9180b19","遊戲對應功能鍵")));
        if(context=="dance")message=DanceCueAccess.Guide();
        if(context=="village")message=ContextGuideText.Village(Key(MultiPlatformInputManager.AllAction.Menu_LeftShoulder,"Q"),Key(MultiPlatformInputManager.AllAction.Menu_RightShoulder,"E"),Key(MultiPlatformInputManager.AllAction.Menu_South,"Enter"),Key(MultiPlatformInputManager.AllAction.Menu_Select,"Tab"));
        string previous=Speech.Last;Speech.Speak(message);Speech.Last=previous;
        Monitor.Record("context-guide",new{context,text=message});
    }
    private static string Resolve()
    {
        if(ModuleHelp.Active)return "help";
        if(DanceCueAccess.Active)return "dance";
        if(BalloonChoiceAccess.Active)return "save-confirm";
        if(SettingsPopupAccess.RepeatText.Length>0)return "popup";
        var settings=UnityEngine.Object.FindObjectOfType<SettingMain>();
        if(settings&&settings.gameObject.activeInHierarchy&&!settings.IsClosed())return "settings";
        if(StructuredUi.ContextKind is "save-confirm" or "gift-confirm")return StructuredUi.ContextKind;
        if(NameInputAccess.IsActive)return "name";
        if(OpeningAccess.IsActive)return "opening";
        if(P2EvolutionAccess.IsActive)return "evolution";
        if(P2CampNavigation.ItemMenuActive)return "equipment-list";
        if(P2CampNavigation.UnitPageActive)return "unit";
        if(ItemAndPlaceAccess.EquipmentActive)return "equipment-list";
        if(ItemAndPlaceAccess.ItemActive)return "inventory";
        if(P2SquadAccess.Selecting||RushP2SquadAccess.Selecting)return "squad";
        if(P2SquadAccess.Preparing||RushP2SquadAccess.Preparing)return "preparation";
        if(StructuredUi.ContextKind.Length>0)return StructuredUi.ContextKind;
        if(P2CampNavigation.TreeActive)return "training";
        if(P2TreasureAccess.IsActive||RewardAccess.IsResultActive)return "treasure";
        if(TitleAccess.IsActive)return "title";
        if(BattleMonitor.Active)return "battle";
        if(CommonUi.CommandWindowActive)return "menu";
        if(ItemAndPlaceAccess.CampActive)return "village";
        return "unknown";
    }
}

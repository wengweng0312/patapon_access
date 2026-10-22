using HarmonyLib;
using System.Reflection;
namespace PataponAccess;
internal static class P2SquadAccess
{
    private static string last="";
    private static long nextRead,lastError,selectionSeen,nextStatus;
    private static long preparationSeen;
    public static bool Preparing=>Environment.TickCount64-preparationSeen<350;
    public static bool Selecting=>Environment.TickCount64-selectionSeen<300;
    public static void Install(Harmony h)
    {
        try{
            h.Patch(typeof(P2.Bases.HeadQuarters.SquadSelectView).GetMethod("update"),postfix:new HarmonyMethod(typeof(P2SquadAccess).GetMethod(nameof(Observe),BindingFlags.Static|BindingFlags.NonPublic)));
            h.Patch(typeof(P2.Bases.HeadQuarters.SquadStatusView).GetMethod("update"),postfix:new HarmonyMethod(typeof(P2SquadAccess).GetMethod(nameof(Status),BindingFlags.Static|BindingFlags.NonPublic)));
            h.Patch(typeof(P2.Bases.HeadQuarters.SquadUI).GetMethod("update"),postfix:new HarmonyMethod(typeof(P2SquadAccess).GetMethod(nameof(Preparation),BindingFlags.Static|BindingFlags.NonPublic)));
        }catch(Exception e){Reader.Log.LogWarning("P2 squad selector hook: "+e.Message);}
    }
    private static void Preparation(P2.Bases.HeadQuarters.SquadUI __instance)
    {
        try{
            var model=__instance.squadModel_;
            if(!__instance.isAllow_||model==null||!model.isReady_||model.isFinish_)return;
            if(model.mode_!=P2.Bases.HeadQuarters.SquadModel.Mode.Mode_Organization&&model.mode_!=P2.Bases.HeadQuarters.SquadModel.Mode.Mode_EggChanging)return;
            bool changed=!Preparing;preparationSeen=Environment.TickCount64;
            if(changed)Monitor.Record("p2-preparation-context",new{source="SquadUI.isAllow/SquadModel",mode=model.mode_.ToString(),state=model.state_.ToString()});
        }catch(Exception e){if(Environment.TickCount64-lastError>5000){lastError=Environment.TickCount64;Monitor.Record("p2-preparation-context-error",new{error=e.Message});}}
    }
    private static void Observe(P2.Bases.HeadQuarters.SquadSelectView __instance)
    {
        try{
            var view=__instance;var model=view.model_;
            if(!view.isActive_||model==null||!model.isActive_||ItemAndPlaceAccess.EquipmentActive)return;
            long now=Environment.TickCount64;selectionSeen=now;if(now<nextRead)return;nextRead=now+100;
            int index=model.slotID_;var options=model.effectiveCharaList_;
            if(options==null||index<0||index>=options.Count)return;
            uint chara=unchecked((uint)options[index]);
            var names=P2.Bases.Organization.Managed.UnitStatusSlot.charaTypeToCharaNameTable_g;
            string? name=names!=null&&chara<names.Length?UnitNames.FetchLocalized("P2",LocalizeIds.Unit(names[(int)chara])):null;
            if(string.IsNullOrWhiteSpace(name))name=I18n.T("ui.d5ce891d09e6","兵種名稱未取得");
            int missions=model.missionCountList_!=null&&index<model.missionCountList_.Count?(int)model.missionCountList_[index]:0;
            var values=SquadScreenValues.Read();
            // This selector shows missions, not a Reborn counter. Do not invent one.
            string text=string.Join("。",new[]{name}.Concat(SquadScreenValues.Headers));
            for(int i=0;i<values.Length;i++)if(!string.IsNullOrWhiteSpace(values[i])&&!string.IsNullOrWhiteSpace(SquadScreenValues.Labels[i]))text+="。"+SquadScreenValues.Labels[i]+" "+values[i];
            if(values.Any(string.IsNullOrWhiteSpace))text+=I18n.T("ui.3078a2e6ac92","。部分能力數值未取得");
            StructuredUi.PublishSquad(text);
            string key=chara+":"+index+":"+text;
            if(key!=last){last=key;Monitor.Record("p2-headquarters-squad-selection",new{index,total=options.Count,chara,name,missions,values,source="SquadSelectView.model.slotID/effectiveCharaList/native-name-table"});}
        }catch(Exception e){if(Environment.TickCount64-lastError>5000){lastError=Environment.TickCount64;Monitor.Record("p2-squad-selection-error",new{error=e.Message});}}
    }
    private static void Status(P2.Bases.HeadQuarters.SquadStatusView __instance)
    {
        try{
            long now=Environment.TickCount64;
            if(!__instance.isActive_||__instance.fade_<0.05f||now-selectionSeen<300||now<nextStatus||ItemAndPlaceAccess.EquipmentActive)return;
            var model=__instance.model_;if(model==null)return;
            var squad=model.getCurSquad();if(squad==null)return;
            nextStatus=now+100;
            string? name;
            if(squad.isHero_)name=model.getHeroName(model.selectLineIndex_);
            else{var param=squad.getParam();name=param==null?null:UnitNames.FetchLocalized("P2",LocalizeIds.Unit(unchecked((uint)param.charNameId)));}
            if(string.IsNullOrWhiteSpace(name))name=I18n.T("ui.d5ce891d09e6","兵種名稱未取得");
            var values=SquadScreenValues.Read();
            string text=string.Join("。",new[]{name}.Concat(SquadScreenValues.Headers));
            for(int i=0;i<values.Length;i++)if(!string.IsNullOrWhiteSpace(values[i])&&!string.IsNullOrWhiteSpace(SquadScreenValues.Labels[i]))text+="。"+SquadScreenValues.Labels[i]+" "+values[i];
            if(values.Any(string.IsNullOrWhiteSpace))text+=I18n.T("ui.3078a2e6ac92","。部分能力數值未取得");
            StructuredUi.PublishSquad(text,"preparation");
            string key="status:"+model.selectLineIndex_+":"+text;
            if(key!=last){last=key;Monitor.Record("p2-headquarters-squad-status",new{name,values,text,source="SquadStatusView.currentSquad"});}
        }catch(Exception e){if(Environment.TickCount64-lastError>5000){lastError=Environment.TickCount64;Monitor.Record("p2-squad-status-error",new{error=e.Message});}}
    }
}

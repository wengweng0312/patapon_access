using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace PataponAccess;
internal static class NativeRushScreenAccess
{
    private static string candidate="",spoken="",repeat="";
    private static long changed;
    private static bool active;
    public static string RepeatText=>active?repeat:"";
    public static bool Scan()
    {
        foreach(var canvas in UnityEngine.Object.FindObjectsOfType<P1S.Game.BossRushUI.BossRushUICanvas>())
            if(Read(canvas,"P1S"))return true;
        foreach(var canvas in UnityEngine.Object.FindObjectsOfType<P2S.Game.BossRushUI.BossRushUICanvas>())
            if(Read(canvas,"P2S"))return true;
        active=false;candidate=spoken=repeat="";return false;
    }
    private static object? Get(object value,string field)=>value.GetType().GetProperty(field)?.GetValue(value);
    private static bool Read(MonoBehaviour owner,string game)
    {
        if(!owner||!owner.gameObject.activeInHierarchy)return false;
        var canvas=Get(owner,"_canvas") as Canvas;var group=Get(owner,"_canvasGroup") as CanvasGroup;
        if(canvas is null||!canvas||!canvas.isActiveAndEnabled||group is null||!group||group.alpha<.95f)return false;
        string type=Get(owner,"_uiType")?.ToString()??"";
        if(type is not ("StageClear" or "Progress"))return false;
        var page=Get(owner,type=="StageClear"?"_stageClearUI":"_progressUI") as MonoBehaviour;
        if(page is null||!page||!page.gameObject.activeInHierarchy)return false;
        // Native modal dialogs have priority over the completion panel behind them.
        var confirmation=Get(owner,"ConfirmationWindow") as PauseConfirmationWindow;
        var reward=Get(owner,"RewardWindow") as BossRushRewardWindow;
        if((confirmation is not null&&confirmation&&!confirmation.IsClosed())||(reward is not null&&reward&&!reward.IsClosed()))return false;
        var parts=new List<string>();
        if(type=="StageClear"&&page.GetComponentsInChildren<Image>().Any(i=>i&&i.isActiveAndEnabled&&i.sprite&&i.sprite.name=="sp_clear"))parts.Add("CLEAR!");
        foreach(var text in page.GetComponentsInChildren<TMP_Text>()){
            string value=VisibleUiText.Read(text);if(value.Length==0)continue;
            string binding="";
            if(text.name=="Decision")binding=BindingNames.FromSprite(text.text,"Menu_South");
            else if(text.name=="Return")binding=BindingNames.FromSprite(text.text,"Menu_East");
            value=DialogueText.Clean(value).TrimStart('：',':',' ');
            parts.Add(binding.Length>0?binding+"，"+value:value);
        }
        string output=string.Join("\n",parts.Distinct());if(output.Length==0)return false;
        active=true;repeat=output;
        string signature=game+":"+owner.GetInstanceID()+":"+type+":"+output;
        long now=Environment.TickCount64;
        if(candidate!=signature){candidate=signature;changed=now;}
        if(spoken!=signature&&now-changed>=180){spoken=signature;Speech.Speak(output,interrupt:false);Monitor.Record("boss-rush-screen-text",new{game,type,text=output,source="active native BossRushUICanvas page; original TMP labels and visually checked CLEAR artwork"});}
        return true;
    }
}


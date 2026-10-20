namespace PataponAccess;
internal sealed class CannonWarningState
{
    private bool cannon;
    public void Reset()=>cannon=false;
    public bool Observe(bool active,string source,string text)
    {
        if(!active||source!="P2.Game.Window.Message.openW")return false;
        if(text=="我們是負責守護 這片土地的 砲台三兄弟…")cannon=true;
        return cannon&&text=="３０…５０…８０…！ 填充能量 百分之１２０！";
    }
}

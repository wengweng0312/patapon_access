namespace PataponAccess;

// Native action identifiers are query inputs only, never a label fallback.
internal static class KeySpriteLabel
{
    public static string Resolve(string sprite,string action,IEnumerable<string> candidates,Func<string,string,string> query)
    {
        foreach(string candidate in candidates)if(query(action,candidate)==sprite)return candidate;
        return "";
    }
}

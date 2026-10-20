namespace PataponAccess;

// IDs refer only to verified common command chatter. Unknown lines remain audible.
// Read the game's current locale; never translate its dialogue through I18n.
internal static class NativeDialogueRoles
{
    private static string game="";
    private static long nextRead;
    private static bool ready;
    public static void Begin(string value)
    {
        game=value;ready=false;nextRead=0;
        BattleChantText.RegisterNative(Array.Empty<string>(),Array.Empty<string>());
        AttackCueText.RegisterNative(Array.Empty<string>());
        Tick();
    }
    public static void Tick()
    {
        if(ready||!BattleMonitor.Active||Environment.TickCount64<nextRead)return;
        nextRead=Environment.TickCount64+3000;
        try {
            string[] lines;
            int[] chant,tactical,attackReady;
            if(game=="P1") {
                var table=P1.LaboCommon.pLaboCommonInstance_g?.getMissionData?.message;
                if(table==null||!table.ContainsKey("missionid_0010"))return;
                var messages=table["missionid_0010"]?.messages;
                if(messages==null||messages.Length<149)return;
                lines=messages.Select(s=>DialogueText.Clean(s)).ToArray();
                chant=new[]{97,98,99,104,106,107,108,118,129,131,136,137,140,145,146,147,148};
                tactical=new[]{125,134,135,142,143,144};
                attackReady=new[]{102,103,109,112};
            } else if(game=="P2") {
                var table=P2.LaboCommon.pLaboCommonInstance_g?.getMissionData?.message;
                if(table==null||!table.ContainsKey("missionid_10000"))return;
                var messages=table["missionid_10000"]?.messages;
                if(messages==null||messages.Length<221)return;
                lines=messages.Select(s=>DialogueText.Clean(s)).ToArray();
                chant=new[]{27,28,167,168,169,174,176,177,178,188,191,199,201,206,207,208,209,210,215,216,217,218,219,220};
                tactical=new[]{190,192,195,203,204,205,211,212,213,214};
                attackReady=new[]{172,173,179,182};
            } else return;
            BattleChantText.RegisterNative(chant.Select(i=>lines[i]),tactical.Select(i=>lines[i]));
            AttackCueText.RegisterNative(attackReady.Select(i=>lines[i]));
            ready=true;
            Monitor.Record("native-dialogue-roles",new{game,chantIds=chant,tacticalIds=tactical,attackReadyIds=attackReady,source="current-game-language-common-message-ids"});
        } catch(Exception e) {
            Monitor.Record("native-dialogue-roles-unavailable",new{game,error=e.Message});
        }
    }
}

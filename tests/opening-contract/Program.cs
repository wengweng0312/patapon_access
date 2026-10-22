using PataponAccess;
int count=0;
void Check(bool pass,string label){if(!pass)throw new Exception(label);count++;}
foreach(string game in new[]{"P1","P2"}){
 var distinct=new HashSet<string>();
 foreach(int language in new[]{8,7,1,0}){
  string? text=OpeningContractText.Resolve(game,language);
  Check(text?.Length>80,$"{game} language {language} has full prose");
  Check(distinct.Add(text!),$"{game} language {language} is distinct");
 }
 Check(OpeningContractText.Resolve(game,null)==null,game+" unknown language has no wrong-language fallback");
 Check(OpeningContractText.Resolve(game,99)==null,game+" unsupported language has no wrong-language fallback");
}
Check(OpeningContractText.Resolve("P2",7,"native contract")=="native contract","P2 live game text preferred");
Check(OpeningContractText.Resolve("P2",7,"")==OpeningContractText.Resolve("P2",7),"P2 temporarily empty text uses verified same-language resource");
Check(OpeningContractText.Resolve("P1",7,"wrong game prose")!= "wrong game prose","P1 cannot use P2 live prose");
Check(OpeningContractText.Resolve("P1",7)!=OpeningContractText.Resolve("P2",7),"each game uses its own oath");
Check(OpeningContractText.Resolve("P3",7,"native")==null,"unknown game refused");
// Verify the actual shipped assembly, not merely this fixture's resource configuration.
var built=System.Reflection.Assembly.LoadFile(Path.GetFullPath("work/mod/bin/Release/net6.0/PataponAccess.dll"));
Check(built.GetManifestResourceNames().Contains("game-text.opening-contracts.json"),"production resource name matches reader");
var method=built.GetType("PataponAccess.OpeningContractText")!.GetMethod("Resolve",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public)!;
foreach(string game in new[]{"P1","P2"})foreach(int language in new[]{8,7,1,0}){
 var actual=(string?)method.Invoke(null,new object?[]{game,language,null});
 Check(actual==OpeningContractText.Resolve(game,language),$"production {game}/{language} reads expected full text");
}
Console.WriteLine($"{count} opening contract cases passed.");

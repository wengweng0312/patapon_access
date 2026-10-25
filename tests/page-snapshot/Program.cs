using PataponAccess;
int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
var page=new PageSnapshotState();page.Request("scene");page.Request("focus");
Check(page.Pending&&page.Reasons.Length==2,"coalesce same-frame page events");
Check(page.Commit("{title:騎士PON的馬很臭？,body:像摩吉吉等,texture:312}"),"first page retained even when no speech emitted");
Check(page.Revision==1&&!page.Pending,"atomic page revision and pending reset");
page.Request("focus");Check(!page.Commit("{title:騎士PON的馬很臭？,body:像摩吉吉等,texture:312}"),"identical snapshot not repeated");
Check(page.Commit("{title:騎士PON的馬很臭？,body:像摩吉吉等,texture:313}"),"texture-only page change retained");
Check(page.Commit("{title:新的PATAPON,focus:常見的樹枝}"),"ordinary menu changes use same common observer");
for(int i=0;i<1000;i++)page.Request("event"+i);
Check(page.Reasons.Length==8,"bounded accumulated reasons");page.Commit("{}");Check(page.Revision==4,"revision monotonically advances");
Console.WriteLine($"{checks} page diagnostic state checks; Unity capture requires runtime verification.");
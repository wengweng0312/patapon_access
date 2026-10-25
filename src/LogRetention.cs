using System.Text.RegularExpressions;

namespace PataponAccess;

internal static class LogRetention
{
    // Only our generated session filenames, directly inside the log directory.
    private static readonly Regex Names=new(@"^(\d{8}-\d{6}-\d+)-[0-4]\.jsonl$",RegexOptions.CultureInvariant);
    public static void Clean(string directory,int days,int maxMb,Action<string> report,long reserveBytes=0)
    {
        try {
            var root=new DirectoryInfo(directory);
            if((root.Attributes&FileAttributes.ReparsePoint)!=0)return;
            var sessions=root.GetFiles("*.jsonl",SearchOption.TopDirectoryOnly)
                .Where(f=>Names.IsMatch(f.Name)&&(f.Attributes&FileAttributes.ReparsePoint)==0)
                .GroupBy(f=>Names.Match(f.Name).Groups[1].Value)
                .Select(g=>new {Files=g.ToArray(),Newest=g.Max(f=>f.LastWriteTimeUtc),Bytes=g.Sum(f=>f.Length)})
                .OrderBy(g=>g.Newest).ToArray();
            long total=sessions.Sum(g=>g.Bytes),limit=Math.Max(0,(long)Math.Clamp(maxMb,50,10240)*1024*1024-Math.Max(0,reserveBytes));
            var cutoff=DateTime.UtcNow.AddDays(-Math.Clamp(days,1,3650));
            int removed=0,skipped=0;
            foreach(var session in sessions) {
                if(session.Newest>=cutoff&&total<=limit)continue;
                // Acquire every part exclusively before deleting any: another running game is protected.
                var handles=new List<FileStream>();
                try {
                    foreach(var file in session.Files)handles.Add(new FileStream(file.FullName,FileMode.Open,FileAccess.Read,FileShare.Delete));
                    foreach(var file in session.Files){File.Delete(file.FullName);total-=file.Length;removed++;}
                }catch(IOException){skipped++;}
                catch(UnauthorizedAccessException){skipped++;}
                finally{foreach(var handle in handles)handle.Dispose();}
            }
            report($"Log cleanup: removed {removed} files; skipped {skipped} unavailable sessions; retained approximately {total} bytes.");
        }catch(Exception e){report("Log cleanup skipped: "+e.Message);}
    }
}

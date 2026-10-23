namespace PataponAccess;

// Captured game sentences retain their own text, variable template and timing.
// Selection focus still uses its existing latest-selection policy separately.
internal sealed class DialogueQueue
{
    internal sealed class Entry
    {
        public readonly string Text,Hint,Source;
        public readonly string Description;
        public string Template;
        public readonly long CapturedAt;
        public long ReadyAt;
        public Entry(string text,string template,string hint,string source,long at,string description="")
        {Text=text;Template=template;Hint=hint;Source=source;Description=description;CapturedAt=at;ReadyAt=at+60;}
    }
    private readonly Queue<Entry> entries=new();
    public int Count=>entries.Count;
    public void Add(string text,string template,string hint,string source,long at,string description="")
    {if(!string.IsNullOrWhiteSpace(text))entries.Enqueue(new(text,template,hint,source,at,description));}
    public Entry? Peek(long now)=>entries.TryPeek(out var entry)&&now>=entry.ReadyAt?entry:null;
    public void Complete(Entry entry)
    {if(entries.TryPeek(out var first)&&ReferenceEquals(first,entry))entries.Dequeue();}
    public int Clear(){int count=entries.Count;entries.Clear();return count;}
}

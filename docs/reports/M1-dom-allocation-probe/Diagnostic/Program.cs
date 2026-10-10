using System.Reflection;
using System.Runtime.CompilerServices;
using Xemele.Parsing.Dom;

internal static class Program
{
    private delegate string NameProbe(ReadOnlySpan<char> name);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Hits(NameProbe probe, string borrowed, string owned)
    {
        for (int i = 0; i < 10000; i++)
            if (!ReferenceEquals(owned, probe(borrowed.AsSpan())))
                throw new InvalidOperationException("Borrowed hit did not reuse owned name.");
    }
    private static void Main()
    {
        long counter = GC.GetAllocatedBytesForCurrentThread();
        long counterOnly = GC.GetAllocatedBytesForCurrentThread() - counter;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var sink = new DomSink(new());
        using var core = new Xemele.Parsing.Syntax.XmlParserCore(new());
        core.Append("<r xmlns:p='urn:p' p:a='v'>", [], false, sink, default);
        long setup = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var probe = typeof(DomSink).GetMethod("OwnName", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<NameProbe>(sink);
        string owned = sink.Document.RootElement.LocalName;
        string borrowed = new string(new[] { 'r' });
        long reflection = GC.GetAllocatedBytesForCurrentThread() - before;
        if (ReferenceEquals(owned, borrowed)) throw new InvalidOperationException("Probe must use independently owned input.");
        before = GC.GetAllocatedBytesForCurrentThread();
        string first = probe(borrowed.AsSpan());
        long firstHit = GC.GetAllocatedBytesForCurrentThread() - before;
        if (!ReferenceEquals(first, owned)) throw new InvalidOperationException("Identity failure.");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) probe(borrowed.AsSpan());
        long originalWarm = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            if (!ReferenceEquals(owned, probe(borrowed.AsSpan()))) throw new InvalidOperationException("Identity failure.");
        long originalMeasured = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        Hits(probe, borrowed, owned);
        long helperFirst = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        Hits(probe, borrowed, owned);
        long helperWarm = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; counter={counterOnly}; DOM/setup={setup}; reflection/input={reflection}; firstHit={firstHit}; originalWarm={originalWarm}; originalMeasured={originalMeasured}; helperFirst={helperFirst}; helperWarm={helperWarm}; iterations=10000; independentInput=true; identity=true");
    }
}

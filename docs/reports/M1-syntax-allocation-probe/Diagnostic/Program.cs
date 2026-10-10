using System.Runtime.CompilerServices;
using Xemele.Parsing.Syntax;

internal static class Program
{
    private const string Chunk = "<c a='v' xmlns:p='u' p:a='w'/>α😀&amp;\r\n";
    private static XmlParserCore Create() => new(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AppendChecked(XmlParserCore core, string chunk, CountingSink sink,
        CancellationToken cancellation, int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            var r = core.Append(chunk, [], false, sink, cancellation);
            if (r.Step != ScanStep.NeedInput || r.Consumed != chunk.Length) throw new Exception("Warm grammar rejected.");
        }
    }
    private static void Main(string[] args)
    {
        bool helper = args.Length == 1 && args[0] == "helper";
        if (args.Length != 1 || (!helper && args[0] != "original")) throw new ArgumentException("Choose original or helper.");
        long before = GC.GetAllocatedBytesForCurrentThread();
        long counter = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        using var cold = Create(); var coldSink = new CountingSink();
        long coldSetup = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var root = cold.Append("<r>", [], false, coldSink, default);
        long coldRoot = GC.GetAllocatedBytesForCurrentThread() - before;
        if (root.Step != ScanStep.NeedInput || root.Consumed != 3) throw new Exception("Root progress.");
        before = GC.GetAllocatedBytesForCurrentThread();
        var first = cold.Append(Chunk, [], false, coldSink, default);
        long firstChunk = GC.GetAllocatedBytesForCurrentThread() - before;
        if (first.Step != ScanStep.NeedInput || first.Consumed != Chunk.Length) throw new Exception("First progress.");
        before = GC.GetAllocatedBytesForCurrentThread();
        using var core = Create(); var sink = new CountingSink();
        core.Append("<r>", [], false, sink, default);
        long setup = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        if (helper) AppendChecked(core, Chunk, sink, default, 100);
        else for (int i = 0; i < 100; i++) core.Append(Chunk, [], false, sink, default);
        long warm = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        if (helper) AppendChecked(core, Chunk, sink, default, 10000);
        else for (int i = 0; i < 10000; i++)
        {
            var r = core.Append(Chunk, [], false, sink, default);
            if (r.Step != ScanStep.NeedInput || r.Consumed != Chunk.Length) throw new Exception("Warm grammar rejected.");
        }
        long measured = GC.GetAllocatedBytesForCurrentThread() - before;
        if (core.Append("</r>", [], true, sink, default).Step != ScanStep.Completed) throw new Exception("Finalization.");
        if (sink.Units != 10100L * 5) throw new Exception("Unicode/text accounting.");
        Console.WriteLine($"mode={args[0]}; runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; counter={counter}; coldSetup={coldSetup}; coldRoot={coldRoot}; firstChunk={firstChunk}; setup={setup}; warm100={warm}; measured10000={measured}; units={sink.Units}; calls={sink.Calls}; completed=true");
        if (helper && measured != 0) throw new Exception("Steady allocation is nonzero.");
    }
    private sealed class CountingSink : IXmlEventSink
    {
        internal long Units, Calls;
        internal bool Continue = true;
        internal Action? Hook = null;
        private bool Count() { Calls++; Hook?.Invoke(); return Continue; }
        public bool StartDocument(XmlLocation l) => Count();
        public bool Declaration(XmlDeclarationView d) => Count();
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) => Count();
        public bool NamespaceDeclaration(XmlNamespaceView n) => Count();
        public bool Attribute(XmlAttributeView a) => Count();
        public bool StartElementContent() => Count();
        public bool EndElement(XmlNameView n, XmlLocation l) => Count();
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l) => Count();
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) { Units += t.Length; return Count(); }
        public bool EndDocument(XmlLocation l) => Count();
    }
}

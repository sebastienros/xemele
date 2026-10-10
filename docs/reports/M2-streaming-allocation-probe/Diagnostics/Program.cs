using System.Text.Json;
using System.Runtime.CompilerServices;
using Xemele;
using Xemele.Parsing.Syntax;
using Xemele.Tests.Syntax;

internal static class Program
{
    private const string Chunk = "<c a='v' xmlns:p='u' p:a='w'/>α😀&amp;\r\n";
    private static XmlParserCore Create() => new(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
    private static void Require(bool condition) { if (!condition) throw new Exception("Streaming diagnostic failed."); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OriginalAppendChecked(XmlParserCore core, string chunk,
        CountingSink sink, CancellationToken cancellation, int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            var r = core.Append(chunk, [], false, sink, cancellation);
            if (r.Step != ScanStep.NeedInput || r.Consumed != chunk.Length || r.Diagnostic is not null)
                throw new Exception("Warm grammar rejected.");
        }
    }
    private static void Main(string[] args)
    {
        Require(args.Length == 1 && (args[0] == "original" || args[0] == "equivalent" || args[0] == "sampled"));
        bool sampled = args[0] == "sampled";
        bool equivalent = args[0] != "original";
        CancellationToken cancellation = default; // synchronous independent process, uncancelled like the test
        long before = GC.GetAllocatedBytesForCurrentThread();
        long counterOnly = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        using var cold = Create(); var coldSink = new CountingSink();
        long coldSetup = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var root = cold.Append("<r>", [], false, coldSink, cancellation);
        long coldRoot = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(root.Step == ScanStep.NeedInput && root.Consumed == 3 && root.Diagnostic is null);
        before = GC.GetAllocatedBytesForCurrentThread();
        var first = cold.Append(Chunk, [], false, coldSink, cancellation);
        long firstChunk = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(first.Step == ScanStep.NeedInput && first.Consumed == Chunk.Length && first.Diagnostic is null);
        before = GC.GetAllocatedBytesForCurrentThread();
        using var core = Create(); var sink = new CountingSink();
        root = core.Append("<r>", [], false, sink, cancellation);
        long setup = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(root.Step == ScanStep.NeedInput && root.Consumed == 3 && root.Diagnostic is null);
        long calls = sink.Calls, units = sink.Units;
        before = GC.GetAllocatedBytesForCurrentThread();
        StreamingAllocationSample[] warmSamples = sampled ? new StreamingAllocationSample[100] : [];
        StreamingAllocationSample[] measuredSamples = sampled ? new StreamingAllocationSample[10000] : [];
        Func<(long Units, long Calls)>? progress = sampled ? () => (sink.Units, sink.Calls) : null;
        long samplingSetup = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        long? warmPrimitive = null;
        if (equivalent) warmPrimitive = StreamingAllocationProbe.AppendChecked(core, Chunk, sink, cancellation, 100, warmSamples, progress);
        else OriginalAppendChecked(core, Chunk, sink, cancellation, 100);
        long warmSurrounding = GC.GetAllocatedBytesForCurrentThread() - before;
        long warmCalls = sink.Calls - calls, warmUnits = sink.Units - units;
        calls = sink.Calls; units = sink.Units;
        before = GC.GetAllocatedBytesForCurrentThread();
        long? primitive = null;
        if (equivalent) primitive = StreamingAllocationProbe.AppendChecked(core, Chunk, sink, cancellation, 10000, measuredSamples, progress);
        else OriginalAppendChecked(core, Chunk, sink, cancellation, 10000);
        long surrounding = GC.GetAllocatedBytesForCurrentThread() - before;
        long measured = equivalent ? primitive!.Value : surrounding;
        long measuredCalls = sink.Calls - calls, measuredUnits = sink.Units - units;
        before = GC.GetAllocatedBytesForCurrentThread();
        var completion = core.Append("</r>", [], true, sink, cancellation);
        long completionBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(warmCalls == 1199 && warmUnits == 500 && measuredUnits == 50000 && measuredCalls == 120000);
        Require(sink.Units == 50500 && completion.Step == ScanStep.Completed && completion.Consumed == 4 && completion.Diagnostic is null && sink.EndDocuments == 1);
        if (sampled)
        {
            string path = Environment.GetEnvironmentVariable("XEMELE_STREAMING_ALLOCATION_SAMPLE_OUTPUT")
                ?? throw new ArgumentException("Sample output path is required.");
            File.WriteAllText(path, JsonSerializer.Serialize(new { warmSamples, measuredSamples }));
        }
        Console.WriteLine(JsonSerializer.Serialize(new {
            mode = args[0], runtime = Environment.Version.ToString(), warmIterations = 100, measuredIterations = 10000,
            samplingSetup, counterOnly, coldSetup, coldRoot, firstChunk, setup, warmPrimitive, warmSurrounding,
            primitive, surrounding, measured, completionBytes, warmCalls, warmUnits, measuredCalls, measuredUnits,
            totalUnits = sink.Units, totalCalls = sink.Calls, endDocuments = sink.EndDocuments, completed = true,
            scope = "Synchronous checked borrowed Append; setup/root/first-use/caller/output reported separately; no timing or retained-memory estimate",
            runtimeSettings = "default JIT/GC; no overrides"
        }));
        Require(!equivalent || measured == 0);
    }
    private sealed class CountingSink : IXmlEventSink
    {
        internal long Units, Calls, EndDocuments;
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
        public bool EndDocument(XmlLocation l) { EndDocuments++; return Count(); }
    }
}

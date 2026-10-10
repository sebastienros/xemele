#if RECOVERY_DRIVER
global using EntityBaseBenchmarks = Xemele.Benchmarks.RecoveryBenchmarks;
#endif
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Recovery;
using Xemele.Parsing.Syntax;
namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class RecoveryBenchmarks
{
    private const string Strict = "<r xmlns:p='urn:p'><p:a id='1'>α😀&amp;</p:a></r>";
    private const string Repaired = "<r a='first' a='second'><a>α😀</r>";
    private static readonly XmlParseOptions Recovery = new() { Recover = true };
    [GlobalSetup] public void Setup() { EntityBaseProvenance.Gate("before"); Check(); }
    [GlobalCleanup] public void Cleanup() => EntityBaseProvenance.Gate("after");
    // Keep the verified driver's two case identifiers; these measure strict
    // owned diagnostic results and recovered owned results respectively.
    [Benchmark] public object InternalNestedModel() => XmlDocument.ParseWithDiagnostics(Strict);
    [Benchmark] public object RedirectedComposition() => XmlDocument.ParseWithDiagnostics(Repaired, Recovery);
    public void Check()
    {
        var strict = (XmlParseResult<XmlDocument>)InternalNestedModel();
        if (!strict.IsComplete || !strict.IsWellFormed || strict.Diagnostics.Count != 0 || strict.Value is null) throw new Exception("strict result oracle");
        var root = strict.Value.RootElement; var child = root.FirstChild;
        if (root.LocalName != "r" || root.NamespaceUri != "" || root.Attributes.Count != 0 || root.Children.Count != 1 || root.NamespaceDeclarations.Count != 1 || root.NamespaceDeclarations[0].Value != "urn:p" || child.LocalName != "a" || child.Prefix != "p" || child.NamespaceUri != "urn:p" || child.Attributes.Count != 1 || child.GetAttribute("id") != "1" || child.GetTextContent() != "α😀&" || child.Children.Count != 1) throw new Exception("complete strict tree oracle");
        var recovered = (XmlParseResult<XmlDocument>)RedirectedComposition();
        if (!recovered.IsComplete || recovered.IsWellFormed || recovered.Value is null || recovered.Diagnostics.Count != 2) throw new Exception("recovery result oracle");
        root = recovered.Value.RootElement; child = root.FirstChild;
        if (root.LocalName != "r" || root.Attributes.Count != 1 || root.GetAttribute("a") != "first" || root.Children.Count != 1 || child.LocalName != "a" || child.GetTextContent() != "α😀" || child.Children.Count != 1 || root.NamespaceDeclarations.Count != 0 || child.Attributes.Count != 0) throw new Exception("complete recovery tree oracle");
        if (recovered.Diagnostics[0] != new XmlDiagnostic("XML.Recovery.DuplicateAttribute", XmlDiagnosticSeverity.Error, "Retained the first expanded attribute name.", new(null, 1, 14, null)) || recovered.Diagnostics[1] != new XmlDiagnostic("XML.Recovery.AncestorClose", XmlDiagnosticSeverity.Error, "Closed an open element before an ancestor closing tag.", new(null, 1, 30, null))) throw new Exception("complete diagnostic oracle");
        Borrowed(false); // Functional counts and finalization; counters disabled.
        if (Environment.GetCommandLineArgs().Contains("--allocation")) Borrowed(true);
    }
    private static void Borrowed(bool measure)
    {
        const string chunk = "<c a='v' xmlns:p='u' p:a='w'/>α😀&amp;\r\n";
        long setupStart = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
        using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
        var sink = new BorrowedSink();
        if (core.Append("<r>", [], false, sink, default).Step != ScanStep.NeedInput) throw new Exception("strict borrowed root");
        for (int i = 0; i < 128; i++) Consume(core, sink, chunk);
        long setup = measure ? GC.GetAllocatedBytesForCurrentThread() - setupStart : 0;
        long before = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
        long calls = sink.Calls, units = sink.Units;
        for (int i = 0; i < 1024; i++) Consume(core, sink, chunk);
        long bytes = measure ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
        if (sink.Calls - calls != 1024 * 12 || sink.Units - units != 1024 * 5) throw new Exception("complete borrowed callback/count oracle");
        if (measure)
        {
            sink.Positive = true;
            before = GC.GetAllocatedBytesForCurrentThread();
            Consume(core, sink, chunk);
            long positive = GC.GetAllocatedBytesForCurrentThread() - before;
            if (bytes != 0 || positive == 0) throw new Exception("borrowed 0B and positive control");
            Console.WriteLine("RECOVERY_BORROWED " + JsonSerializer.Serialize(new { iterations = 1024, bytes, positive, setupBytes = setup, callsPerOperation = 12, utf16OutputUnits = 5, boundedSetup = "token128, frame/binding/name warm capacity; no source retention", ownedResultsExcluded = true }));
        }
        sink.Positive = false;
        if (core.Append("</r>", [], true, sink, default).Step != ScanStep.Completed) throw new Exception("borrowed balanced completion");
    }
    private static void Consume(XmlParserCore core, BorrowedSink sink, string chunk)
    { var r = core.Append(chunk, [], false, sink, default); if (r.Consumed != chunk.Length || r.Step != ScanStep.NeedInput || r.Diagnostic is not null) throw new Exception("strict borrowed outcome"); }
    private sealed class BorrowedSink : IXmlEventSink
    {
        internal long Units, Calls; internal bool Positive; private object? escaped;
        private bool Next() { Calls++; if (Positive) escaped = new byte[37]; return true; }
        public bool StartDocument(XmlLocation l) => Next();
        public bool Declaration(XmlDeclarationView d) => Next();
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) => Next();
        public bool NamespaceDeclaration(XmlNamespaceView n) => Next();
        public bool Attribute(XmlAttributeView a) => Next();
        public bool StartElementContent() => Next();
        public bool EndElement(XmlNameView n, XmlLocation l) => Next();
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l) => Next();
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) { Units += t.Length; return Next(); }
        public bool EndDocument(XmlLocation l) => Next();
    }
}

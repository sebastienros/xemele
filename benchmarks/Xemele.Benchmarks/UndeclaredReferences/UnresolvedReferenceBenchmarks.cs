#if UNRESOLVED_DRIVER
global using EntityBaseBenchmarks = Xemele.Benchmarks.UnresolvedReferenceBenchmarks;
#endif
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Syntax;
namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class UnresolvedReferenceBenchmarks
{
    private const string Prefix = "<!DOCTYPE r [<!ENTITY % p ''>%p;]>";
    private const string Chunk = "a&x;&y;🙂&𐀀;";
    private readonly string document = Prefix + "<r>" + Chunk + "b&" + new string('n', 200) + ";c</r>";
    private readonly XmlParseOptions options = new() { DtdProcessing = XmlDtdProcessing.Parse };
    private readonly OracleSink sink = new();
    [GlobalSetup] public void Setup() { EntityBaseProvenance.Gate("before"); Check(); }
    [GlobalCleanup] public void Cleanup() => EntityBaseProvenance.Gate("after");

    // Preserve the verified two-case driver's method names. This case executes
    // the actual parser and borrowed events, not an independently scripted model.
    [Benchmark] public object InternalNestedModel()
    {
        sink.Reset();
        using var core = new XmlParserCore(options);
        var result = core.Append(document, [], true, sink, default);
        if (result.Step != ScanStep.Completed || result.Consumed != document.Length || result.Diagnostic is not null ||
            sink.References != 4 || sink.Units != 5 || sink.Ended != 4) throw new Exception("Complete actual event oracle.");
        return sink;
    }
    [Benchmark] public object RedirectedComposition() => XmlDocument.Parse(document, options);
    public void Check()
    {
        InternalNestedModel();
        var dom = (XmlDocument)RedirectedComposition();
        if (dom.RootElement.LocalName != "r" || dom.UnresolvedReferences.Count != 4 ||
            dom.RootElement.FirstChild.Value != "a" || dom.RootElement.LastChild.Value != "c") throw new Exception("Owned DOM oracle.");
        string[] names = ["x", "y", "𐀀", new string('n', 200)];
        int[] boundaries = [1, 1, 2, 3];
        for (int i = 0; i < 4; i++)
        {
            var occurrence = dom.UnresolvedReferences[i];
            if (occurrence.Name != names[i] || occurrence.LexicalReference != "&" + names[i] + ";" ||
                occurrence.Sequence != i || occurrence.ChildBoundary != boundaries[i] || occurrence.Parent != dom.RootElement ||
                occurrence.Location.ByteOffset is not null) throw new Exception("Complete owned occurrence oracle.");
        }
        try { dom.ToXmlString(); throw new Exception("Loss refusal absent."); }
        catch (InvalidOperationException error) when (error.Message == "Document contains unresolved entity references.") { }
        if (Environment.GetCommandLineArgs().Contains("--allocation")) BorrowedAllocation();
    }
    private void BorrowedAllocation()
    {
        using var core = new XmlParserCore(options);
        var borrowed = new OracleSink();
        if (core.Append(Prefix + "<r>", [], false, borrowed, default).Step != ScanStep.NeedInput) throw new Exception("Borrowed initialization.");
        for (int i = 0; i < 128; i++) AppendBorrowed(core, borrowed);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) AppendBorrowed(core, borrowed);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        if (bytes != 0 || borrowed.References != 1152 * 3 || borrowed.Units != 1152 * 3 || borrowed.Ended != 1152 * 2)
            throw new Exception("0 B borrowed callback interval/counts.");
        if (core.Append("</r>", [], true, borrowed, default).Step != ScanStep.Completed) throw new Exception("Borrowed finalization.");
        Console.WriteLine("UNRESOLVED_BORROWED " + JsonSerializer.Serialize(new { iterations = 1024, utf16PerOperation = Chunk.Length, bytes, coldAndOwnedExcluded = true }));
    }
    private static void AppendBorrowed(XmlParserCore core, OracleSink sink)
    {
        var result = core.Append(Chunk, [], false, sink, default);
        if (result.Step != ScanStep.NeedInput || result.Consumed != Chunk.Length || result.Diagnostic is not null) throw new Exception("Borrowed append oracle.");
    }
    private sealed class OracleSink : IXmlEventSink, IXmlUnresolvedReferenceSink
    {
        internal int References, Units, Ended;
        internal void Reset() => References = Units = Ended = 0;
        public bool UnresolvedReference(XmlUnresolvedReferenceView reference)
        {
            if (reference.LexicalReference.Length != reference.Name.Length + 2 || reference.LexicalReference[0] != '&' ||
                reference.LexicalReference[^1] != ';' || !reference.LexicalReference[1..^1].SequenceEqual(reference.Name)) throw new Exception("Borrowed identity.");
            References++; return true;
        }
        public bool StartDocument(XmlLocation at) => true;
        public bool Declaration(XmlDeclarationView view) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation at) => name.LocalName.SequenceEqual("r");
        public bool NamespaceDeclaration(XmlNamespaceView view) => throw new Exception("Unexpected namespace.");
        public bool Attribute(XmlAttributeView view) => throw new Exception("Unexpected attribute.");
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation at) => name.LocalName.SequenceEqual("r");
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation at) => kind == XmlTextKind.Text && target.IsEmpty;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation at) { Units += text.Length; if (ended) Ended++; return true; }
        public bool EndDocument(XmlLocation at) => true;
    }
}

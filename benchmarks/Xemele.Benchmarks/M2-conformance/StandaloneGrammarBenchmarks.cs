#if GRAMMAR_DRIVER
global using EntityBaseBenchmarks = Xemele.Benchmarks.StandaloneGrammarBenchmarks;
#endif
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class StandaloneGrammarBenchmarks
{
    private const string Declaration = "<?xml version='1.0' standalone='yes'?>";
    private const string Chunk = "<![CDATA[ \t]]>&#32;&reference;&cdata;";
    private const string Text = " \t   ";
    private const string Subset = "<!ELEMENT r (child)*><!ELEMENT child (#PCDATA)>";
    private const string Internal = "<!ENTITY reference '&#38;#32;'><!ENTITY cdata '<![CDATA[ ]]>'>";
    private const string Primary = Declaration + "<!DOCTYPE r SYSTEM 'urn:grammar:subset' [" + Internal + "]><r>" + Chunk + "<child>🙂</child></r>";
    private readonly XmlEntityContext context = new(new(
        [new("r", new(DtdParticleKind.Sequence, null, DtdOccurrence.ZeroOrMore,
            [new(DtdParticleKind.Name, "child", DtdOccurrence.Once, [])]), true, default)], [],
        [new("reference", false, "&#32;", null, null, null, false, default),
         new("cdata", false, "<![CDATA[ ]]>", null, null, null, false, default)], [], []));
    private readonly Sink sink = new();
    private readonly Offline resolver = new();
    [GlobalSetup] public void Setup() { EntityBaseProvenance.Gate("before"); Check(); }
    [GlobalCleanup] public void Cleanup() => EntityBaseProvenance.Gate("after");

    // The verified driver's two case labels retain its preparation/result protocol.
    [Benchmark] public object InternalNestedModel()
    {
        sink.Reset();
        using var core = new XmlParserCore(new(), context);
        var result = core.Append(Declaration + "<r>" + Chunk + "</r>", [], true, sink, default);
        if (result.Step != ScanStep.Completed || result.Diagnostic is not null || sink.Units != 5 || sink.Stage != 4)
            throw new Exception("Strict Core authored whitespace and document oracle.");
        return sink;
    }
    [Benchmark] public object RedirectedComposition() => XmlDocument.Parse(Primary,
        new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
    public void Check()
    {
        InternalNestedModel();
        int opens = resolver.Opens, closes = resolver.Disposals;
        var document = (XmlDocument)RedirectedComposition(); var root = document.RootElement;
        if (root.LocalName != "r" || root.NamespaceUri != "" || root.Prefix != "" || root.Parent != document.DocumentNode ||
            document.DocumentNode.FirstChild != root || document.DocumentNode.LastChild != root ||
            root.GetTextContent() != Text + "🙂" || document.UnresolvedReferences.Count != 0)
            throw new Exception("Complete authored DOM root/content oracle.");
        foreach (var attribute in root.Attributes) throw new Exception("Unexpected attribute.");
        foreach (var ns in root.NamespaceDeclarations) throw new Exception("Unexpected namespace.");
        var first = root.FirstChild; var second = first.NextSibling; var third = second.NextSibling; var child = third.NextSibling;
        if (first.Kind != XmlNodeKind.CData || first.Value != " \t" || !first.PreviousSibling.IsDefault ||
            second.Kind != XmlNodeKind.Text || second.Value != "  " || second.PreviousSibling != first ||
            third.Kind != XmlNodeKind.CData || third.Value != " " || third.PreviousSibling != second ||
            child.Kind != XmlNodeKind.Element || child.LocalName != "child" || child.NamespaceUri != "" || child.Prefix != "" ||
            child.PreviousSibling != third || child.Parent != root || root.LastChild != child || !child.NextSibling.IsDefault ||
            child.FirstChild.Kind != XmlNodeKind.Text || child.FirstChild.Value != "🙂" || child.FirstChild.Parent != child ||
            child.FirstChild != child.LastChild || !child.FirstChild.NextSibling.IsDefault)
            throw new Exception("Complete authored DOM topology/lexical oracle.");
        foreach (var attribute in child.Attributes) throw new Exception("Unexpected child attribute.");
        foreach (var ns in child.NamespaceDeclarations) throw new Exception("Unexpected child namespace.");
        if (document.DocumentType is not { Name: "r", SystemId: "urn:grammar:subset", PublicId: null } type || type.InternalSubset != Internal ||
            document.Dtd is not { } model || model.Elements.Count != 2 || model.GeneralEntities.Count != 2 || model.Attributes.Count != 0 ||
            model.ParameterEntities.Count != 0 || model.Notations.Count != 0 || resolver.Opens != opens + 1 || resolver.Disposals != closes + 1)
            throw new Exception("Complete authored DTD/resource oracle.");
        RequireParticles(model.Elements[0].Content, model.Elements[1].Content);
        RejectWrongParticles(new(DtdParticleKind.Sequence, null, DtdOccurrence.Once, model.Elements[0].Content.Children), model.Elements[1].Content);
        RejectWrongParticles(new(DtdParticleKind.Sequence, null, DtdOccurrence.ZeroOrMore,
            [new(DtdParticleKind.Name, "other", DtdOccurrence.Once, [])]), model.Elements[1].Content);
        foreach (var entity in model.GeneralEntities)
            if (entity.IsParameter || entity.IsExternal || entity.SystemId is not null || entity.PublicId is not null || entity.NotationName is not null)
                throw new Exception("Complete authored internal parsed entity fields oracle.");
        if (model.Elements[0].Name != "r" || !model.Elements[0].IsExternal || model.Elements[0].Content.Kind != DtdParticleKind.Sequence ||
            model.Elements[1].Name != "child" || !model.Elements[1].IsExternal || model.Elements[1].Content.Kind != DtdParticleKind.Mixed ||
            model.GeneralEntities[0].Name != "reference" || model.GeneralEntities[0].ReplacementText != "&#32;" || model.GeneralEntities[0].IsExternal ||
            model.GeneralEntities[1].Name != "cdata" || model.GeneralEntities[1].ReplacementText != "<![CDATA[ ]]>" || model.GeneralEntities[1].IsExternal)
            throw new Exception("Authored declaration values/origin oracle.");
        if (Environment.GetCommandLineArgs().Contains("--allocation")) BorrowedAllocation();
    }
    private const string ParticleFailure = "Complete authored content particle oracle.";
    private static void RequireParticles(DtdParticle root, DtdParticle child)
    {
        if (root.Kind != DtdParticleKind.Sequence || root.Name is not null || root.Occurrence != DtdOccurrence.ZeroOrMore ||
            root.Children.Count != 1 || root.Children[0].Kind != DtdParticleKind.Name || root.Children[0].Name != "child" ||
            root.Children[0].Occurrence != DtdOccurrence.Once || root.Children[0].Children.Count != 0 ||
            child.Kind != DtdParticleKind.Mixed || child.Name is not null || child.Occurrence != DtdOccurrence.Once || child.Children.Count != 0)
            throw new InvalidOperationException(ParticleFailure);
    }
    private static void RejectWrongParticles(DtdParticle root, DtdParticle child)
    {
        try { RequireParticles(root, child); }
        catch (InvalidOperationException error) when (error.Message == ParticleFailure) { return; }
        throw new Exception("Wrong occurrence/child-name negative control escaped the authored oracle.");
    }
    private void BorrowedAllocation()
    {
        using var core = new XmlParserCore(new(), context); sink.Reset();
        if (core.Append(Declaration + "<r>", [], false, sink, default).Step != ScanStep.NeedInput) throw new Exception("Initialize borrowed Core.");
        for (int i = 0; i < 128; i++) Append(core);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) Append(core);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        if (allocated != 0 || sink.Units != 1152 * Text.Length) throw new Exception("Warm borrowed grammar allocation/content oracle.");
        if (core.Append("</r>", [], true, sink, default).Step != ScanStep.Completed || sink.Stage != 4) throw new Exception("Complete borrowed Core.");
        Console.WriteLine("GRAMMAR_BORROWED " + JsonSerializer.Serialize(new { iterations = 1024, utf16PerOperation = Chunk.Length, bytes = allocated, coldAndOwnedExcluded = true }));
    }
    private void Append(XmlParserCore core)
    {
        var result = core.Append(Chunk, [], false, sink, default);
        if (result.Step != ScanStep.NeedInput || result.Consumed != Chunk.Length || result.Diagnostic is not null)
            throw new Exception("Repeated borrowed grammar append oracle.");
    }
    private sealed class Sink : IXmlEventSink
    {
        internal int Units, Stage;
        internal void Reset() { Units = Stage = 0; }
        public bool StartDocument(XmlLocation at) { if (Stage != 0 || at != new XmlLocation(null, 1, 1, null)) throw new Exception("Start document."); Stage = 1; return true; }
        public bool Declaration(XmlDeclarationView d)
        { if (Stage != 1 || !d.Version.SequenceEqual("1.0") || d.Standalone != true || !d.EncodingName.IsEmpty) throw new Exception("Declaration."); return true; }
        public bool StartElement(XmlNameView name, bool empty, XmlLocation at)
        { if (Stage != 1 || empty || !name.LocalName.SequenceEqual("r") || !name.Prefix.IsEmpty || !name.NamespaceUri.IsEmpty) throw new Exception("Root."); Stage = 2; return true; }
        public bool NamespaceDeclaration(XmlNamespaceView d) => throw new Exception("Unexpected namespace.");
        public bool Attribute(XmlAttributeView a) => throw new Exception("Unexpected attribute.");
        public bool StartElementContent() { if (Stage != 2) throw new Exception("Content."); Stage = 3; return true; }
        public bool EndElement(XmlNameView name, XmlLocation at)
        { if (Stage != 3 || Units % Text.Length != 0 || !name.LocalName.SequenceEqual("r") || !name.Prefix.IsEmpty || !name.NamespaceUri.IsEmpty) throw new Exception("End root."); Stage = 4; return true; }
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation at)
        { if (Stage != 3 || !target.IsEmpty || kind is not (XmlTextKind.Text or XmlTextKind.CData)) throw new Exception("Text kind."); return true; }
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation at)
        { foreach (char c in text) { if (c != Text[Units % Text.Length]) throw new Exception("Exact borrowed whitespace."); Units++; } return true; }
        public bool EndDocument(XmlLocation at) { if (Stage != 4) throw new Exception("End document."); return true; }
    }
    private sealed class Offline : XmlResourceResolver
    {
        internal int Opens, Disposals;
        // Immutable input/identity fixture construction belongs to setup. Each
        // timed acquisition owns only its read-only wrapper and resource handle.
        private readonly byte[] bytes = Encoding.UTF8.GetBytes(Subset);
        private readonly Uri expected = new("urn:grammar:subset"), final = new("https://offline.invalid/grammar.dtd");
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Uri != expected || request.SystemId != "urn:grammar:subset" || request.Kind != XmlResourceKind.ExternalSubset)
                throw new Exception("Explicit resource grant.");
            Opens++; return new(new Tracked(bytes, this), final);
        }
        private sealed class Tracked(byte[] bytes, Offline owner) : MemoryStream(bytes, false)
        { protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); } }
    }
}

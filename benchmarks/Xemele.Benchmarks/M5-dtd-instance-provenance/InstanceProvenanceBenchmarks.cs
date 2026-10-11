using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;
using Xemele.Validation;

namespace Xemele.Benchmarks.M5DtdInstanceProvenance;

[MemoryDiagnoser, ShortRunJob]
public class InstanceProvenanceBenchmarks
{
    [Params("no-dtd", "literal", "charref", "entity-literal", "entity-reparsed", "empty", "missing")]
    public string Scenario { get; set; } = "literal";
    [Params(8, 128)] public int Count { get; set; }
    private string source = "";
    private XmlParseOptions options = null!;
    [GlobalSetup] public void Setup()
    {
        source = Fixture(Scenario, Count); options = new() { DtdProcessing = XmlDtdProcessing.Parse };
        var d = XmlDocument.Parse(source, options);
        if (Scenario != "no-dtd")
        {
            var result = DtdSchema.FromDocument(d).Validate(d);
            bool valid = Scenario is "literal" or "entity-literal";
            if (!result.IsComplete || result.IsValid != valid) throw new InvalidOperationException("Composition control failed.");
        }
    }
    internal static string Fixture(string scenario, int count)
    {
        string prefix = scenario == "no-dtd" ? "" : scenario == "empty" ? "<!DOCTYPE r [<!ELEMENT r EMPTY><!ENTITY e ''>]>" : scenario == "missing" ? "<!DOCTYPE r [<!ENTITY % p '<!ELEMENT r ANY>'>%p;]>" :
            "<!DOCTYPE r [<!ELEMENT r (r*)><!ENTITY s '&#32;'><!ENTITY e '&#38;#32;'>]>";
        string unit = scenario switch { "charref" => "&#32;", "entity-literal" => "&s;", "entity-reparsed" or "empty" => "&e;", "missing" => "&absent;", _ => " " };
        return prefix + "<r>" + string.Concat(Enumerable.Repeat(unit, count)) + "</r>";
    }
    [Benchmark] public XmlDocument ParseOwnedDom() => XmlDocument.Parse(source, options);
}

[MemoryDiagnoser, ShortRunJob]
public class StreamingProvenanceBenchmarks
{
    [Params(false, true)] public bool OptionalProof { get; set; }
    private XmlParserCore core = null!;
    private CountSink sink = null!;
    [GlobalSetup] public void Setup()
    {
        core = new(new() { DtdProcessing = XmlDtdProcessing.Parse, Limits = new() { DocumentCharacters = null, TokenCharacters = null, ExpandedCharacters = null, EvaluationWork = null } });
        sink = OptionalProof ? new ProofSink() : new CountSink();
        Append("<!DOCTYPE r [<!ELEMENT r (r*)>]><r>");
        for (int i = 0; i < 1000; i++) Append("&#32;");
    }
    private void Append(string text)
    { var result = core.Append(text, [], false, sink, default); if (result.Step != ScanStep.NeedInput) throw new InvalidOperationException(); }
    [Benchmark] public long BorrowedScalarCallbacks() { Append("&#32;"); return sink.Scalars; }
    [GlobalCleanup] public void Cleanup() => core.Dispose();
}

internal class CountSink : IXmlEventSink
{
    internal long Scalars, Names;
    public bool StartDocument(XmlLocation location) => true;
    public bool Declaration(XmlDeclarationView declaration) => true;
    public bool StartElement(XmlNameView name, bool isEmptyElement, XmlLocation location) { Names += name.LocalName.Length + name.Prefix.Length; return true; }
    public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
    public bool Attribute(XmlAttributeView attribute) => true;
    public bool StartElementContent() => true;
    public bool EndElement(XmlNameView name, XmlLocation location) => true;
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> piTarget, XmlLocation location) => true;
    public bool TextFragment(ReadOnlySpan<char> fragment, bool tokenEnded, XmlLocation location) { Scalars += fragment.Length; return true; }
    public bool EndDocument(XmlLocation location) => true;
}
internal sealed class ProofSink : CountSink, IXmlValidationProvenanceSink
{
    internal XmlDiagnostic? Diagnostic;
    public void ValidationProvenance(XmlDiagnostic diagnostic, DtdElementDeclaration declaration) => Diagnostic = diagnostic;
}

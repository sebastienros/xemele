using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class EntityCompositionBenchmarks
{
    [Params(16, 4096)] public int Length { get; set; }
    private string internalInput = "", externalInput = "", expected = "";
    private readonly XmlParseOptions internalOptions = new() { DtdProcessing = XmlDtdProcessing.Parse };
    private XmlParseOptions externalOptions = new() { DtdProcessing = XmlDtdProcessing.Parse };

    [GlobalSetup]
    public void Setup()
    {
        expected = new string('a', Length) + "😀";
        const string attributes = "<!ATTLIST r xmlns CDATA 'urn:r' a NMTOKENS ' owned  tail ' id ID #IMPLIED>";
        internalInput = "<!DOCTYPE r [<!ENTITY e '" + expected + "'>" + attributes + "]><r id='one'><b>&e;</b></r>";
        externalInput = "<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:benchmark'>" + attributes + "]><r id='one'><b>&e;</b></r>";
        externalOptions = internalOptions with { Resolver = new OfflineResolver(Encoding.UTF8.GetBytes(expected)) };
    }

    [Benchmark] public XmlDocument InternalDocument() => XmlDocument.Parse(internalInput, internalOptions);
    [Benchmark] public XmlDocument ExternalDocument() => XmlDocument.Parse(externalInput, externalOptions);

    public void FullOutputOracle()
    {
        Validate(InternalDocument(), false); Validate(ExternalDocument(), true);
    }
    private void Validate(XmlDocument document, bool external)
    {
        var root = document.RootElement;
        if (document.DocumentType?.Name != "r" || document.DocumentType.InternalSubset is null || root.LocalName != "r" ||
            root.NamespaceUri != "urn:r" || root.GetAttribute("a") != "owned tail" || root.GetAttribute("id") != "one" ||
            root.Attributes.Count != 2 || root.NamespaceDeclarations.Count != 1 || root.Children.Count != 1 ||
            root.FirstChild.Kind != XmlNodeKind.Element || root.FirstChild.LocalName != "b" || root.FirstChild.NamespaceUri != "urn:r" ||
            root.FirstChild.GetTextContent() != expected || root.GetTextContent() != expected || document.Dtd?.GeneralEntities.Count != 1)
            throw new InvalidOperationException("Actual composition benchmark full output oracle failed.");
        var entity = document.Dtd.GeneralEntities[0];
        if (entity.Name != "e" || (external ? entity.SystemId != "urn:benchmark" || entity.ReplacementText is not null : entity.ReplacementText != expected))
            throw new InvalidOperationException("Actual immutable entity model oracle failed.");
    }
    private sealed class OfflineResolver(byte[] body) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return new(new MemoryStream(body, false), new("https://final.example/benchmark")); }
    }
}

[MemoryDiagnoser, ShortRunJob]
public class EntityLookupBenchmarks
{
    private XmlEntityContext context = null!;
    [GlobalSetup]
    public void Setup() => context = new(new([], [new("r", "a", DtdAttributeType.CData, [], DtdDefaultKind.Value, "v", false, default)],
        [new DtdEntityDeclaration("e", false, "owned", null, null, null, false, default)], [], []));
    [Benchmark] public object? GeneralLookup()
    { context.TryGetGeneralEntity("e", out var declaration); return declaration; }
    [Benchmark] public object DefaultLookup() => context.GetAttributes("r");
    public void FullOutputOracle()
    {
        var declaration = GeneralLookup() as DtdEntityDeclaration; var attributes = (IReadOnlyList<DtdAttributeDeclaration>)DefaultLookup();
        if (declaration?.Name != "e" || declaration.ReplacementText != "owned" || attributes.Count != 1 ||
            attributes[0].ElementName != "r" || attributes[0].Name != "a" || attributes[0].DefaultValue != "v")
            throw new InvalidOperationException("Actual lookup benchmark output oracle failed.");
    }
}

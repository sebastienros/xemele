using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class EntityCompositionBenchmarks
{
    [Params(16, 4096)] public int Length { get; set; }
    private string internalInput = "", externalInput = "", expected = "", internalSubset = "", externalSubset = "";
    private const string Attributes = "<!ATTLIST r xmlns CDATA 'urn:r' a NMTOKENS ' owned  tail ' id ID #IMPLIED>";
    private readonly XmlParseOptions internalOptions = new() { DtdProcessing = XmlDtdProcessing.Parse };
    private XmlParseOptions externalOptions = new() { DtdProcessing = XmlDtdProcessing.Parse };

    [GlobalSetup]
    public void Setup()
    {
        expected = new string('a', Length) + "😀";
        internalSubset = "<!ENTITY e '" + expected + "'>" + Attributes;
        externalSubset = "<!ENTITY e SYSTEM 'urn:benchmark'>" + Attributes;
        internalInput = "<!DOCTYPE r [" + internalSubset + "]><r id='one'><b>&e;</b></r>";
        externalInput = "<!DOCTYPE r [" + externalSubset + "]><r id='one'><b>&e;</b></r>";
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
        string subset = external ? externalSubset : internalSubset;
        var root = document.RootElement;
        var type = document.DocumentType;
        var model = document.Dtd;
        if (type is null || type.Name != "r" || type.PublicId is not null || type.SystemId is not null || type.InternalSubset != subset ||
            root.Kind != XmlNodeKind.Element || root.LocalName != "r" || root.Prefix != "" || root.NamespaceUri != "urn:r" ||
            root.Attributes.Count != 2 || root.NamespaceDeclarations.Count != 1 || root.Children.Count != 1 ||
            root.NamespaceDeclarations[0].Prefix != "" || root.NamespaceDeclarations[0].Value != "urn:r" ||
            root.Attributes[0].LocalName != "id" || root.Attributes[0].Prefix != "" || root.Attributes[0].NamespaceUri != "" || root.Attributes[0].Value != "one" ||
            root.Attributes[1].LocalName != "a" || root.Attributes[1].Prefix != "" || root.Attributes[1].NamespaceUri != "" || root.Attributes[1].Value != "owned tail" ||
            root.FirstChild.Kind != XmlNodeKind.Element || root.FirstChild.LocalName != "b" || root.FirstChild.Prefix != "" || root.FirstChild.NamespaceUri != "urn:r" ||
            root.FirstChild.Attributes.Count != 0 || root.FirstChild.NamespaceDeclarations.Count != 0 || root.FirstChild.Children.Count != 1 ||
            root.FirstChild.FirstChild.Kind != XmlNodeKind.Text || root.FirstChild.FirstChild.GetTextContent() != expected ||
            root.FirstChild.FirstChild.Children.Count != 0 || root.FirstChild.GetTextContent() != expected || root.GetTextContent() != expected ||
            model is null || model.Elements.Count != 0 || model.Attributes.Count != 3 || model.ParameterEntities.Count != 0 || model.Notations.Count != 0 || model.GeneralEntities.Count != 1)
            throw new InvalidOperationException("Actual composition benchmark full DOM/DTD inventory oracle failed.");
        var entity = model.GeneralEntities[0];
        if (entity.Name != "e" || entity.IsParameter || entity.IsExternal || entity.PublicId is not null || entity.NotationName is not null ||
            entity.Location != new XmlLocation(null, 1, 14, null) ||
            (external ? entity.SystemId != "urn:benchmark" || entity.ReplacementText is not null : entity.SystemId is not null || entity.ReplacementText != expected))
            throw new InvalidOperationException("Actual immutable entity declaration oracle failed.");
        // Independently locate authored attribute names in the known fixture;
        // scalar columns account for the supplementary character in the entity.
        CheckAttribute(model.Attributes[0], "xmlns", DtdAttributeType.CData, DtdDefaultKind.Value, "urn:r", Column("xmlns"));
        CheckAttribute(model.Attributes[1], "a", DtdAttributeType.NmTokens, DtdDefaultKind.Value, " owned  tail ", Column("a NMTOKENS"));
        CheckAttribute(model.Attributes[2], "id", DtdAttributeType.Id, DtdDefaultKind.Implied, null, Column("id ID"));
        int Column(string name) => 14 + subset[..subset.IndexOf(name, StringComparison.Ordinal)].EnumerateRunes().Count();
    }
    private static void CheckAttribute(DtdAttributeDeclaration declaration, string name, DtdAttributeType type, DtdDefaultKind kind, string? value, int column)
    {
        if (declaration.ElementName != "r" || declaration.Name != name || declaration.Type != type || declaration.DefaultKind != kind ||
            declaration.DefaultValue != value || declaration.Tokens.Count != 0 || declaration.IsExternal || declaration.Location != new XmlLocation(null, 1, column, null))
            throw new InvalidOperationException("Actual immutable attribute declaration oracle failed.");
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
        var declaration = GeneralLookup() as DtdEntityDeclaration;
        var attributes = (IReadOnlyList<DtdAttributeDeclaration>)DefaultLookup();
        var model = context.Model;
        if (declaration?.Name != "e" || declaration.IsParameter || declaration.ReplacementText != "owned" || declaration.SystemId is not null ||
            declaration.PublicId is not null || declaration.NotationName is not null || declaration.IsExternal || declaration.Location != default ||
            attributes.Count != 1 || attributes[0].ElementName != "r" || attributes[0].Name != "a" || attributes[0].Type != DtdAttributeType.CData ||
            attributes[0].Tokens.Count != 0 || attributes[0].DefaultKind != DtdDefaultKind.Value || attributes[0].DefaultValue != "v" ||
            attributes[0].IsExternal || attributes[0].Location != default || model.Elements.Count != 0 || model.Attributes.Count != 1 ||
            model.GeneralEntities.Count != 1 || model.ParameterEntities.Count != 0 || model.Notations.Count != 0 ||
            !ReferenceEquals(declaration, model.GeneralEntities[0]) || !ReferenceEquals(declaration, GeneralLookup()) ||
            !ReferenceEquals(attributes[0], model.Attributes[0]) || !ReferenceEquals(attributes, DefaultLookup()) ||
            attributes is not System.Collections.ObjectModel.ReadOnlyCollection<DtdAttributeDeclaration> ||
            model.GeneralEntities is not System.Collections.ObjectModel.ReadOnlyCollection<DtdEntityDeclaration>)
            throw new InvalidOperationException("Actual lookup immutable identity/full metadata oracle failed.");
    }
}

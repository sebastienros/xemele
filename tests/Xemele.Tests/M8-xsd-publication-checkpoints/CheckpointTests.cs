using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdPublicationCheckpoints;

public sealed class CheckpointTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static readonly Uri Main = new("https://example.test/main.xsd");
    internal static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    internal static (XsdCompiledSchema Graph, SchemaCompileContext Context) Compile(string body, SchemaCompileOptions? options = null)
    {
        var context = new SchemaCompileContext(options ?? new() { BaseUri = Main });
        var graph = new XmlSchemaCompiler().Compile(context.Parse(Wrap(body)).Document, context);
        return (graph with { Datatypes = new XmlDatatypeRegistry(graph.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), context.Operation) }, context);
    }
    private static XsdTypeDefinition Carrier(XsdCompiledSchema graph, string name) => Assert.Single(graph.Types, t => t.Name.LocalName == name);

    [Theory]
    [InlineData("1", true)] [InlineData("01", true)] [InlineData("2", false)]
    public void UnusedFixedUseRetainsBothTypedConstraints(string fixedUse, bool equal)
    {
        var (graph, context) = Compile($"<xs:attribute name='a' type='xs:int' fixed='1'/><xs:attributeGroup name='g'><xs:attribute ref='a' fixed='{fixedUse}'/></xs:attributeGroup>");
        var carrier = Carrier(graph, "#attribute-group:g"); var use = Assert.Single(carrier.Attributes);
        var declaration = graph.Attributes[use.DeclarationId];
        Assert.True(carrier.Anonymous); Assert.Equal("1", declaration.FixedValue); Assert.Equal(fixedUse, use.FixedValue);
        Assert.Equal(Main, use.Location.SourceUri); Assert.True(use.Location.LinePosition > declaration.Location.LinePosition);
        var facets = new XmlFacetEvaluator(); var type = graph.Datatypes.FindType(declaration.Type)!;
        Assert.True(facets.TryValidate(type, declaration.FixedValue, declaration.Context, graph.Datatypes, context.Operation, out var first, out _));
        Assert.True(facets.TryValidate(type, use.FixedValue, use.Context, graph.Datatypes, context.Operation, out var second, out _));
        Assert.Equal(equal, graph.Datatypes.Find(Xs)!.ValueEquals(first!, second!, context.Operation));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnusedIdUsesRemainVisibleTogether(bool two)
    {
        var (graph, _) = Compile("<xs:attributeGroup name='g'><xs:attribute name='a' type='xs:ID'/>" + (two ? "<xs:attribute name='b' type='xs:ID'/>" : "") + "</xs:attributeGroup>");
        var carrier = Carrier(graph, "#attribute-group:g");
        Assert.Equal(two ? 2 : 1, carrier.Attributes.Count);
        Assert.All(carrier.Attributes, use => Assert.Equal(new SchemaName(Xs, "ID"), graph.Attributes[use.DeclarationId].Type));
        Assert.Equal(carrier.Attributes.Count, carrier.Attributes.Select(u => u.DeclarationId).Distinct().Count());
    }

    [Theory]
    [InlineData("int", true)] [InlineData("string", false)]
    public void UnusedModelGroupReachesRealContentConsistency(string secondType, bool legal)
    {
        var (graph, context) = Compile($"<xs:group name='g'><xs:sequence><xs:element name='x' type='xs:int'/><xs:element name='x' type='xs:{secondType}'/></xs:sequence></xs:group>");
        var carrier = Carrier(graph, "#model-group:g");
        Assert.True(carrier.Anonymous); Assert.Equal(Main, carrier.Location.SourceUri);
        var group = Assert.IsType<XsdGroupTerm>(carrier.Particle!.Term); Assert.Equal(2, group.Particles.Count);
        var ids = group.Particles.Select(p => Assert.IsType<XsdElementTerm>(p.Term).DeclarationId).ToArray();
        Assert.NotEqual(ids[0], ids[1]); Assert.All(ids, id => Assert.Equal("x", graph.Elements[id].Name.LocalName));
        if (legal) Assert.NotNull(new XsdContentCompiler().Compile(graph, carrier, context.Operation));
        else Assert.Contains(Assert.Throws<SchemaCompileException>(() => new XsdContentCompiler().Compile(graph, carrier, context.Operation)).Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }

    [Theory]
    [InlineData("urn:same", true)] [InlineData("urn:other", false)]
    public void UnusedQNameUseKeepsIndependentNamespaceContexts(string useNamespace, bool equal)
    {
        var (graph, context) = Compile($"<xs:attribute name='a' type='xs:QName' fixed='p:v' xmlns:p='urn:same'/><xs:attributeGroup name='g'><xs:attribute ref='a' fixed='q:v' xmlns:q='{useNamespace}'/></xs:attributeGroup>");
        var use = Assert.Single(Carrier(graph, "#attribute-group:g").Attributes); var declaration = graph.Attributes[use.DeclarationId];
        Assert.Equal("urn:same", declaration.Context.Prefixes["p"]); Assert.Equal(useNamespace, use.Context.Prefixes["q"]);
        var facets = new XmlFacetEvaluator(); var type = graph.Datatypes.FindType(declaration.Type)!;
        Assert.True(facets.TryValidate(type, declaration.FixedValue, declaration.Context, graph.Datatypes, context.Operation, out var first, out _));
        Assert.True(facets.TryValidate(type, use.FixedValue, use.Context, graph.Datatypes, context.Operation, out var second, out _));
        Assert.Equal(equal, graph.Datatypes.Find(Xs)!.ValueEquals(first!, second!, context.Operation));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DisplacedInconsistentOriginalStillReachesContentChecks(bool selfReference)
    {
        var child = Wrap("<xs:group name='g'><xs:sequence><xs:element name='x' type='xs:int'/><xs:element name='x' type='xs:string'/></xs:sequence></xs:group>");
        var body = selfReference ? "<xs:group ref='g'/>" : "<xs:element name='x' type='xs:int'/>";
        var (graph, context) = Compile($"<xs:redefine schemaLocation='child.xsd'><xs:group name='g'><xs:sequence>{body}</xs:sequence></xs:group></xs:redefine>", new() { BaseUri = Main, Resolver = new ParsingTests.Resolver(_ => new(new ParsingTests.CountStream(child), new Uri("urn:original"))) });
        var original = Assert.Single(graph.Types, t => t.Name.LocalName.StartsWith("#model-group:#redefined", StringComparison.Ordinal));
        Assert.Equal(new Uri("urn:original"), original.Location.SourceUri);
        Assert.Throws<SchemaCompileException>(() => new XsdContentCompiler().Compile(graph, original, context.Operation));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RepeatedReferencesKeepOneCheckpointAndOriginalLocalIds(bool attributes)
    {
        var body = attributes
            ? "<xs:attributeGroup name='g'><xs:attribute name='a'/></xs:attributeGroup><xs:complexType name='A'><xs:attributeGroup ref='g'/></xs:complexType><xs:complexType name='B'><xs:attributeGroup ref='g'/></xs:complexType>"
            : "<xs:group name='g'><xs:sequence><xs:element name='a'/></xs:sequence></xs:group><xs:complexType name='A'><xs:group ref='g'/></xs:complexType><xs:complexType name='B'><xs:group ref='g'/></xs:complexType>";
        var (graph, context) = Compile(body); var carrier = Carrier(graph, attributes ? "#attribute-group:g" : "#model-group:g");
        var a = graph.Types.Single(t => t.Name.LocalName == "A"); var b = graph.Types.Single(t => t.Name.LocalName == "B");
        if (attributes)
        {
            Assert.Equal(Assert.Single(carrier.Attributes).DeclarationId, Assert.Single(a.Attributes).DeclarationId);
            Assert.Equal(Assert.Single(a.Attributes).DeclarationId, Assert.Single(b.Attributes).DeclarationId);
            Assert.Single(graph.Attributes);
        }
        else Assert.Single(graph.Elements);
        Assert.NotNull(new XsdContentCompiler().Compile(graph, carrier, context.Operation));
        Assert.StartsWith("#", carrier.Name.LocalName); // Cannot be a lexical NCName/xsi:type.
        Assert.False(graph.GlobalElements.ContainsKey(carrier.Name));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DisplacedGroupsKeepOriginalSourceContextsAndOneCarrierPerVersion(bool selfReference)
    {
        var child = Wrap("<xs:group name='g'><xs:sequence><xs:element name='old'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute name='q' type='xs:QName' fixed='p:v' xmlns:p='urn:old'/></xs:attributeGroup>");
        var replacement = selfReference
            ? "<xs:group name='g'><xs:sequence><xs:group ref='g'/><xs:element name='new'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attributeGroup ref='ag'/><xs:attribute name='new'/></xs:attributeGroup>"
            : "<xs:group name='g'><xs:sequence><xs:element name='old'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute name='q' type='xs:QName' fixed='p:v' xmlns:p='urn:old'/></xs:attributeGroup>";
        var (graph, context) = Compile($"<xs:redefine schemaLocation='child.xsd'>{replacement}</xs:redefine>", new() { BaseUri = Main, Resolver = new ParsingTests.Resolver(_ => new(new ParsingTests.CountStream(child), new Uri("https://example.test/child.xsd"))) });
        var carriers = graph.Types.Where(t => t.Name.LocalName.StartsWith("#model-group:", StringComparison.Ordinal) || t.Name.LocalName.StartsWith("#attribute-group:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, carriers.Length); Assert.All(carriers, t => Assert.True(t.Anonymous));
        var old = Assert.Single(carriers, t => t.Name.LocalName.StartsWith("#attribute-group:#redefined", StringComparison.Ordinal));
        var use = Assert.Single(old.Attributes); Assert.Equal("urn:old", use.Context.Prefixes["p"]);
        Assert.Equal(new Uri("https://example.test/child.xsd"), use.Location.SourceUri);
        Assert.Equal(new Uri("https://example.test/child.xsd"), old.Location.SourceUri);
        var current = Carrier(graph, "#attribute-group:ag");
        Assert.Equal(selfReference ? new SchemaName(Xs, "anyType") : old.Name, current.BaseType);
        Assert.NotNull(new XsdContentCompiler().Compile(graph, Carrier(graph, "#model-group:g"), context.Operation));
    }
}

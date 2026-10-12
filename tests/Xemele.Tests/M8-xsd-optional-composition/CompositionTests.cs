using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdOptionalComposition.NeighborTests;
namespace Xemele.Tests.M8XsdOptionalComposition;

public sealed class CompositionTests
{
    private static Explicit Missing() => new((_, _) => XmlResourceResolution.Unavailable);
    [Theory]
    [InlineData("<xs:include schemaLocation='missing'/>")]
    [InlineData("<xs:import namespace='urn:b' schemaLocation='missing'/>")]
    [InlineData("<xs:import schemaLocation='missing'/>", "targetNamespace='urn:a'")]
    [InlineData("<xs:redefine schemaLocation='missing'/>")]
    [InlineData("<xs:redefine schemaLocation='missing'><xs:annotation><xs:documentation>text</xs:documentation></xs:annotation></xs:redefine>")]
    public void ExplicitAbsenceLeavesOrdinaryDefinitionsUsable(string directive, string attributes = "")
    {
        var resolver = Missing(); var schema = XmlSchema.Compile(Wrap(directive + "<xs:element name='root' type='xs:int'/>", attributes), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate(attributes.Length == 0 ? "<root>7</root>" : "<root xmlns='urn:a'>7</root>").IsValid);
        Assert.Equal(1, resolver.Calls);
    }
    [Theory]
    [InlineData("<xs:simpleType name='T'><xs:restriction base='xs:string'/></xs:simpleType>")]
    [InlineData("<xs:complexType name='T'><xs:complexContent><xs:extension base='T'/></xs:complexContent></xs:complexType>")]
    [InlineData("<xs:group name='g'><xs:sequence><xs:group ref='g'/></xs:sequence></xs:group>")]
    [InlineData("<xs:attributeGroup name='g'><xs:attributeGroup ref='g'/></xs:attributeGroup>")]
    public void SubstantiveRedefineRequiresResolution(string child)
    {
        var resolver = Missing(); var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:redefine schemaLocation='missing'>{child}</xs:redefine>"), new() { BaseUri = Basis, Resolver = resolver }));
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Single(error.Diagnostics).Code); Assert.Equal(1, resolver.Calls);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingThenAvailableSameUriIsNotCachedAndImportPermissionSurvives(bool import)
    {
        var stream = new OwnedStream(Wrap("<xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>", import ? "targetNamespace='urn:b'" : ""));
        int calls = 0;
        var resolver = new Explicit((request, _) => { Assert.Equal(new Uri(Basis, "same.xsd"), request.Uri); return ++calls == 1 ? XmlResourceResolution.Unavailable : XmlResourceResolution.FromResource(new(stream, request.Uri)); });
        string directive = import ? "<xs:import namespace='urn:b' schemaLocation='same.xsd'/>" : "<xs:include schemaLocation='same.xsd'/>";
        var schema = XmlSchema.Compile(Wrap(directive + directive + $"<xs:element name='root' type='{(import ? "b:T" : "T")}'/>", import ? "xmlns:b='urn:b'" : ""), new() { BaseUri = Basis, Resolver = resolver, Limits = new() { ExternalResources = 1 } });
        Assert.True(schema.Validate("<root>7</root>").IsValid); Assert.False(schema.Validate("<root>bad</root>").IsValid); Assert.Equal(2, calls); Assert.Equal(1, stream.Disposals);
    }
    [Theory]
    [InlineData("<xs:include schemaLocation='missing'/><xs:element name='root' type='missing'/>")]
    [InlineData("<xs:import namespace='urn:b' schemaLocation='missing'/><xs:element name='root' type='b:missing'/>")]
    [InlineData("<xs:include schemaLocation='missing'/><xs:element name='root'><xs:complexType><xs:group ref='g'/></xs:complexType></xs:element>")]
    [InlineData("<xs:include schemaLocation='missing'/><xs:complexType name='T'><xs:attributeGroup ref='g'/></xs:complexType>")]
    public void MissingRequiredReferencesStillFail(string body)
    {
        var resolver = Missing(); var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(body, "xmlns:b='urn:b'"), new() { BaseUri = Basis, Resolver = resolver }));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction"); Assert.DoesNotContain(error.Diagnostics, d => d.Code == "SCHEMA.ResourceDenied"); Assert.Equal(1, resolver.Calls);
    }
    private sealed class Legacy : XmlResourceResolver
    {
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) { Calls++; return null; }
    }
    [Theory]
    [InlineData("include", 0)] [InlineData("include", 1)] [InlineData("include", 2)] [InlineData("include", 3)]
    [InlineData("import", 0)] [InlineData("import", 1)] [InlineData("import", 2)] [InlineData("import", 3)]
    [InlineData("redefine", 0)] [InlineData("redefine", 1)] [InlineData("redefine", 2)] [InlineData("redefine", 3)]
    public void EveryLegacyAndExplicitDenialRemainsFatal(string directive, int mode)
    {
        var legacy = new Legacy(); var explicitResolver = new Explicit((_, _) => mode == 2 ? default : XmlResourceResolution.Denied);
        XmlResourceResolver? resolver = mode == 0 ? null : mode == 1 ? legacy : explicitResolver;
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:{directive} schemaLocation='child'/>", directive == "import" ? "targetNamespace='urn:a'" : ""), new() { BaseUri = Basis, Resolver = resolver }));
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Single(error.Diagnostics).Code); Assert.Equal(mode == 1 ? 1 : 0, legacy.Calls); Assert.Equal(mode > 1 ? 1 : 0, explicitResolver.Calls);
    }
    [Theory]
    [InlineData("<xs:include/>")]
    [InlineData("<xs:import namespace=''/>")]
    [InlineData("<xs:redefine schemaLocation='missing'><xs:element name='e'/></xs:redefine>")]
    [InlineData("<xs:include schemaLocation='missing'><xs:simpleType name='T'/></xs:include>")]
    [InlineData("<xs:redefine schemaLocation='missing'>text</xs:redefine>")]
    public void InvalidDirectiveSyntaxFailsBeforeAcquisition(string directive)
    {
        var resolver = Missing(); Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(directive), new() { BaseUri = Basis, Resolver = resolver })); Assert.Equal(0, resolver.Calls);
    }
    [Theory]
    [InlineData("include", "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>", "SCHEMA.Syntax")]
    [InlineData("include", "<r/>", "XSD.SchemaRestriction")]
    [InlineData("include", "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='wrong'/>", "XSD.SchemaRestriction")]
    [InlineData("import", "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='wrong'/>", "XSD.SchemaRestriction")]
    [InlineData("redefine", "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='wrong'/>", "XSD.SchemaRestriction")]
    public void ResolvedMalformedWrongKindAndNamespaceAreNotAbsence(string directive, string content, string code)
    {
        var stream = new OwnedStream(content); var resolver = new Explicit((r, _) => XmlResourceResolution.FromResource(new(stream, r.Uri)));
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:{directive} schemaLocation='child' {(directive == "import" ? "namespace='urn:b'" : "")}/>"), new() { BaseUri = Basis, Resolver = resolver }));
        Assert.Contains(error.Diagnostics, d => d.Code == code); Assert.Equal(1, stream.Disposals);
    }
    [Theory]
    [InlineData("include")] [InlineData("redefine")]
    public void ActiveCompositionCyclesStillRefuse(string directive)
    {
        var stream = new OwnedStream(Wrap($"<xs:{directive} schemaLocation='main.xsd'/>"));
        var resolver = new Explicit((r, _) => XmlResourceResolution.FromResource(new(stream, r.Uri)));
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:{directive} schemaLocation='child'/>"), new() { BaseUri = Basis, Resolver = resolver }));
        Assert.Equal("SCHEMA.ResourceCycle", Assert.Single(error.Diagnostics).Code); Assert.Equal(1, resolver.Calls); Assert.Equal(1, stream.Disposals);
    }
    [Fact]
    public void MutualImportCycleRemainsLegal()
    {
        var stream = new OwnedStream(Wrap("<xs:import namespace='urn:a' schemaLocation='main.xsd'/><xs:element name='b'/>", "targetNamespace='urn:b'"));
        var resolver = new Explicit((r, _) => XmlResourceResolution.FromResource(new(stream, r.Uri)));
        Assert.NotNull(XmlSchema.Compile(Wrap("<xs:import namespace='urn:b' schemaLocation='child'/><xs:element name='a'/>", "targetNamespace='urn:a'"), new() { BaseUri = Basis, Resolver = resolver })); Assert.Equal(1, resolver.Calls); Assert.Equal(1, stream.Disposals);
    }
    [Fact]
    public void ResolvedSubstantiveRedefineRetainsOriginalComponentSemantics()
    {
        var stream = new OwnedStream(Wrap("<xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>"));
        var resolver = new Explicit((r, _) => XmlResourceResolution.FromResource(new(stream, r.Uri)));
        var schema = XmlSchema.Compile(Wrap("<xs:redefine schemaLocation='child'><xs:simpleType name='T'><xs:restriction base='T'><xs:minInclusive value='2'/></xs:restriction></xs:simpleType></xs:redefine><xs:element name='root' type='T'/>"), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate("<root>2</root>").IsValid); Assert.False(schema.Validate("<root>1</root>").IsValid); Assert.Equal(1, stream.Disposals);
    }
}

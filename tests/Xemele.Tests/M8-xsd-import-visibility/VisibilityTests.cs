using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdImportVisibility.Support;
namespace Xemele.Tests.M8XsdImportVisibility;

public sealed class VisibilityTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void IncludedAuthorsRetainOwnImportsAndChameleonLocalReferences(bool chameleon)
    {
        string child = Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:simpleType name='Local'><xs:restriction base='b:T'/></xs:simpleType><xs:element name='included' type='" + (chameleon ? "Local" : "a:Local") + "'/>", "xmlns:b='urn:b' xmlns:a='urn:a' " + (chameleon ? "" : "targetNamespace='urn:a'"));
        var resolver = Map(new() { ["child"] = child, ["foreign"] = Foreign });
        var schema = XmlSchema.Compile(Wrap("<xs:include schemaLocation='child'/><xs:element name='principal' type='a:Local'/>", "targetNamespace='urn:a' xmlns:a='urn:a'"), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate("<included xmlns='urn:a'>7</included>").IsValid);
        Assert.False(schema.Validate("<included xmlns='urn:a'>bad</included>").IsValid);
        Assert.True(schema.Validate("<principal xmlns='urn:a'>8</principal>").IsValid); resolver.Disposed();
    }
    [Theory]
    [InlineData("<xs:element name='root' type='b:T'/>")]
    [InlineData("<xs:element name='root'><xs:complexType><xs:sequence><xs:element ref='b:b'/></xs:sequence></xs:complexType></xs:element>")]
    [InlineData("<xs:complexType name='C'><xs:attribute ref='b:a'/></xs:complexType>")]
    [InlineData("<xs:complexType name='C'><xs:group ref='b:g'/></xs:complexType>")]
    [InlineData("<xs:complexType name='C'><xs:attributeGroup ref='b:ag'/></xs:complexType>")]
    [InlineData("<xs:simpleType name='L'><xs:list itemType='b:T'/></xs:simpleType>")]
    [InlineData("<xs:simpleType name='U'><xs:union memberTypes='xs:int b:T'/></xs:simpleType>")]
    [InlineData("<xs:simpleType name='R'><xs:restriction base='b:T'/></xs:simpleType>")]
    [InlineData("<xs:element name='member' type='b:T' substitutionGroup='b:b'/>")]
    public void EveryReferenceRouteRequiresTheAuthorImportEvenWhenComponentsExist(string body)
    {
        string child = Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/>");
        var resolver = Map(new() { ["child"] = child, ["foreign"] = Foreign });
        var error = Reject(Wrap("<xs:include schemaLocation='child'/>" + body, "targetNamespace='urn:a' xmlns:b='urn:b'"), resolver);
        Assert.Contains(error.Diagnostics, d => d.Message.Contains("without an import", StringComparison.Ordinal));
        resolver = Map(new() { ["child"] = child, ["foreign"] = Foreign });
        Assert.NotNull(XmlSchema.Compile(Wrap("<xs:import namespace='urn:b'/><xs:include schemaLocation='child'/>" + body, "targetNamespace='urn:a' xmlns:b='urn:b'"), new() { BaseUri = Basis, Resolver = resolver })); resolver.Disposed();
    }
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void PermissionAndAcquisitionAreIndependentAndOrderDoesNotLendImports(int principalImport, bool reverse)
    {
        string permission = principalImport switch { 1 => "<xs:import namespace='urn:b'/>", 2 => "<xs:import namespace='urn:b' schemaLocation='missing'/>", _ => "" };
        string include = "<xs:include schemaLocation='child'/>";
        string child = Wrap("<xs:include schemaLocation='grand'/>");
        string grand = Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:element name='included' type='b:T'/>", "xmlns:b='urn:b'");
        var resolver = Map(new() { ["child"] = child, ["grand"] = grand, ["foreign"] = Foreign });
        string principal = Wrap((reverse ? include + permission : permission + include) + "<xs:element name='root' type='b:T'/>", "targetNamespace='urn:a' xmlns:b='urn:b'");
        if (principalImport == 0) Reject(principal, resolver);
        else { var schema = XmlSchema.Compile(principal, new() { BaseUri = Basis, Resolver = resolver }); Assert.True(schema.Validate("<root xmlns='urn:a'>3</root>").IsValid); Assert.False(schema.Validate("<root xmlns='urn:a'>bad</root>").IsValid); }
        Assert.Equal(principalImport == 2 ? 4 : 3, resolver.Requests.Count); resolver.Disposed();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void IncludedAuthorNamespaceOnlyOrUnavailableImportUsesAlreadyLoadedComponents(bool optionalLocation)
    {
        string child = Wrap("<xs:import namespace='urn:b'" + (optionalLocation ? " schemaLocation='missing'" : "") + "/><xs:element name='included' type='b:T'/>", "xmlns:b='urn:b'");
        var resolver = Map(new() { ["child"] = child, ["foreign"] = Foreign });
        var schema = XmlSchema.Compile(Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:include schemaLocation='child'/>", "targetNamespace='urn:a'"), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate("<included xmlns='urn:a'>7</included>").IsValid); Assert.False(schema.Validate("<included xmlns='urn:a'>bad</included>").IsValid);
        Assert.Equal(optionalLocation ? 3 : 2, resolver.Requests.Count); resolver.Disposed();
    }
    [Fact]
    public void PrincipalPermissionCannotAuthorizeIncludedAuthor()
    {
        var resolver = Map(new() { ["child"] = Wrap("<xs:element name='included' type='b:T'/>", "xmlns:b='urn:b'"), ["foreign"] = Foreign });
        var error = Reject(Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:include schemaLocation='child'/>", "targetNamespace='urn:a'"), resolver);
        Assert.Equal(new Uri(Basis, "child"), Assert.Single(error.Diagnostics).Location.SourceUri);
    }
    [Fact]
    public void NamespaceOnlyPermissionDoesNotInventMissingComponents()
    {
        var resolver = Map(new());
        const string body = "<xs:element name='root' type='b:T'/><xs:element name='good' type='xs:int'/>";
        string source = Wrap("<xs:import namespace='urn:b'/>" + body, "xmlns:b='urn:b'");
        var options = new SchemaCompileOptions { BaseUri = Basis, Resolver = resolver };
        var schema = XmlSchema.Compile(source, options);
        var good = schema.Validate("<good>7</good>"); Assert.True(good.IsComplete); Assert.True(good.IsValid);
        var bad = schema.Validate("<good>bad</good>"); Assert.True(bad.IsComplete); Assert.False(bad.IsValid);
        var missing = schema.Validate("<root>7</root>"); Assert.False(missing.IsComplete); Assert.False(missing.IsValid);
        var diagnostic = Assert.Single(missing.Diagnostics);
        Assert.Equal("XSD.MissingComponent", diagnostic.Code); Assert.Equal(XmlDiagnosticSeverity.Fatal, diagnostic.Severity);
        Assert.DoesNotContain(missing.Diagnostics, d => d.Message.Contains("without an import", StringComparison.Ordinal));

        var context = new SchemaCompileContext(options); var availability = new AvailabilityBuilder(context.Operation);
        var graph = new XmlSchemaCompiler().CompileAvailable(context.Parse(source).Document, context, availability);
        availability.Join(graph); var snapshot = availability.Seal();
        var authored = new SchemaName("urn:b", "T");
        var root = graph.Elements.Single(e => e.Name.LocalName == "root"); Assert.Equal(authored, root.Type);
        var reference = Assert.Single(snapshot.Entries, e => e.Kind == ReferenceKind.ExplicitElementType && e.AuthoredName == authored);
        Assert.Equal(ComponentAvailability.Element(root), reference.Owner);
        Assert.Equal(SymbolSpace.Type, reference.Space); Assert.Equal(AvailabilityState.Unavailable, reference.State);
        Assert.DoesNotContain(graph.Types, t => t.Name == authored);
        Assert.DoesNotContain(graph.SimpleTypes, t => t.Name == authored); Assert.Null(graph.Datatypes.FindType(authored));

        var denied = Reject(Wrap(body, "xmlns:b='urn:b'"), resolver);
        Assert.Contains(denied.Diagnostics, d => d.Message.Contains("without an import", StringComparison.Ordinal));
        var strictContext = new SchemaCompileContext(options);
        IXsdCompiler compiler = new XmlSchemaCompiler();
        var error = Assert.Throws<SchemaCompileException>(() => compiler.Compile(strictContext.Parse(source).Document, options, strictContext.Operation));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction");
        Assert.Empty(resolver.Requests); Assert.Empty(resolver.Streams);
    }
    [Fact]
    public void ImportWithNoNamespaceAuthorizesOnlyItsOwnDocument()
    {
        string child = Wrap("<xs:import schemaLocation='foreign'/>", "targetNamespace='urn:a'");
        string foreign = Wrap(ForeignType);
        var resolver = Map(new() { ["child"] = child, ["foreign"] = foreign });
        string body = "<xs:include schemaLocation='child'/><xs:element name='root' type='T'/>";
        Reject(Wrap(body, "targetNamespace='urn:a'"), resolver);
        resolver = Map(new() { ["child"] = child, ["foreign"] = foreign });
        var schema = XmlSchema.Compile(Wrap("<xs:import/>" + body, "targetNamespace='urn:a'"), new() { BaseUri = Basis, Resolver = resolver }); Assert.True(schema.Validate("<root xmlns='urn:a'>4</root>").IsValid); resolver.Disposed();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MutualImportsKeepEachAuthorsPermissionAndDeclarationOrder(bool reverse)
    {
        string declarations = "<xs:element name='root' type='b:T'/><xs:simpleType name='A'><xs:restriction base='xs:int'/></xs:simpleType>";
        if (reverse) declarations = "<xs:simpleType name='A'><xs:restriction base='xs:int'/></xs:simpleType><xs:element name='root' type='b:T'/>";
        var resolver = Map(new() { ["foreign"] = Wrap("<xs:import namespace='urn:a' schemaLocation='main.xsd'/><xs:simpleType name='T'><xs:restriction base='a:A'/></xs:simpleType>", "targetNamespace='urn:b' xmlns:a='urn:a'") });
        var schema = XmlSchema.Compile(Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/>" + declarations, "targetNamespace='urn:a' xmlns:b='urn:b'"), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate("<root xmlns='urn:a'>5</root>").IsValid); Assert.False(schema.Validate("<root xmlns='urn:a'>bad</root>").IsValid); Assert.Single(resolver.Requests); resolver.Disposed();
    }
    [Fact]
    public void RedefinitionUsesItsAuthorPermissionAndPreservesOriginalSource()
    {
        string old = Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:simpleType name='T'><xs:restriction base='b:T'/></xs:simpleType>", "xmlns:b='urn:b'");
        string redefine = "<xs:redefine schemaLocation='old'><xs:simpleType name='T'><xs:restriction base='a:T'><xs:minInclusive value='2'/></xs:restriction></xs:simpleType></xs:redefine><xs:element name='root' type='a:T'/>";
        var resolver = Map(new() { ["old"] = old, ["foreign"] = Foreign });
        var schema = XmlSchema.Compile(Wrap(redefine, "targetNamespace='urn:a' xmlns:a='urn:a'"), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate("<root xmlns='urn:a'>2</root>").IsValid); Assert.False(schema.Validate("<root xmlns='urn:a'>1</root>").IsValid); resolver.Disposed();
        resolver = Map(new() { ["old"] = old, ["foreign"] = Foreign });
        var error = Reject(Wrap(redefine.Replace("base='a:T'", "base='b:T'", StringComparison.Ordinal), "targetNamespace='urn:a' xmlns:a='urn:a' xmlns:b='urn:b'"), resolver);
        Assert.Contains(error.Diagnostics, d => d.Message.Contains("without an import", StringComparison.Ordinal));
    }
}

using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdCompile;

public sealed class CompositionTests
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly Uri Main = new("https://example.test/main.xsd");
    private static XsdCompiledSchema Compile(string body, string attrs, Dictionary<string, string> files, List<CountingStream> streams, out List<XmlResourceRequest> requests)
    {
        var seen = new List<XmlResourceRequest>();
        requests = seen;
        return CompilerTests.Compile(body, attrs, new()
        {
            BaseUri = Main,
            Resolver = new Resolver(request =>
        {
            seen.Add(request);
            if (!files.TryGetValue(request.Uri.AbsoluteUri, out var text))
            {
                return null;
            }

            var stream = new CountingStream(text);
            streams.Add(stream);
            return new(stream, request.Uri);
        })
        });
    }
    [Fact]
    public void MutualImportsUseBoundedInProgressGraphAndSchemaAuthorization()
    {
        var files = new Dictionary<string, string> { ["https://example.test/b.xsd"] = CompilerTests.Wrap("<xs:import namespace='urn:a' schemaLocation='main.xsd'/><xs:element name='b' type='a:T'/>", "targetNamespace='urn:b' xmlns:a='urn:a'") };
        var streams = new List<CountingStream>();
        var graph = Compile("<xs:import namespace='urn:b' schemaLocation='b.xsd'/><xs:complexType name='T'><xs:sequence><xs:element ref='b:b'/></xs:sequence></xs:complexType>", "targetNamespace='urn:a' xmlns:b='urn:b'", files, streams, out var requests);
        Assert.Single(requests);
        Assert.Equal(XmlResourceKind.Schema, requests[0].Kind);
        Assert.Equal("b.xsd", requests[0].SystemId);
        Assert.Equal(Main, requests[0].Referrer);
        Assert.True(graph.GlobalElements.ContainsKey(new("urn:b", "b")));
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }
    [Fact]
    public void ChameleonIdentityIncludesEffectiveNamespaceAndDuplicateIncludesAreCached()
    {
        var files = new Dictionary<string, string>
        {
            ["https://example.test/a.xsd"] = CompilerTests.Wrap("<xs:include schemaLocation='shared.xsd'/>", "targetNamespace='urn:a'"),
            ["https://example.test/b.xsd"] = CompilerTests.Wrap("<xs:include schemaLocation='shared.xsd'/>", "targetNamespace='urn:b'"),
            ["https://example.test/shared.xsd"] = CompilerTests.Wrap("<xs:simpleType name='T'><xs:restriction base='xs:string'/></xs:simpleType><xs:element name='e' type='T'/>")
        };
        var streams = new List<CountingStream>();
        var graph = Compile("<xs:import namespace='urn:a' schemaLocation='a.xsd'/><xs:import namespace='urn:b' schemaLocation='b.xsd'/><xs:import namespace='urn:a' schemaLocation='a.xsd'/>", "", files, streams, out var requests);
        Assert.Equal(4, requests.Count);
        Assert.Equal(new SchemaName("urn:a", "T"), graph.Elements[graph.GlobalElements[new("urn:a", "e")]].Type);
        Assert.Equal(new SchemaName("urn:b", "T"), graph.Elements[graph.GlobalElements[new("urn:b", "e")]].Type);
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }
    [Fact]
    public void RedefinePreservesOriginalDerivationAndSelfGroupTargets()
    {
        var files = new Dictionary<string, string> { ["https://example.test/base.xsd"] = CompilerTests.Wrap("<xs:simpleType name='S'><xs:restriction base='xs:string'/></xs:simpleType><xs:complexType name='T'><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType><xs:group name='g'><xs:sequence><xs:element name='old'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute name='old'/></xs:attributeGroup>") };
        var streams = new List<CountingStream>();
        var graph = Compile("<xs:redefine schemaLocation='base.xsd'><xs:simpleType name='S'><xs:restriction base='S'><xs:maxLength value='5'/></xs:restriction></xs:simpleType><xs:complexType name='T'><xs:complexContent><xs:extension base='T'><xs:sequence><xs:element name='b'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType><xs:group name='g'><xs:sequence><xs:group ref='g'/><xs:element name='new'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attributeGroup ref='ag'/><xs:attribute name='new'/></xs:attributeGroup></xs:redefine><xs:complexType name='Use'><xs:group ref='g'/><xs:attributeGroup ref='ag'/></xs:complexType>", "", files, streams, out _);
        var s = graph.SimpleTypes.Single(t => t.Name == new SchemaName("", "S"));
        Assert.StartsWith("#redefined", s.BaseType!.Value.LocalName);
        Assert.Equal(2, graph.Types.Single(t => t.Name == new SchemaName("", "Use")).Attributes.Count);
        Assert.Equal(1, streams[0].Disposals);
    }
    [Fact]
    public void SuccessiveRedefinitionsBindEachBaseToItsImmediatelyPreviousVersion()
    {
        var files = new Dictionary<string, string>
        {
            ["https://example.test/base.xsd"] = CompilerTests.Wrap("<xs:simpleType name='S'><xs:restriction base='xs:string'/></xs:simpleType>"),
            ["https://example.test/mid.xsd"] = CompilerTests.Wrap("<xs:redefine schemaLocation='base.xsd'><xs:simpleType name='S'><xs:restriction base='S'><xs:maxLength value='10'/></xs:restriction></xs:simpleType></xs:redefine>")
        };
        var graph = Compile("<xs:redefine schemaLocation='mid.xsd'><xs:simpleType name='S'><xs:restriction base='S'><xs:maxLength value='5'/></xs:restriction></xs:simpleType></xs:redefine>", "", files, [], out _);
        var current = graph.Datatypes.FindType(new("", "S"))!;
        var previous = graph.Datatypes.FindType(current.BaseType!.Value)!;
        var original = graph.Datatypes.FindType(previous.BaseType!.Value)!;
        Assert.Equal("5", Assert.Single(current.Facets).LexicalValue);
        Assert.Equal("10", Assert.Single(previous.Facets).LexicalValue);
        Assert.Empty(original.Facets);
        Assert.Equal(new SchemaName(Xs, "string"), original.BaseType);
    }
    [Fact]
    public void RedefineKeepsRecursiveReplacementReferencesAndDistinctSymbolSpaces()
    {
        var files = new Dictionary<string, string> { ["https://example.test/base.xsd"] = CompilerTests.Wrap("<xs:complexType name='T'><xs:sequence><xs:element name='old' type='T' minOccurs='0'/></xs:sequence></xs:complexType><xs:element name='T' type='xs:string'/>") };
        var graph = Compile("<xs:redefine schemaLocation='base.xsd'><xs:complexType name='T'><xs:complexContent><xs:extension base='T'><xs:sequence><xs:element name='child' type='T' minOccurs='0'/><xs:element ref='T'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType></xs:redefine>", "", files, [], out _);
        var type = graph.Types.Single(t => t.Name == new SchemaName("", "T"));
        Assert.StartsWith("#redefined", type.BaseType!.Value.LocalName);
        Assert.Equal(new SchemaName("", "T"), graph.Elements.Single(e => e.Name.LocalName == "child").Type);
        Assert.Equal(new SchemaName(Xs, "string"), graph.Elements[graph.GlobalElements[new("", "T")]].Type);
        Assert.Equal(new SchemaName("", "T"), graph.Elements.Single(e => e.Name.LocalName == "old").Type);
    }
    [Fact]
    public void ZeroSelfReferenceRedefineRetainsOldAndNewRestrictionPrograms()
    {
        var files = new Dictionary<string, string> { ["https://example.test/base.xsd"] = CompilerTests.Wrap("<xs:group name='g'><xs:choice><xs:element name='a'/><xs:element name='b'/></xs:choice></xs:group><xs:attributeGroup name='ag'><xs:attribute name='a'/><xs:attribute name='b'/></xs:attributeGroup>") };
        var graph = Compile("<xs:redefine schemaLocation='base.xsd'><xs:group name='g'><xs:sequence><xs:element name='a'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute name='a'/></xs:attributeGroup></xs:redefine><xs:complexType name='T'><xs:group ref='g'/><xs:attributeGroup ref='ag'/></xs:complexType>", "", files, [], out _);
        var restrictions = graph.Types.Where(t => t.Name.LocalName is "#model-group:g" or "#attribute-group:ag").ToArray();
        Assert.Equal(2, restrictions.Length);
        foreach (var restriction in restrictions)
        {
            Assert.True(restriction.Anonymous);
            Assert.Equal(XsdDerivation.Restriction, restriction.Derivation);
            Assert.Contains(graph.Types, t => t.Name == restriction.BaseType && t.Anonymous);
        }
        Assert.Equal(2, graph.Types.Single(t => t.Name == restrictions.Single(t => t.Name.LocalName == "#attribute-group:ag").BaseType).Attributes.Count);
    }
    [Fact]
    public void FinalRedirectUriControlsNestedCacheSourceAndXmlBase()
    {
        var requests = new List<XmlResourceRequest>();
        var streams = new List<CountingStream>();
        var resolver = new Resolver(request =>
        {
            requests.Add(request);
            var first = requests.Count == 1;
            var xml = first ? CompilerTests.Wrap("<xs:include schemaLocation='next.xsd'/>", "xml:base='nested/'") : CompilerTests.Wrap("<xs:element name='leaf'/>");
            var stream = new CountingStream(xml);
            streams.Add(stream);
            return new(stream, first ? new Uri("https://redirect.test/path/final.xsd") : request.Uri);
        });
        var graph = CompilerTests.Compile("<xs:include schemaLocation='initial.xsd'/>", options: new()
        {
            BaseUri = Main,
            Resolver = resolver
        });
        Assert.Equal(new Uri("https://redirect.test/path/nested/next.xsd"), requests[1].Uri);
        Assert.Equal(requests[1].Uri, graph.Elements[0].Location.SourceUri);
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }
    [Theory]
    [InlineData("<xs:include schemaLocation='child.xsd'/>", "", "targetNamespace='urn:other'")]
    [InlineData("<xs:import namespace='urn:a' schemaLocation='child.xsd'/>", "", "targetNamespace='urn:b'")]
    [InlineData("<xs:import namespace='urn:a' schemaLocation='child.xsd'/>", "targetNamespace='urn:a'", "targetNamespace='urn:a'")]
    [InlineData("<xs:redefine schemaLocation='child.xsd'><xs:complexType name='missing'><xs:complexContent><xs:extension base='missing'/></xs:complexContent></xs:complexType></xs:redefine>", "", "")]
    public void NamespaceAndRedefinitionRestrictionsReject(string body, string attrs, string childAttrs)
    {
        var files = new Dictionary<string, string> { ["https://example.test/child.xsd"] = CompilerTests.Wrap("", childAttrs) };
        var streams = new List<CountingStream>();
        Assert.Throws<SchemaCompileException>(() => Compile(body, attrs, files, streams, out _));
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }
    [Fact]
    public void IllegalIncludeCycleAndCollisionKeepSourceIdentity()
    {
        var files = new Dictionary<string, string> { ["https://example.test/child.xsd"] = CompilerTests.Wrap("<xs:include schemaLocation='main.xsd'/>") };
        var streams = new List<CountingStream>();
        var error = Assert.Throws<SchemaCompileException>(() => Compile("<xs:include schemaLocation='child.xsd'/>", "", files, streams, out _));
        Assert.Equal("SCHEMA.ResourceCycle", error.Diagnostics[^1].Code);
        Assert.Equal(new Uri("https://example.test/child.xsd"), error.Diagnostics[^1].Location.SourceUri);
        files["https://example.test/child.xsd"] = CompilerTests.Wrap("<xs:element name='same'/>");
        var collision = Assert.Throws<SchemaCompileException>(() => Compile("<xs:include schemaLocation='child.xsd'/><xs:element name='same'/>", "", files, [], out _));
        Assert.Equal("XSD.SchemaRestriction", collision.Diagnostics[0].Code);
    }
    [Fact]
    public void MissingResolverAndExternalExceptionsDoNotBecomeFakeSchemaSuccess()
    {
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile("<xs:include schemaLocation='urn:child'/>")).Diagnostics[0].Code);
        var caller = new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Error, "caller", default)]);
        Assert.Same(caller, Assert.Throws<XmlParseException>(() => CompilerTests.Compile("<xs:include schemaLocation='urn:child'/>", options: new() { Resolver = new Resolver(_ => throw caller) })));
    }
    private sealed class Resolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return resolve(request);
        }
    }
    internal sealed class CountingStream(string xml) : MemoryStream(Encoding.UTF8.GetBytes(xml))
    {
        internal int Disposals; protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }
}

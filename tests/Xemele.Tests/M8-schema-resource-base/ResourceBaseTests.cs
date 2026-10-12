using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8SchemaResourceBase;

public sealed class ResourceBaseTests
{
    private static readonly Uri Source = new("https://source.test/original/schema.xml");
    private static readonly Uri Basis = new("https://base.test/elsewhere/");
    private static readonly Uri Fallback = new("https://fallback.test/options.xml");
    private static readonly XmlLocation At = new(new Uri("https://location.test/diagnostic.xml"), 7, 3, 41);

    [Fact]
    public void EffectiveBaseSourceAndDiagnosticUriHaveIndependentRoles()
    {
        XmlResourceRequest? seen = null;
        var final = new Uri("https://redirect.test/final.xml");
        var stream = new CountingStream("<r/>");
        var context = new SchemaCompileContext(new() { BaseUri = Fallback, Resolver = new Resolver(r => { seen = r; return new(stream, final); }) });
        var document = context.Resources.Load(new("../child.xml", Source, SchemaResourcePurpose.XsdInclude, At), Basis, context.Operation);
        AssertRequest(seen!, new("https://base.test/child.xml"), Source, "../child.xml");
        Assert.Equal(final, context.Resources.SourceUri(document));
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public void AbsoluteHrefNeedsNoEffectiveBase()
    {
        XmlResourceRequest? seen = null;
        var stream = new CountingStream("<r/>");
        var context = new SchemaCompileContext(new() { BaseUri = Fallback, Resolver = new Resolver(r => { seen = r; return new(stream, r.Uri); }) });
        context.Resources.Load(new("https://absolute.test/child.xml", Source, SchemaResourcePurpose.RelaxNgInclude, At), null, context.Operation);
        AssertRequest(seen!, new("https://absolute.test/child.xml"), Source, "https://absolute.test/child.xml");
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public void ExplicitNullBaseDoesNotUseReferrerOptionsOrDiagnosticLocation()
    {
        int calls = 0;
        var context = new SchemaCompileContext(new() { BaseUri = Fallback, Resolver = new Resolver(_ => { calls++; return null; }) });
        var error = Assert.Throws<SchemaCompileException>(() => context.Resources.Load(new("child.xml", Source, SchemaResourcePurpose.XsdInclude, At), null, context.Operation));
        Assert.Equal(0, calls);
        var diagnostic = Assert.Single(error.Diagnostics);
        Assert.Equal("SCHEMA.ResourceDenied", diagnostic.Code);
        Assert.Equal(XmlDiagnosticSeverity.Fatal, diagnostic.Severity);
        Assert.Equal(At, diagnostic.Location);
        Assert.False(context.Operation.IsComplete);
    }

    [Fact]
    public void RelativeEffectiveBaseIsAnArgumentErrorBeforeResolver()
    {
        int calls = 0;
        var context = new SchemaCompileContext(new() { Resolver = new Resolver(_ => { calls++; return null; }) });
        var error = Assert.Throws<ArgumentException>(() => context.Resources.Load(new("urn:absolute", Source, SchemaResourcePurpose.XsdInclude, At), new("relative/", UriKind.Relative), context.Operation));
        Assert.Equal("effectiveBaseUri", error.ParamName);
        Assert.Equal(0, calls);
        Assert.True(context.Operation.IsComplete);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FrozenInterfacePreservesReferrerThenOptionsFallback(bool hasReferrer)
    {
        XmlResourceRequest? seen = null;
        var stream = new CountingStream("<r/>");
        var context = new SchemaCompileContext(new() { BaseUri = Fallback, Resolver = new Resolver(r => { seen = r; return new(stream, r.Uri); }) });
        ISchemaResourceLoader loader = context.Resources;
        loader.Load(new("child.xml", hasReferrer ? Source : null, SchemaResourcePurpose.SchematronInclude, At), context.Operation);
        var expected = hasReferrer ? Source : Fallback;
        AssertRequest(seen!, new(expected, "child.xml"), expected, "child.xml");
        Assert.Equal(1, stream.Disposals);
    }

    [Theory]
    [InlineData("include")]
    [InlineData("import")]
    [InlineData("redefine")]
    public void XsdRedirectAndInheritedBaseKeepActualReferringSource(string kind)
    {
        var requests = new List<XmlResourceRequest>();
        var streams = new List<CountingStream>();
        var final = new Uri("https://redirect.test/path/final.xsd");
        string initialBody = kind == "import" ? "<xs:import namespace='urn:child' schemaLocation='initial.xsd'/>" : $"<xs:{kind} schemaLocation='initial.xsd'/>";
        string attrs = kind == "import" ? "targetNamespace='urn:child'" : "";
        string imported = Xsd("<xs:include xml:base='nested/' schemaLocation='next.xsd'/>", "xml:base='parent/' " + attrs);
        var options = new SchemaCompileOptions { BaseUri = Source, Resolver = new Resolver(r =>
        {
            requests.Add(r);
            bool first = requests.Count == 1;
            var stream = new CountingStream(first ? imported : Xsd("<xs:element name='leaf'/>", attrs)); streams.Add(stream);
            return new(stream, first ? final : r.Uri);
        }) };
        var context = new SchemaCompileContext(options);
        var document = context.Parse(Xsd(initialBody, "xml:base='https://base.test/elsewhere/'")).Document;
        var graph = new XmlSchemaCompiler().Compile(document, context.Options, context.Operation);
        Assert.Equal(2, requests.Count);
        AssertRequest(requests[0], new("https://base.test/elsewhere/initial.xsd"), Source, "initial.xsd");
        AssertRequest(requests[1], new("https://redirect.test/path/parent/nested/next.xsd"), final, "next.xsd");
        Assert.Equal(requests[1].Uri, Assert.Single(graph.Elements).Location.SourceUri);
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RngRedirectAndInheritedBaseKeepActualReferringSource(bool include)
    {
        const string ns = "http://relaxng.org/ns/structure/1.0";
        var requests = new List<XmlResourceRequest>();
        var streams = new List<CountingStream>();
        var final = new Uri("https://redirect.test/path/final.rng");
        string main = include
            ? $"<grammar xmlns='{ns}' xml:base='https://base.test/elsewhere/'><include href='initial.rng'/></grammar>"
            : $"<externalRef xmlns='{ns}' xml:base='https://base.test/elsewhere/' href='initial.rng'/>";
        string imported = include
            ? $"<grammar xmlns='{ns}' xml:base='parent/'><include xml:base='nested/' href='next.rng'/></grammar>"
            : $"<grammar xmlns='{ns}' xml:base='parent/'><start><externalRef xml:base='nested/' href='next.rng'/></start></grammar>";
        string leaf = $"<element xmlns='{ns}' name='leaf'><empty/></element>";
        string last = include ? $"<grammar xmlns='{ns}'><start>{leaf}</start></grammar>" : leaf;
        var context = new SchemaCompileContext(new() { BaseUri = Source, Resolver = new Resolver(r =>
        {
            requests.Add(r);
            bool first = requests.Count == 1;
            var stream = new CountingStream(first ? imported : last); streams.Add(stream);
            return new(stream, first ? final : r.Uri);
        }) });
        var document = context.Parse(main).Document;
        var graph = new RelaxNgCompiler().Compile(document, context.Options, context.Operation);
        Assert.Equal(2, requests.Count);
        AssertRequest(requests[0], new("https://base.test/elsewhere/initial.rng"), Source, "initial.rng");
        AssertRequest(requests[1], new("https://redirect.test/path/parent/nested/next.rng"), final, "next.rng");
        Assert.Equal(requests[1].Uri, Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Element).Location.SourceUri);
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }

    private static string Xsd(string body, string attrs = "") => $"<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' {attrs}>{body}</xs:schema>";
    private static void AssertRequest(XmlResourceRequest request, Uri uri, Uri referrer, string lexical)
    {
        Assert.Equal(uri, request.Uri); Assert.Equal(referrer, request.Referrer);
        Assert.Equal(XmlResourceKind.Schema, request.Kind);
        Assert.Equal(lexical, request.SystemId); Assert.Null(request.PublicId);
    }
    private sealed class Resolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return resolve(request); }
    }
    private sealed class CountingStream(string xml) : MemoryStream(Encoding.UTF8.GetBytes(xml))
    {
        internal int Disposals;
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
}

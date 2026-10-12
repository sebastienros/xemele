using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8RngValidate;

public sealed class ResourceTests
{
    private static string External(string href) => "<externalRef xmlns='" + EvaluationTests.Ns + "' href='" + href + "'/>";
    private sealed class Resolver(Func<XmlResourceRequest, CancellationToken, XmlResolvedResource?> resolve) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest r, CancellationToken token) => resolve(r, token); }
    private sealed class CountStream(string source) : MemoryStream(Encoding.UTF8.GetBytes(source))
    { internal int Disposals; protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); } }
    [Fact]
    public void RedirectedExternalReferenceKeepsReferrerBaseContextAndDisposal()
    {
        var seen = new List<XmlResourceRequest>(); var streams = new List<CountStream>();
        var options = new SchemaCompileOptions { BaseUri = new("https://example.test/start/root.rng"), Resolver = new Resolver((request, _) =>
        {
            seen.Add(request); var source = seen.Count == 1 ? External("child.rng") : EvaluationTests.Element("<value type='QName' ns='urn:v'>x</value>", "datatypeLibrary='" + EvaluationTests.Xsd + "'");
            var stream = new CountStream(source); streams.Add(stream); return new(stream, new("https://example.test/final/" + (seen.Count == 1 ? "parent.rng" : "child.rng")));
        }) };
        var schema = RelaxNgSchema.Compile(External("parent.rng"), options);
        Assert.True(schema.Validate("<r xmlns:p='urn:v'>p:x</r>").IsValid);
        Assert.Equal(new Uri("https://example.test/start/parent.rng"), seen[0].Uri);
        Assert.Equal(new Uri("https://example.test/final/child.rng"), seen[1].Uri);
        Assert.All(seen, request => Assert.Equal(XmlResourceKind.Schema, request.Kind)); Assert.All(streams, stream => Assert.Equal(1, stream.Disposals));
    }
    [Fact]
    public void IncludeOverrideAndInvalidDetachedImportedDatatypeAreFinalized()
    {
        string imported = "<grammar xmlns='" + EvaluationTests.Ns + "'><start><ref name='x'/></start><define name='x'><element name='old'><empty/></element></define></grammar>";
        var options = new SchemaCompileOptions { Resolver = new Resolver((r, _) => new(new CountStream(imported), r.Uri)) };
        string source = "<grammar xmlns='" + EvaluationTests.Ns + "'><include href='urn:import'><define name='x'><element name='r'><empty/></element></define></include></grammar>";
        var schema = RelaxNgSchema.Compile(source, options); Assert.True(schema.Validate("<r/>").IsValid); Assert.False(schema.Validate("<old/>").IsValid);
        imported = "<grammar xmlns='" + EvaluationTests.Ns + "' datatypeLibrary='" + EvaluationTests.Xsd + "'><start><element name='r'><empty/></element></start><define name='unused'><value type='int'>bad</value></define></grammar>";
        var error = Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(External("urn:import"), options));
        var diagnostic = Assert.Single(error.Diagnostics); Assert.Equal("RNG.Datatype", diagnostic.Code); Assert.Equal(new Uri("urn:import"), diagnostic.Location.SourceUri); Assert.NotNull(diagnostic.Location.ByteOffset);
    }
    [Fact]
    public void RefusalCycleExternalByteBudgetAndCallerExceptionAreDistinct()
    {
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(External("urn:r"))).Diagnostics[^1].Code);
        var resources = new List<CountStream>();
        var cycle = new SchemaCompileOptions { Resolver = new Resolver((_, _) => { var s = new CountStream(External("urn:alias")); resources.Add(s); return new(s, new("urn:final")); }) };
        Assert.Equal("SCHEMA.ResourceCycle", Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(External("urn:r"), cycle)).Diagnostics[^1].Code); Assert.All(resources, s => Assert.Equal(1, s.Disposals));
        string content = EvaluationTests.Element("<empty/>");
        var options = new SchemaCompileOptions { Limits = new() { ExternalBytes = Encoding.UTF8.GetByteCount(content) }, Resolver = new Resolver((r, _) => new(new CountStream(content), r.Uri)) };
        Assert.True(RelaxNgSchema.Compile(External("urn:r"), options).Validate("<r/>").IsValid);
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(External("urn:r"), options with { Limits = options.Limits with { ExternalBytes = Encoding.UTF8.GetByteCount(content) - 1 } })).Diagnostics[^1].Code);
        var caller = new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Fatal, "caller", default)]);
        Assert.Same(caller, Assert.Throws<XmlParseException>(() => RelaxNgSchema.Compile(External("urn:r"), new() { Resolver = new Resolver((_, _) => throw caller) })));
    }
    [Fact]
    public void ResolverMutationAndCancellationAreDetectedAndResourcesDisposed()
    {
        var document = XmlDocument.Parse(External("urn:r"), cancellationToken: TestContext.Current.CancellationToken);
        var stream = new CountStream(EvaluationTests.Element("<empty/>"));
        Assert.Throws<InvalidOperationException>(() => RelaxNgSchema.Compile(document, new() { Resolver = new Resolver((r, _) => { document.RootElement.SetAttribute("ns", "urn:changed"); return new(stream, r.Uri); }) })); Assert.Equal(1, stream.Disposals);
        using var cancel = new CancellationTokenSource(); var canceled = new CountStream(EvaluationTests.Element("<empty/>"));
        Assert.ThrowsAny<OperationCanceledException>(() => RelaxNgSchema.Compile(External("urn:r"), new() { CancellationToken = cancel.Token, Resolver = new Resolver((r, _) => { cancel.Cancel(); return new(canceled, r.Uri); }) })); Assert.Equal(1, canceled.Disposals);
    }
}

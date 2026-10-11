using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8RngCompile;

public sealed class ResourceTests
{
    private const string Ns = "http://relaxng.org/ns/structure/1.0";
    private static string Grammar(string content) => "<grammar xmlns='" + Ns + "'>" + content + "</grammar>";
    private static string External(string href) => "<externalRef xmlns='" + Ns + "' href='" + href + "'/>";
    [Fact]
    public void IncludeOverridesDivAndFinalResourceCoordinatesRemainOriginal()
    {
        var stream = new CountStream(Grammar("<div><start><ref name='x'/></start><define name='x'><element name='old'><empty/></element></define></div>"));
        XmlResourceRequest? request = null; var final = new Uri("https://example.test/redirect/import.rng");
        var graph = CompilerTests.Compile(Grammar("<include href='import.rng'><div><define name='x'><element name='new'><empty/></element></define></div></include>"),
            new() { BaseUri = new("https://example.test/principal/root.rng"), Resolver = new Resolver((r, _) => { request = r; return new(stream, final); }) });
        Assert.Equal(1, stream.Disposals); Assert.Equal(XmlResourceKind.Schema, request!.Kind); Assert.Equal("import.rng", request.SystemId);
        Assert.Equal(new Uri("https://example.test/principal/import.rng"), request.Uri);
        var element = Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Element); Assert.Equal("new", Assert.IsType<RngName>(element.NameClass).Name.LocalName);
        Assert.Equal(new Uri("https://example.test/principal/root.rng"), element.Location.SourceUri); Assert.Null(element.Location.ByteOffset);
        var external = CompilerTests.Compile(External("urn:input"), new() { Resolver = new Resolver((_, _) => new(new CountStream("<element xmlns='" + Ns + "' name='e'><value>x</value></element>"), final)) });
        var value = Assert.Single(external.Patterns, p => p.Kind == RngPatternKind.Value);
        Assert.Equal(final, value.Location.SourceUri); Assert.Equal(final, value.Context.BaseUri); Assert.NotNull(value.Location.ByteOffset); Assert.Equal(1, value.Location.LineNumber);
    }
    [Theory]
    [InlineData("<start><element name='e'><empty/></element></start>")]
    [InlineData("<define name='missing'><empty/></define>")]
    public void IncludeOverridesRequireCorrespondingImportedComponent(string replacement)
    {
        var options = new SchemaCompileOptions { Resolver = new Resolver((_, _) => new(new CountStream(Grammar("<define name='x'><empty/></define>")), new("urn:import"))) };
        Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(Grammar("<include href='urn:import'>" + replacement + "</include>"), options));
    }
    [Fact]
    public void OverridesCannotHideFullGrammarErrorsOrUnexpandedResources()
    {
        int calls = 0;
        var options = new SchemaCompileOptions { Resolver = new Resolver((r, _) =>
        {
            calls++; return new(new CountStream(r.Uri.OriginalString == "urn:import" ? Grammar("<start><ref name='x'/></start><define name='x'><externalRef href='urn:child'/></define>") : "<element xmlns='" + Ns + "' name='old'><empty/></element>"), r.Uri);
        }) };
        CompilerTests.Compile(Grammar("<include href='urn:import'><define name='x'><element name='new'><empty/></element></define></include>"), options);
        Assert.Equal(2, calls);
        options = options with { Resolver = new Resolver((_, _) => new(new CountStream(Grammar("<start><ref name='x'/></start><define name='x'><unknown/></define>")), new("urn:import"))) };
        Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(Grammar("<include href='urn:import'><define name='x'><element name='new'><empty/></element></define></include>"), options));
    }
    [Fact]
    public void ExternalNsTransfersButLibraryAndPrefixMapsDoNot()
    {
        var options = new SchemaCompileOptions { Resolver = new Resolver((_, _) => new(new CountStream("<element xmlns='" + Ns + "' name='e'><value>x</value></element>"), new("urn:final"))) };
        var graph = CompilerTests.Compile("<externalRef xmlns='" + Ns + "' xmlns:p='urn:caller' ns='urn:inherited' datatypeLibrary='http://www.w3.org/2001/XMLSchema-datatypes' href='urn:request'/>", options);
        var element = Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Element); Assert.Equal("urn:inherited", Assert.IsType<RngName>(element.NameClass).Name.NamespaceUri);
        var value = Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Value); Assert.Equal(new SchemaName("", "token"), value.Datatype); Assert.False(value.Context.Prefixes.ContainsKey("p"));
    }
    [Fact]
    public void FinalUriRedirectCyclesAndNestedMixedCompositionDisposeExactlyOnce()
    {
        var streams = new List<CountStream>(); int calls = 0;
        var options = new SchemaCompileOptions { BaseUri = new("urn:principal"), Resolver = new Resolver((r, _) =>
        { calls++; var stream = new CountStream(External("urn:alias")); streams.Add(stream); return new(stream, new("urn:redirect")); }) };
        var error = Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(External("urn:first"), options));
        Assert.Equal("SCHEMA.ResourceCycle", error.Diagnostics[^1].Code); Assert.Equal(2, calls); Assert.All(streams, s => Assert.Equal(1, s.Disposals));
        var direct = options with { Resolver = new Resolver((_, _) => new(new CountStream(External("urn:principal")), new("urn:resource"))) };
        Assert.Equal("SCHEMA.ResourceCycle", Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(External("urn:first"), direct)).Diagnostics[^1].Code);
    }
    [Fact]
    public void ResourceByteCountDepthAndWorkLimitsAggregateWithExactBoundary()
    {
        string child = "<element xmlns='" + Ns + "' name='e'><empty/></element>";
        var options = new SchemaCompileOptions { Limits = new() { ExternalBytes = Encoding.UTF8.GetByteCount(child), ExternalResources = 1, InclusionDepth = 1 }, Resolver = new Resolver((r, _) => new(new CountStream(child), r.Uri)) };
        Assert.NotEmpty(CompilerTests.Compile(External("urn:r"), options).Patterns);
        var bytes = options with { Limits = options.Limits with { ExternalBytes = Encoding.UTF8.GetByteCount(child) - 1 } };
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(External("urn:r"), bytes)).Diagnostics[^1].Code);
        string siblings = "<element xmlns='" + Ns + "' name='root'><choice><externalRef href='urn:a'/><externalRef href='urn:b'/></choice></element>";
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(siblings, options)).Diagnostics[^1].Code);
        var nested = options with { Limits = new() { InclusionDepth = 1 }, Resolver = new Resolver((r, _) => new(new CountStream(r.Uri.OriginalString == "urn:a" ? External("urn:b") : child), r.Uri)) };
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(External("urn:a"), nested)).Diagnostics[^1].Code);
        string xml = "<element xmlns='" + Ns + "' name='e'><empty/></element>";
        var document = XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken); var measured = new Counting(new());
        new RelaxNgCompiler().Compile(document, new(), measured);
        Assert.True(measured.Work > 0); Assert.True(measured.Components > 0);
        var exact = new SchemaCompileOptions { MaxOperations = measured.Work, MaxComponents = measured.Components };
        new RelaxNgCompiler().Compile(document, exact, new SchemaCompileOperation(exact));
        foreach (var cap in new[] { exact with { MaxOperations = measured.Work - 1 }, exact with { MaxComponents = measured.Components - 1 } })
            Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => new RelaxNgCompiler().Compile(document, cap, new SchemaCompileOperation(cap))).Diagnostics[^1].Code);
    }
    [Fact]
    public void RefusalSyntaxCallerFailuresCancellationAndMutationPreserveProvenance()
    {
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(External("relative"))).Diagnostics[^1].Code);
        var streams = new List<CountStream>(); var invalid = new SchemaCompileOptions { Resolver = new Resolver((r, _) => { var stream = new CountStream("<bad>"); streams.Add(stream); return new(stream, r.Uri); }) };
        Assert.Equal("SCHEMA.Syntax", Assert.Throws<SchemaCompileException>(() => CompilerTests.Compile(External("urn:r"), invalid)).Diagnostics[^1].Code); Assert.Equal(1, streams[0].Disposals);
        var caller = new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Error, "caller", default)]);
        var throwing = new SchemaCompileOptions { Resolver = new Resolver((_, _) => throw caller) };
        Assert.Same(caller, Assert.Throws<XmlParseException>(() => CompilerTests.Compile(External("urn:r"), throwing)));
        using var cancellation = new CancellationTokenSource(); var returned = new CountStream("<empty xmlns='" + Ns + "'/>");
        var canceled = new SchemaCompileOptions { CancellationToken = cancellation.Token, Resolver = new Resolver((_, _) => { cancellation.Cancel(); return new(returned, new("urn:r")); }) };
        Assert.Throws<OperationCanceledException>(() => CompilerTests.Compile(External("urn:r"), canceled)); Assert.Equal(1, returned.Disposals);
        var document = XmlDocument.Parse(External("urn:r"), cancellationToken: TestContext.Current.CancellationToken);
        var changing = new SchemaCompileOptions { Resolver = new Resolver((r, _) => { document.RootElement.SetAttribute("ns", "changed"); return new(new CountStream("<empty xmlns='" + Ns + "'/>"), r.Uri); }) };
        Assert.Throws<InvalidOperationException>(() => new RelaxNgCompiler().Compile(document, changing, new SchemaCompileOperation(changing)));
    }
    [Fact]
    public void IncompleteInputRejectsAndUnchangedByteLocationsAreExact()
    {
        var partial = XmlDocument.ParseWithDiagnostics("<e>\0", new() { Recover = true }, TestContext.Current.CancellationToken).Value!;
        Assert.Equal("SCHEMA.IncompleteInput", Assert.Throws<SchemaCompileException>(() => new RelaxNgCompiler().Compile(partial, new(), new SchemaCompileOperation())).Diagnostics[0].Code);
        string xml = "<element xmlns='" + Ns + "' name='e'>\r\n <attribute name='a'><element name='illegal'><empty/></element></attribute></element>";
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(xml)); var origin = new Uri("urn:bytes");
        var document = XmlDocument.Load(bytes, null, origin, TestContext.Current.CancellationToken);
        var error = Assert.Throws<SchemaCompileException>(() => new RelaxNgCompiler().Compile(document, new(), new SchemaCompileOperation()));
        Assert.Equal(origin, error.Diagnostics[0].Location.SourceUri); Assert.Equal(2, error.Diagnostics[0].Location.LineNumber);
        Assert.Equal(22, error.Diagnostics[0].Location.LinePosition); Assert.Equal(Encoding.UTF8.GetByteCount(xml.AsSpan(0, xml.IndexOf("<element name='illegal'", StringComparison.Ordinal))), error.Diagnostics[0].Location.ByteOffset);
    }
    internal sealed class Counting(SchemaCompileOptions options) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(options);
        internal long Work; internal int Components;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation location) { bool result = inner.Charge(work, location); if (result) Work += work; return result; }
        public bool ChargeComponents(int count, XmlLocation location) { bool result = inner.ChargeComponents(count, location); if (result) Components += count; return result; }
        public bool AddDiagnostic(XmlDiagnostic value) => inner.AddDiagnostic(value);
        public void Refuse(string code, string message, XmlLocation location) => inner.Refuse(code, message, location);
    }
    private sealed class Resolver(Func<XmlResourceRequest, CancellationToken, XmlResolvedResource?> resolve) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => resolve(request, cancellationToken); }
    private sealed class CountStream(string xml) : MemoryStream(Encoding.UTF8.GetBytes(xml), writable: false)
    { internal int Disposals; protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); } }
}

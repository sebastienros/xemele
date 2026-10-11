using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8Model;

public sealed class ResourceTests
{
    private static readonly XmlLocation At = new(new Uri("https://example.test/from/schema.xsd"), 4, 2, 10);
    private static SchemaResourceReference Reference(string href = "next.xsd") => new(href, At.SourceUri, SchemaResourcePurpose.XsdInclude, At);
    [Fact]
    public void ResolverSeesSchemaKindLexicalReferenceAndFinalUriControlsNestedBase()
    {
        var stream = new ModelTests.CountStream("<schema xml:base='nested/'/>");
        var final = new Uri("https://example.test/redirect/final.xsd"); XmlResourceRequest? seen = null;
        var context = new SchemaCompileContext(new() { Resolver = new Resolver((request, _) => { seen = request; return new(stream, final); }) });
        var document = context.Resources.Load(Reference(), context.Operation);
        Assert.Equal(XmlResourceKind.Schema, seen!.Kind); Assert.Equal(new Uri("https://example.test/from/next.xsd"), seen.Uri);
        Assert.Equal("next.xsd", seen.SystemId); Assert.Null(seen.PublicId); Assert.Equal(At.SourceUri, seen.Referrer);
        Assert.Equal(final, context.Resources.SourceUri(document)); Assert.Equal(new Uri("https://example.test/redirect/nested/"), context.Open(document).Context(document.RootElement).BaseUri);
        Assert.Equal(1, stream.Disposals);
        Assert.Throws<ArgumentException>(() => context.Resources.Load(Reference(), new SchemaCompileOperation()));
    }
    [Fact]
    public void NullResolverAndMissingBaseRefuseBeforeExternalAccess()
    {
        foreach (var options in new[] { new SchemaCompileOptions(), new() { Resolver = new Resolver((_, _) => null) } })
        {
            var context = new SchemaCompileContext(options);
            Assert.Equal("SCHEMA.ResourceDenied", Assert.Throws<SchemaCompileException>(() => context.Resources.Load(Reference(), context.Operation)).Diagnostics[0].Code);
        }
        int calls = 0; var missing = new SchemaCompileContext(new() { Resolver = new Resolver((_, _) => { calls++; return null; }) });
        Assert.Throws<SchemaCompileException>(() => missing.Resources.Load(new("relative.xsd", null, SchemaResourcePurpose.RelaxNgInclude, default), missing.Operation)); Assert.Equal(0, calls);
    }
    [Theory]
    [InlineData("<schema>", "SCHEMA.Syntax")]
    [InlineData("<!DOCTYPE schema SYSTEM 'entity'><schema/>", "SCHEMA.Syntax")]
    public void StrictParserOwnsSyntaxDiagnosticsAtFinalSourceAndDisposes(string xml, string code)
    {
        var stream = new ModelTests.CountStream(xml); var final = new Uri("urn:test:final");
        var context = new SchemaCompileContext(new() { Resolver = new Resolver((_, _) => new(stream, final)) });
        var error = Assert.Throws<SchemaCompileException>(() => context.Resources.Load(Reference(), context.Operation));
        Assert.Equal(code, error.Diagnostics[0].Code); Assert.Equal(final, error.Diagnostics[0].Location.SourceUri);
        Assert.True(error.Diagnostics[0].Location.LineNumber > 0); Assert.NotNull(error.Diagnostics[0].Location.ByteOffset); Assert.Equal(1, stream.Disposals);
    }
    [Fact]
    public void BytesAndResourcesAggregateExactlyAtCapAndDepthScopesBalance()
    {
        var streams = new List<ModelTests.CountStream>(); int calls = 0;
        var context = new SchemaCompileContext(new() { Limits = new() { ExternalBytes = 8, ExternalResources = 3, InclusionDepth = 2 }, Resolver = new Resolver((_, _) => { calls++; var stream = new ModelTests.CountStream("<r/>"); streams.Add(stream); return new(stream, new Uri("urn:resource:" + calls)); }) });
        context.Resources.Load(Reference(), context.Operation); context.Resources.Load(Reference(), context.Operation);
        var error = Assert.Throws<SchemaCompileException>(() => context.Resources.Load(Reference(), context.Operation)); Assert.Equal("SCHEMA.Limit", error.Diagnostics[0].Code); Assert.Equal(new Uri("urn:resource:3"), error.Diagnostics[0].Location.SourceUri); Assert.Null(error.Diagnostics[0].Location.ByteOffset); Assert.All(streams, stream => Assert.Equal(1, stream.Disposals));
        int count = 0; var resources = new SchemaCompileContext(new() { Limits = new() { ExternalResources = 1 }, Resolver = new Resolver((_, _) => { count++; return new(new ModelTests.CountStream("<r/>"), new("urn:r")); }) });
        resources.Resources.Load(Reference(), resources.Operation); Assert.Throws<SchemaCompileException>(() => resources.Resources.Load(Reference(), resources.Operation)); Assert.Equal(1, count);
        var depth = new SchemaCompileContext(new() { Limits = new() { InclusionDepth = 1 }, Resolver = new Resolver((_, _) => throw new Exception("not called")) });
        using (depth.Resources.EnterComposition(At)) Assert.Throws<SchemaCompileException>(() => depth.Resources.Load(Reference(), depth.Operation));
        var balanced = new SchemaCompileContext(new() { Limits = new() { InclusionDepth = 1 } });
        var scope = balanced.Resources.EnterComposition(At); scope.Dispose(); scope.Dispose(); using var next = balanced.Resources.EnterComposition(At);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExpandedCharactersAggregateAcrossPrincipalAndExternalSchemas(bool principal)
    {
        int calls = 0;
        var context = new SchemaCompileContext(new() { Limits = new() { ExpandedCharacters = 2 }, Resolver = new Resolver((_, _) => new(new ModelTests.CountStream("<r>&amp;</r>"), new Uri("urn:expanded:" + ++calls))) });
        if (principal) context.Parse("<r>&#38;</r>"); else context.Resources.Load(Reference(), context.Operation);
        var exact = context.Resources.Load(Reference(), context.Operation); Assert.Equal("&", exact.RootElement.GetTextContent());
        context.Parse("<r>literal</r>"); // At-cap allowance still permits nonexpanding input.
        var error = Assert.Throws<SchemaCompileException>(() => context.Resources.Load(Reference(), context.Operation));
        Assert.Equal("SCHEMA.Limit", error.Diagnostics[^1].Code); Assert.False(context.Operation.IsComplete);
        var one = new SchemaCompileContext(new() { Limits = new() { ExpandedCharacters = 1 }, Resolver = new Resolver((_, _) => new(new ModelTests.CountStream("<r>&amp;</r>"), new Uri("urn:one"))) });
        one.Parse("<r>&amp;</r>"); Assert.Throws<SchemaCompileException>(() => one.Resources.Load(Reference(), one.Operation));
    }

    [Fact]
    public void ResolverReadAndCancellationFailuresPropagateUnchangedAndDispose()
    {
        var caller = new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Error, "caller", At)]);
        var context = new SchemaCompileContext(new() { Resolver = new Resolver((_, _) => throw caller) });
        Assert.Same(caller, Assert.Throws<XmlParseException>(() => context.Resources.Load(Reference(), context.Operation)));
        var stream = new ModelTests.FailingStream(caller); var reading = new SchemaCompileContext(new() { Resolver = new Resolver((_, _) => new(stream, new("urn:r"))) });
        Assert.Same(caller, Assert.Throws<XmlParseException>(() => reading.Resources.Load(Reference(), reading.Operation))); Assert.Equal(1, stream.Disposals);
        using var canceled = new CancellationTokenSource(); var transferred = new ModelTests.CountStream("<r/>");
        var cancellation = new SchemaCompileContext(new() { CancellationToken = canceled.Token, Resolver = new Resolver((_, _) => { canceled.Cancel(); return new(transferred, new("urn:r")); }) });
        Assert.Throws<OperationCanceledException>(() => cancellation.Resources.Load(Reference(), cancellation.Operation)); Assert.Equal(1, transferred.Disposals);
    }
    internal sealed class Resolver(Func<XmlResourceRequest, CancellationToken, XmlResolvedResource?> resolve) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => resolve(request, cancellationToken); }
}

using System.Runtime.InteropServices;
using Xemele.Resolution.Core;
using Xunit;

namespace Xemele.Tests.SchemaResources;

public sealed class SchemaResourceKindTests
{
    private static readonly Uri Requested = new("https://fixture.invalid/schema.xsd");
    private static readonly Uri Referrer = new("https://fixture.invalid/root.xsd");
    private static readonly Uri Final = new("https://redirect.invalid/schema.xsd");
    private static readonly XmlLocation Location = new(Referrer, 1, 1, 0);
    private static XmlResourceRequest Request =>
        new(Requested, Referrer, XmlResourceKind.Schema) { SystemId = "schema.xsd", PublicId = null };

    [Fact]
    public void ExactEnumValuesAndActualRuntime()
    {
        Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);
        Assert.Equal(new[] { "ExternalSubset", "ExternalEntity", "Catalog", "XIncludeXml", "XIncludeText", "Schema" },
            Enum.GetNames<XmlResourceKind>());
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, Enum.GetValues<XmlResourceKind>().Select(kind => (int)kind));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SchemaRequestForwardingAndExactlyOnceOwnership(bool asynchronous)
    {
        var request = Request;
        var stream = new OwnedStream();
        var resource = new XmlResolvedResource(stream, Final);
        var resolver = new RecordingResolver(resource);
        var token = TestContext.Current.CancellationToken;
        var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), token);
        var opened = asynchronous ? await session.OpenAsync(request, Location) : session.Open(request, Location);
        Assert.Null(opened.Diagnostic);
        Assert.NotNull(opened.Resource);
        Assert.Same(request, resolver.Request);
        Assert.Equal(token, resolver.Token);
        Assert.Equal(asynchronous ? 0 : 1, resolver.SyncCalls);
        Assert.Equal(asynchronous ? 1 : 0, resolver.AsyncCalls);
        Assert.Same(Final, opened.Resource.SourceUri);
        Assert.True(stream.CanRead);
        Assert.Equal(1, session.ResourcesOpened);
        if (asynchronous)
        {
            await session.DisposeAsync();
            await opened.Resource.DisposeAsync();
            resource.Dispose();
        }
        else
        {
            session.Dispose();
            opened.Resource.Dispose();
            await resource.DisposeAsync();
        }
        session.Dispose();
        Assert.Equal(asynchronous ? 0 : 1, stream.SyncDisposals);
        Assert.Equal(asynchronous ? 1 : 0, stream.AsyncDisposals);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SchemaRefusalRequiresExplicitResolverSuccess(bool asynchronous, bool absentResolver)
    {
        var request = Request;
        var resolver = new RecordingResolver(null);
        var token = TestContext.Current.CancellationToken;
        await using var session = new XmlResolutionSession(absentResolver ? null : resolver, new XmlResourceLimits(), token);
        var opened = asynchronous ? await session.OpenAsync(request, Location) : session.Open(request, Location);
        Assert.Null(opened.Resource);
        Assert.Equal("XML.ResourceDenied", opened.Diagnostic!.Code);
        Assert.Equal(Location, opened.Diagnostic.Location);
        Assert.Equal(0, session.ResourcesOpened);
        Assert.Equal(!absentResolver && !asynchronous ? 1 : 0, resolver.SyncCalls);
        Assert.Equal(!absentResolver && asynchronous ? 1 : 0, resolver.AsyncCalls);
        if (!absentResolver)
        {
            Assert.Same(request, resolver.Request);
            Assert.Equal(token, resolver.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCanAuthorizeSchemaWithoutAuthorizingEntitiesOrIncludes(bool asynchronous)
    {
        var stream = new OwnedStream();
        var resolver = new SchemaOnlyResolver(new(stream, Final));
        await using var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), TestContext.Current.CancellationToken);
        foreach (var kind in new[] { XmlResourceKind.ExternalSubset, XmlResourceKind.ExternalEntity,
            XmlResourceKind.Catalog, XmlResourceKind.XIncludeXml, XmlResourceKind.XIncludeText, XmlResourceKind.Schema })
        {
            var request = Request with { Kind = kind };
            var opened = asynchronous ? await session.OpenAsync(request, Location) : session.Open(request, Location);
            if (kind == XmlResourceKind.Schema)
            {
                Assert.Null(opened.Diagnostic);
                Assert.NotNull(opened.Resource);
            }
            else
            {
                Assert.Null(opened.Resource);
                Assert.Equal("XML.ResourceDenied", opened.Diagnostic!.Code);
            }
        }
        Assert.Equal(1, session.ResourcesOpened);
        Assert.Equal(6, resolver.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogUsesSchemaUriAndPreservesPurposeAndLexicalIds(bool asynchronous)
    {
        var mapped = new Uri("https://mapped.invalid/schema.xsd");
        var catalog = XmlCatalog.Parse("""
            <catalog xmlns="urn:oasis:names:tc:entity:xmlns:xml:catalog">
              <system systemId="schema.xsd" uri="https://entity.invalid/system"/>
              <public publicId="schema public" uri="https://entity.invalid/public"/>
              <uri name="https://fixture.invalid/schema.xsd" uri="https://mapped.invalid/schema.xsd"/>
            </catalog>
            """, cancellationToken: TestContext.Current.CancellationToken);
        var request = Request with { PublicId = "schema public" };
        Assert.Equal(mapped, catalog.Resolve(request, TestContext.Current.CancellationToken));
        Assert.Equal("https://entity.invalid/system", catalog.Resolve(request with { Kind = XmlResourceKind.ExternalEntity }, TestContext.Current.CancellationToken)!.AbsoluteUri);
        var stream = new OwnedStream();
        var resource = new XmlResolvedResource(stream, Final);
        var fallback = new RecordingResolver(resource);
        var resolver = new CatalogResolver(catalog, fallback);
        var token = TestContext.Current.CancellationToken;
        var returned = asynchronous ? await resolver.ResolveAsync(request, token) : resolver.Resolve(request, token);
        Assert.Same(resource, returned);
        Assert.Equal(request with { Uri = mapped }, fallback.Request);
        Assert.Equal(token, fallback.Token);
        Assert.Equal(asynchronous ? 0 : 1, fallback.SyncCalls);
        Assert.Equal(asynchronous ? 1 : 0, fallback.AsyncCalls);
        Assert.True(stream.CanRead);
        await resource.DisposeAsync();
        resource.Dispose();
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(0, stream.SyncDisposals);
    }

    [Fact]
    public async Task DefaultAsyncResolverFallbackPreservesSchemaRequest()
    {
        var request = Request;
        var resolver = new SchemaOnlyResolver(null);
        Assert.Null(await resolver.ResolveAsync(request, TestContext.Current.CancellationToken));
        Assert.Same(request, resolver.Request);
        Assert.Equal(1, resolver.Calls);
    }

    private sealed class RecordingResolver(XmlResolvedResource? resource) : XmlResourceResolver
    {
        internal XmlResourceRequest? Request;
        internal CancellationToken Token;
        internal int SyncCalls, AsyncCalls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Request = request; Token = cancellationToken; SyncCalls++; return resource;
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Request = request; Token = cancellationToken; AsyncCalls++;
            await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return resource;
        }
    }

    private sealed class SchemaOnlyResolver(XmlResolvedResource? resource) : XmlResourceResolver
    {
        internal XmlResourceRequest? Request;
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request; Calls++;
            return request.Kind == XmlResourceKind.Schema ? resource : null;
        }
    }

    private sealed class OwnedStream : MemoryStream
    {
        internal int SyncDisposals, AsyncDisposals;
        protected override void Dispose(bool disposing)
        {
            if (disposing) SyncDisposals++;
            base.Dispose(disposing);
        }
        public override ValueTask DisposeAsync()
        {
            AsyncDisposals++; base.Dispose(false); return default;
        }
    }
}

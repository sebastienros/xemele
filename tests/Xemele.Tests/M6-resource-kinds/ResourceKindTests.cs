using System.Runtime.InteropServices;
using Xemele.Resolution.Core;
using Xunit;

namespace Xemele.Tests.ResourceKinds;

public sealed class ResourceKindTests
{
    private static readonly Uri Requested = new("https://fixture.invalid/request");
    private static readonly Uri Final = new("https://fixture.invalid/final");
    private static readonly XmlLocation Location = new(Requested, 1, 1, 0);
    private static XmlResourceRequest Request(XmlResourceKind kind) =>
        new(Requested, Final, kind) { SystemId = "../lexical", PublicId = "public identifier" };
    public static TheoryData<XmlResourceKind, int> Kinds => new()
    {
        { XmlResourceKind.ExternalSubset, 0 }, { XmlResourceKind.ExternalEntity, 1 },
        { XmlResourceKind.Catalog, 2 }, { XmlResourceKind.XIncludeXml, 3 }, { XmlResourceKind.XIncludeText, 4 }
    };

    [Fact]
    public void ActualRuntimeIsPinned() => Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);

    [Theory]
    [MemberData(nameof(Kinds))]
    public void SyncForwardingAndExactlyOnceOwnership(XmlResourceKind kind, int value)
    {
        Assert.Equal(value, (int)kind);
        var request = Request(kind);
        var stream = new OwnedStream();
        var resolver = new RecordingResolver(new(stream, Final));
        var token = TestContext.Current.CancellationToken;
        var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), token);
        var opened = session.Open(request, Location);
        Assert.Null(opened.Diagnostic);
        Assert.NotNull(opened.Resource);
        Assert.Same(Final, opened.Resource.SourceUri);
        Assert.Same(request, resolver.Request);
        Assert.Equal(token, resolver.Token);
        Assert.Equal(1, resolver.SyncCalls);
        Assert.Equal(0, resolver.AsyncCalls);
        Assert.Equal(1, session.ResourcesOpened);
        session.Dispose(); opened.Resource.Dispose(); session.Dispose();
        Assert.Equal(1, stream.SyncDisposals);
        Assert.Equal(0, stream.AsyncDisposals);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task AsyncForwardingAndExactlyOnceOwnership(XmlResourceKind kind, int value)
    {
        Assert.Equal(value, (int)kind);
        var request = Request(kind);
        var stream = new OwnedStream();
        var resolver = new RecordingResolver(new(stream, Final));
        var token = TestContext.Current.CancellationToken;
        var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), token);
        var opened = await session.OpenAsync(request, Location);
        Assert.Null(opened.Diagnostic);
        Assert.NotNull(opened.Resource);
        Assert.Same(Final, opened.Resource.SourceUri);
        Assert.Same(request, resolver.Request);
        Assert.Equal(token, resolver.Token);
        Assert.Equal(0, resolver.SyncCalls);
        Assert.Equal(1, resolver.AsyncCalls);
        Assert.Equal(1, session.ResourcesOpened);
        await session.DisposeAsync(); await opened.Resource.DisposeAsync(); session.Dispose();
        Assert.Equal(0, stream.SyncDisposals);
        Assert.Equal(1, stream.AsyncDisposals);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task RefusalIsStructuredWithoutAcquisition(XmlResourceKind kind, int value)
    {
        Assert.Equal(value, (int)kind);
        var request = Request(kind);
        var resolver = new RecordingResolver(null);
        var token = TestContext.Current.CancellationToken;
        using (var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), token))
        {
            var result = session.Open(request, Location);
            Assert.Null(result.Resource);
            Assert.Equal("XML.ResourceDenied", result.Diagnostic!.Code);
            Assert.Equal(Location, result.Diagnostic.Location);
            Assert.Equal(0, session.ResourcesOpened);
        }
        await using (var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), token))
        {
            var result = await session.OpenAsync(request, Location);
            Assert.Null(result.Resource);
            Assert.Equal("XML.ResourceDenied", result.Diagnostic!.Code);
            Assert.Equal(Location, result.Diagnostic.Location);
            Assert.Equal(0, session.ResourcesOpened);
        }
        Assert.Same(request, resolver.Request);
        Assert.Equal(token, resolver.Token);
        Assert.Equal(1, resolver.SyncCalls);
        Assert.Equal(1, resolver.AsyncCalls);
        await using var absent = new XmlResolutionSession(null, new XmlResourceLimits(), token);
        Assert.Equal("XML.ResourceDenied", absent.Open(request, Location).Diagnostic!.Code);
        Assert.Equal("XML.ResourceDenied", (await absent.OpenAsync(request, Location)).Diagnostic!.Code);
        Assert.Equal(0, absent.ResourcesOpened);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public async Task UndefinedKindsFailBeforeResolver(int value)
    {
        var resolver = new RecordingResolver(null);
        var token = TestContext.Current.CancellationToken;
        using (var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), token))
            Assert.Throws<ArgumentException>(() => session.Open(Request((XmlResourceKind)value), Location));
        await using (var session = new XmlResolutionSession(resolver, new XmlResourceLimits(), token))
            await Assert.ThrowsAsync<ArgumentException>(async () => await session.OpenAsync(Request((XmlResourceKind)value), Location));
        Assert.Equal(0, resolver.SyncCalls);
        Assert.Equal(0, resolver.AsyncCalls);
    }

    private sealed class RecordingResolver(XmlResolvedResource? result) : XmlResourceResolver
    {
        internal XmlResourceRequest? Request;
        internal CancellationToken Token;
        internal int SyncCalls, AsyncCalls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Request = request; Token = cancellationToken; SyncCalls++; return result;
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Request = request; Token = cancellationToken; AsyncCalls++;
            await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return result;
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

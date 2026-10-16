using Xemele.Internal.Operations;
using Xemele.Resolution.Core;
using Xunit;
namespace Xemele.Tests.Resolution;

public sealed class ResolverTests
{
    private static readonly Uri Source = new("https://fixture.invalid/final/root.dtd");
    private static readonly XmlLocation Location = new(Source, 3, 7, 20);
    private static XmlResourceRequest Request(string uri = "https://fixture.invalid/original") => new(new(uri), Source, XmlResourceKind.ExternalSubset);
    private static XmlResolutionSession Session(XmlResourceResolver? resolver, long? bytes = 12, int? count = 8) =>
        new(resolver, new XmlResourceLimits { ExternalBytes = bytes, ExternalResources = count }, TestContext.Current.CancellationToken);
    private static XmlResolutionSession SessionWithToken(XmlResourceResolver? resolver, CancellationToken token, long? bytes = 12) =>
        new(resolver, new XmlResourceLimits { ExternalBytes = bytes }, token);

    [Fact]
    public async Task PublicShapeAndOwnership()
    {
        var stream = new CountingStream([1]);
        var resource = new XmlResolvedResource(stream, Source);
        Assert.Same(stream, resource.Content);
        Assert.Same(Source, resource.SourceUri);
        resource.Dispose();
        resource.Dispose();
        await resource.DisposeAsync();
        Assert.Equal(1, stream.SyncDisposals);
        Assert.Equal(0, stream.AsyncDisposals);
        Assert.Throws<ArgumentNullException>(() => new XmlResolvedResource(null!, Source));
        Assert.Throws<ArgumentNullException>(() => new XmlResolvedResource(stream, null!));
        Assert.Throws<ArgumentException>(() => new XmlResolvedResource(new MemoryStream(), new("relative", UriKind.Relative)));
        Assert.Throws<ArgumentException>(() => new XmlResolvedResource(stream, Source));
    }
    [Fact]
    public async Task AsyncOwnershipIsExactlyOnceEvenWhenMixed()
    {
        var stream = new CountingStream([]);
        var resource = new XmlResolvedResource(stream, Source);
        await resource.DisposeAsync();
        await resource.DisposeAsync();
        resource.Dispose();
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(0, stream.SyncDisposals);
    }
    [Fact]
    public async Task ConcurrentPublicDisposalClaimsOwnershipOnce()
    {
        var stream = new CountingStream([]);
        var resource = new XmlResolvedResource(stream, Source);
        await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(resource.Dispose, TestContext.Current.CancellationToken)));
        Assert.Equal(1, stream.SyncDisposals);
    }
    [Theory]
    [InlineData("file:///does/not/exist.dtd")]
    [InlineData("https://fixture.invalid/unreachable.dtd")]
    public void NullResolverNeverReadsAmbientSources(string uri)
    {
        using var session = Session(null);
        var result = session.Open(Request(uri), Location);
        Assert.Null(result.Resource);
        Assert.Equal("XML.ResourceDenied", result.Diagnostic!.Code);
        Assert.Equal(Location, result.Diagnostic.Location);
        Assert.Equal(0, session.ResourcesOpened);
    }
    [Fact]
    public void ExplicitRefusalDoesNotCountOrFetch()
    {
        var resolver = new DelegateResolver(_ => null);
        using var session = Session(resolver);
        Assert.Equal("XML.ResourceDenied", session.Open(Request(), Location).Diagnostic!.Code);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(0, session.ResourcesOpened);
    }
    [Fact]
    public void RelativeRequestUsesFinalDeclarationUriAndPreservesLexicalIds()
    {
        var result = XmlResolutionSession.CreateRequest("../entity.xml", "public-id", Source, XmlResourceKind.ExternalEntity, Location);
        Assert.Null(result.Diagnostic);
        Assert.Equal(new Uri("https://fixture.invalid/entity.xml"), result.Request!.Uri);
        Assert.Equal(Source, result.Request.Referrer);
        Assert.Equal("../entity.xml", result.Request.SystemId);
        Assert.Equal("public-id", result.Request.PublicId);
        Assert.Equal("XML.ResourceDenied", XmlResolutionSession.CreateRequest("relative", null, null, XmlResourceKind.ExternalEntity, Location).Diagnostic!.Code);
        Assert.Null(XmlResolutionSession.CreateRequest("urn:fixture:entity", null, null, XmlResourceKind.ExternalEntity, Location).Diagnostic);
        Assert.Throws<ArgumentException>(() => XmlResolutionSession.CreateRequest("a", null, new("b", UriKind.Relative), XmlResourceKind.ExternalEntity, Location));
    }
    [Fact]
    public void RequestValidationHappensBeforeResolver()
    {
        var resolver = new DelegateResolver(_ => null);
        foreach (var request in new XmlResourceRequest[] {
            new(new("a", UriKind.Relative), null, XmlResourceKind.ExternalEntity),
            new(Source, new("a", UriKind.Relative), XmlResourceKind.ExternalEntity),
            new(Source, null, (XmlResourceKind)99) })
        {
            using var session = Session(resolver);
            Assert.Throws<ArgumentException>(() => session.Open(request, Location));
        }
        Assert.Equal(0, resolver.Calls);
    }
    [Fact]
    public void ExactBytesRequireOnlySingleByteEofProbe()
    {
        var stream = new CountingStream([1, 2, 3]);
        using var session = Session(new DelegateResolver(_ => new(stream, Source)), 3);
        using var handle = session.Open(Request(), Location).Resource!;
        var buffer = new byte[10000];
        Assert.Equal(3, handle.Read(buffer, Location).Count);
        Assert.Equal(0, handle.Read(buffer, Location).Count);
        Assert.Equal(new[] { 3, 1 }, stream.Requested);
        Assert.Equal(3, session.BytesRead);
    }
    [Fact]
    public void OneOverBytesIsChargedWithoutExposingOrReadingMore()
    {
        var stream = new CountingStream([1, 2, 3, 4, 5]);
        using var session = Session(new DelegateResolver(_ => new(stream, Source)), 3);
        using var handle = session.Open(Request(), Location).Resource!;
        var buffer = new byte[8];
        Assert.Equal(3, handle.Read(buffer, Location).Count);
        var over = handle.Read(buffer, Location);
        Assert.Equal(0, over.Count);
        Assert.Equal("XML.ExternalBytesLimit", over.Diagnostic!.Code);
        Assert.Equal(Source, over.Diagnostic.Location.SourceUri);
        Assert.Equal(4, session.BytesRead);
        Assert.Equal(over, handle.Read(buffer, Location));
        Assert.Equal(new[] { 3, 1 }, stream.Requested);
    }
    [Fact]
    public void NestedHandlesShareByteAndCountLedger()
    {
        var streams = new[] { new CountingStream([1, 2]), new CountingStream([3, 4, 5]) };
        var i = 0;
        var budget = new OperationBudget(new()
        {
            Limits = new()
            {
                ExternalBytes = 4,
                ExternalResources = 2
            }
        }, default);
        using var session = new XmlResolutionSession(new DelegateResolver(_ => new(streams[i], new Uri($"urn:fixture:{i++}"))), budget, default);
        using var first = session.Open(Request(), Location).Resource!;
        using var second = session.Open(Request(), Location).Resource!;
        var buffer = new byte[9];
        Assert.Equal(2, first.Read(buffer, Location).Count);
        Assert.Equal(2, second.Read(buffer, Location).Count);
        Assert.Equal("XML.ExternalBytesLimit", second.Read(buffer, Location).Diagnostic!.Code);
        Assert.Equal(4, budget.Count(Quota.ExternalBytes));
        Assert.Equal(5, session.BytesRead);
        Assert.Equal(2, budget.Count(Quota.ExternalResources));
    }
    [Fact]
    public void ExhaustedCountDoesNotInvokeResolver()
    {
        var stream = new CountingStream([]);
        var resolver = new DelegateResolver(_ => new(stream, Source));
        using var session = Session(resolver, count: 1);
        session.Open(Request(), Location).Resource!.Dispose();
        Assert.Equal("XML.ExternalResourcesLimit", session.Open(Request(), Location).Diagnostic!.Code);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(1, session.ResourcesOpened);
    }
    [Fact]
    public void ActiveFinalUriAliasesAndFragmentsFormCyclesButInactiveRepeatsAreAllowed()
    {
        var streams = new List<CountingStream>();
        var resolver = new DelegateResolver(_ => { var stream = new CountingStream([]); streams.Add(stream); return new(stream, new Uri(Source.AbsoluteUri + "#" + streams.Count)); });
        using var session = Session(resolver);
        var parent = session.Open(Request(), Location).Resource!;
        var cycle = session.Open(Request("https://fixture.invalid/alias"), Location);
        Assert.Equal("XML.ResourceCycle", cycle.Diagnostic!.Code);
        Assert.Equal(1, streams[1].SyncDisposals);
        Assert.Equal(2, session.ResourcesOpened);
        parent.Dispose();
        using var repeated = session.Open(Request(), Location).Resource!;
        Assert.Equal(3, session.ResourcesOpened);
    }
    [Fact]
    public void SourceOwnershipRestoresParentWithoutGlobalCurrentUri()
    {
        var i = 0;
        using var session = Session(new DelegateResolver(_ => new(new MemoryStream([]), new Uri($"https://fixture.invalid/final/{i++}/x"))));
        using var parent = session.Open(Request(), Location).Resource!;
        var request = XmlResolutionSession.CreateRequest("child", null, parent.SourceUri, XmlResourceKind.ExternalEntity, Location).Request!;
        using (var child = session.Open(request, Location).Resource!)
        {
            Assert.NotEqual(child.SourceUri, parent.SourceUri);
        }

        Assert.Equal(new Uri("https://fixture.invalid/final/0/child"), request.Uri);
        Assert.Equal(new Uri("https://fixture.invalid/final/0/x"), parent.SourceUri);
    }
    [Fact]
    public void StreamsAreReadableOnAcquisitionAndInvalidReturnIsDisposed()
    {
        var stream = new CountingStream([]);
        var resource = new XmlResolvedResource(stream, Source);
        stream.SetUnreadable();
        using var session = Session(new DelegateResolver(_ => resource));
        Assert.Equal("XML.ResourceDenied", session.Open(Request(), Location).Diagnostic!.Code);
        Assert.Equal(1, stream.SyncDisposals);
        Assert.Equal(1, session.ResourcesOpened);
    }
    [Fact]
    public void ResolverExceptionsIncludingParseExceptionsPropagateByIdentity()
    {
        foreach (var error in new Exception[] { new IOException("fixture"), new XmlParseException([new("user", XmlDiagnosticSeverity.Fatal, "user", Location)]) })
        {
            using var session = Session(new DelegateResolver(_ => throw error));
            Assert.Same(error, Assert.ThrowsAny<Exception>(() => session.Open(Request(), Location)));
        }
    }
    [Fact]
    public void CancellationAtEntryAndAfterUncooperativeSyncResolverDisposesReturn()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => SessionWithToken(null, token: cancellation.Token));
        using var later = new CancellationTokenSource();
        var stream = new CountingStream([]);
        var resolver = new DelegateResolver(_ => { later.Cancel(); return new(stream, Source); });
        using var session = SessionWithToken(resolver, token: later.Token);
        Assert.Throws<OperationCanceledException>(() => session.Open(Request(), Location));
        Assert.Equal(1, stream.SyncDisposals);
        Assert.Equal(1, session.ResourcesOpened);
    }
    [Fact]
    public void CancellationBeforeReadAndEvery4096Bytes()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new CountingStream(new byte[10000]);
        using var session = SessionWithToken(new DelegateResolver(_ => new(stream, Source)), bytes: null, token: cancellation.Token);
        using var handle = session.Open(Request(), Location).Resource!;
        var buffer = new byte[10000];
        Assert.Equal(4096, handle.Read(buffer, Location).Count);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => handle.Read(buffer, Location));
        Assert.Single(stream.Requested);
        Assert.Equal(1, stream.SyncDisposals);
    }
    [Fact]
    public void NullLimitsDisableOnlyQuotasAndEmptyReadsDoNoIo()
    {
        using var session = Session(new DelegateResolver(_ => new(new MemoryStream([1]), Source)), null, null);
        using var handle = session.Open(Request(), Location).Resource!;
        Assert.Equal(default, handle.Read([], Location));
        Assert.Equal(1, handle.Read(new byte[1], Location).Count);
    }
    [Fact]
    public void IoExceptionDisposesOwnedResourceAndDoesNotBecomeRejection()
    {
        var error = new IOException("fixture");
        var stream = new CountingStream([1]) { ReadFailure = error };
        using var session = Session(new DelegateResolver(_ => new(stream, Source)));
        var handle = session.Open(Request(), Location).Resource!;
        Assert.Same(error, Assert.Throws<IOException>(() => handle.Read(new byte[1], Location)));
        Assert.Equal(1, stream.SyncDisposals);
        handle.Dispose();
        Assert.Equal(1, stream.SyncDisposals);
    }
    [Fact]
    public void SessionDisposesAllHandlesEvenIfOneDisposalThrows()
    {
        var error = new IOException("dispose");
        var streams = new[] { new CountingStream([]) { DisposeFailure = error }, new CountingStream([]) };
        var i = 0;
        var session = Session(new DelegateResolver(_ => new(streams[i], new Uri($"urn:fixture:{i++}"))));
        session.Open(Request(), Location);
        session.Open(Request(), Location);
        Assert.Same(error, Assert.Throws<IOException>(session.Dispose));
        session.Dispose();
        Assert.All(streams, stream => Assert.Equal(1, stream.SyncDisposals));
    }
    [Fact]
    public void SecondaryCleanupFailurePreservesReadException()
    {
        var error = new IOException("read");
        var stream = new CountingStream([1]) { ReadFailure = error, DisposeFailure = new IOException("cleanup") };
        using var session = Session(new DelegateResolver(_ => new(stream, Source)));
        var handle = session.Open(Request(), Location).Resource!;
        Assert.Same(error, Assert.Throws<IOException>(() => handle.Read(new byte[1], Location)));
        Assert.Equal(1, stream.SyncDisposals);
    }
    [Fact]
    public async Task SecondaryAsyncCleanupFailurePreservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new CountingStream([]) { DisposeFailure = new IOException("cleanup") };
        var resolver = new AsyncResolver(_ => { cancellation.Cancel(); return Task.FromResult<XmlResolvedResource?>(new(stream, Source)); });
        await using var session = SessionWithToken(resolver, cancellation.Token);
        var error = await Assert.ThrowsAsync<OperationCanceledException>(async () => await session.OpenAsync(Request(), Location));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, stream.AsyncDisposals);
    }
    [Fact]
    public void SessionRejectsResolverReentrancyAndDisposedWork()
    {
        XmlResolutionSession session = null!;
        session = Session(new DelegateResolver(_ => { Assert.Throws<InvalidOperationException>(() => session.Open(Request(), Location)); return null; }));
        session.Open(Request(), Location);
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Open(Request(), Location));
    }
    [Fact]
    public async Task DefaultAsyncFallbackChecksCancellationWithoutTaskRun()
    {
        var thread = Environment.CurrentManagedThreadId;
        var resolver = new DelegateResolver(_ => { Assert.Equal(thread, Environment.CurrentManagedThreadId); return null; });
        await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken);
        Assert.Equal(1, resolver.Calls);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await resolver.ResolveAsync(Request(), cancellation.Token));
        Assert.Equal(1, resolver.Calls);
    }
    [Fact]
    public async Task GenuineAsyncOverrideAndAsyncReadsAndDisposal()
    {
        var stream = new CountingStream([1, 2, 3]);
        var resolver = new AsyncResolver(async token => { await Task.Yield(); token.ThrowIfCancellationRequested(); return new(stream, Source); });
        await using var session = Session(resolver, bytes: 3);
        await using var handle = (await session.OpenAsync(Request(), Location)).Resource!;
        var buffer = new byte[10000];
        Assert.Equal(3, (await handle.ReadAsync(buffer, Location)).Count);
        Assert.Equal(0, (await handle.ReadAsync(buffer, Location)).Count);
        Assert.Equal(2, stream.AsyncReads);
        Assert.Equal(0, resolver.SyncCalls);
        await handle.DisposeAsync();
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(0, stream.SyncDisposals);
    }
    [Fact]
    public async Task UncooperativeAsyncResolverReturnIsDisposedAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new CountingStream([]);
        var resolver = new AsyncResolver(async _ => { await Task.Yield(); cancellation.Cancel(); return new(stream, Source); });
        await using var session = SessionWithToken(resolver, token: cancellation.Token);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await session.OpenAsync(Request(), Location));
        Assert.Equal(1, stream.AsyncDisposals);
        Assert.Equal(0, stream.SyncDisposals);
        Assert.Equal(1, session.ResourcesOpened);
    }
    [Fact]
    public async Task AsyncExceptionsAndCycleCleanupUseAsyncOwnership()
    {
        var error = new IOException("async");
        await using (var failing = Session(new AsyncResolver(_ => throw error)))
        {
            Assert.Same(error, await Assert.ThrowsAsync<IOException>(async () => await failing.OpenAsync(Request(), Location)));
        }

        var streams = new List<CountingStream>();
        await using var session = Session(new AsyncResolver(_ => { var stream = new CountingStream([]); streams.Add(stream); return Task.FromResult<XmlResolvedResource?>(new(stream, Source)); }));
        await session.OpenAsync(Request(), Location);
        Assert.Equal("XML.ResourceCycle", (await session.OpenAsync(Request(), Location)).Diagnostic!.Code);
        Assert.Equal(1, streams[1].AsyncDisposals);
        await session.DisposeAsync();
        Assert.Equal(1, streams[0].AsyncDisposals);
    }
    [Fact]
    public async Task UncooperativeAsyncReadIsChargedThenCancelledAndDisposed()
    {
        using var cancellation = new CancellationTokenSource();
        var stream = new CountingStream([1]) { OnRead = cancellation.Cancel };
        await using var session = SessionWithToken(new DelegateResolver(_ => new(stream, Source)), token: cancellation.Token);
        var handle = (await session.OpenAsync(Request(), Location)).Resource!;
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await handle.ReadAsync(new byte[1], Location));
        Assert.Equal(1, session.BytesRead);
        Assert.Equal(1, stream.AsyncDisposals);
    }
    [Fact]
    public void WarmBorrowedReadsAllocateNothing()
    {
        var child = ResolverAllocationChild.EnterChild();
        if (child is null)
        {
            var pair = ResolverAllocationChild.RunPair();
            Assert.Equal(0, pair.Real.AllocatedBytes);
            Assert.Equal(640000, pair.Positive.AllocatedBytes);
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"real={pair.Real.AllocatedBytes}; positive={pair.Positive.AllocatedBytes}; warm1000={pair.Real.WarmBytes}; measured10000={pair.Real.MeasuredBytes}; total={pair.Real.TotalBytes}; artifacts={pair.Directory}");
            return;
        }
        var cancellation = TestContext.Current.CancellationToken;
        var stream = new ZeroStream();
        var resolver = new DelegateResolver(_ => new(stream, Source));
        using var session = Session(resolver, bytes: null);
        var opened = session.Open(Request(), Location);
        Assert.Null(opened.Diagnostic);
        using var handle = opened.Resource!;
        Assert.Same(stream, handle.Content);
        Assert.Same(Source, handle.SourceUri);
        var buffer = new byte[4096];
        var warm = ResolverAllocationProbe.ReadChecked(session, handle, buffer, Location, cancellation, 1000);
        var warmBytes = session.BytesRead;
        var allocated = ResolverAllocationProbe.ReadChecked(session, handle, buffer, Location, cancellation, 10000, child.Mode == "positive");
        var measuredBytes = session.BytesRead - warmBytes;
        Assert.Equal(4096000, warmBytes);
        Assert.Equal(40960000, measuredBytes);
        Assert.Equal(45056000, session.BytesRead);
        Assert.Equal(1, session.ResourcesOpened);
        Assert.Equal(1, resolver.Calls);
        Assert.Same(stream, handle.Content);
        Assert.Same(Source, handle.SourceUri);
        ResolverAllocationChild.Write(child, warm, allocated, warmBytes, measuredBytes, session.BytesRead,
            session.ResourcesOpened, resolver.Calls, ResolverAllocationProbe.EscapedLength);
        if (child.Mode == "positive")
        {
            Assert.Equal(640000, allocated);
        }
        else
        {
            Assert.Equal(0, allocated);
        }
    }

    [Theory]
    [InlineData("read")]
    [InlineData("resolver")]
    [InlineData("cancel")]
    [InlineData("callback")]
    public void PrimaryFailureDrainsAllActiveSourcesBeforeOuterUsingUnwinds(string failure)
    {
        using var cancellation = new CancellationTokenSource();
        var primary = new XmlParseException([new("user.primary", XmlDiagnosticSeverity.Fatal, "primary", Location)]);
        var secondary = new IOException("parent cleanup");
        var streams = new[] { new CountingStream([1]) { DisposeFailure = secondary }, new CountingStream([1]) { DisposeFailure = secondary } };
        var i = 0;
        var resolver = new DelegateResolver(_ => i == 2 ? throw primary : new(streams[i], new Uri($"urn:stack:{i++}")));
        var session = SessionWithToken(resolver, cancellation.Token);
        Exception? observed = null;
        var escaped = Assert.ThrowsAny<Exception>(() =>
        {
            using (session)
            {
                session.Open(Request(), Location);
                var child = session.Open(Request(), Location).Resource!;
                try
                {
                    if (failure == "read")
                    {
                        streams[1].ReadFailure = primary;
                        child.Read(new byte[1], Location);
                    }
                    else if (failure == "resolver")
                    {
                        session.Open(Request(), Location);
                    }
                    else if (failure == "cancel")
                    {
                        cancellation.Cancel();
                        session.Open(Request(), Location);
                    }
                    else
                    {
                        try
                        {
                            throw primary;
                        }
                        catch { session.Abort(); throw; }
                    }
                }
                catch (Exception error)
                {
                    observed = error;
                    Assert.All(streams, stream => Assert.Equal(1, stream.SyncDisposals));
                    Assert.Throws<InvalidOperationException>(() => session.Open(Request(), Location));
                    Assert.Throws<InvalidOperationException>(() => child.Read(new byte[1], Location));
                    throw;
                }
            }
        });
        Assert.Same(observed, escaped);
        if (failure == "cancel")
        {
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(escaped).CancellationToken);
        }
        else
        {
            Assert.Same(primary, escaped);
        }

        Assert.All(streams, stream => { Assert.Equal(1, stream.SyncDisposals); Assert.Equal(0, stream.AsyncDisposals); });
    }
    [Theory]
    [InlineData("read")]
    [InlineData("resolver")]
    [InlineData("cancel")]
    [InlineData("callback")]
    public async Task PrimaryAsyncFailureDrainsAllActiveSourcesBeforeOuterUsingUnwinds(string failure)
    {
        using var cancellation = new CancellationTokenSource();
        var primary = new IOException("primary async");
        var secondary = new IOException("parent cleanup");
        var streams = new[] { new CountingStream([1]) { DisposeFailure = secondary }, new CountingStream([1]) { DisposeFailure = secondary } };
        var i = 0;
        var resolver = new AsyncResolver(async _ => { await Task.Yield(); return i == 2 ? throw primary : new(streams[i], new Uri($"urn:async-stack:{i++}")); });
        var session = SessionWithToken(resolver, cancellation.Token);
        Exception? observed = null;
        var escaped = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using (session)
            {
                await session.OpenAsync(Request(), Location);
                var child = (await session.OpenAsync(Request(), Location)).Resource!;
                try
                {
                    if (failure == "read")
                    {
                        streams[1].ReadFailure = primary;
                        await child.ReadAsync(new byte[1], Location);
                    }
                    else if (failure == "resolver")
                    {
                        await session.OpenAsync(Request(), Location);
                    }
                    else if (failure == "cancel")
                    {
                        cancellation.Cancel();
                        await session.OpenAsync(Request(), Location);
                    }
                    else
                    {
                        try
                        {
                            throw primary;
                        }
                        catch { await session.AbortAsync(); throw; }
                    }
                }
                catch (Exception error)
                {
                    observed = error;
                    Assert.All(streams, stream => Assert.Equal(1, stream.AsyncDisposals));
                    await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.OpenAsync(Request(), Location));
                    await Assert.ThrowsAsync<InvalidOperationException>(async () => await child.ReadAsync(new byte[1], Location));
                    throw;
                }
            }
        });
        Assert.Same(observed, escaped);
        if (failure == "cancel")
        {
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(escaped).CancellationToken);
        }
        else
        {
            Assert.Same(primary, escaped);
        }

        Assert.All(streams, stream => { Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals); });
    }
    [Fact]
    public void RejectedWrapperCleanupExceptionFaultsAndDrainsParent()
    {
        var primary = new IOException("rejected cleanup");
        var parent = new CountingStream([]) { DisposeFailure = new IOException("parent cleanup") };
        var rejected = new CountingStream([]) { DisposeFailure = primary };
        var i = 0;
        using var session = Session(new DelegateResolver(_ => new(i++ == 0 ? parent : rejected, Source)));
        session.Open(Request(), Location);
        Assert.Same(primary, Assert.Throws<IOException>(() => session.Open(Request(), Location)));
        Assert.Equal(1, parent.SyncDisposals);
        Assert.Equal(1, rejected.SyncDisposals);
        Assert.Throws<InvalidOperationException>(() => session.Open(Request(), Location));
    }
    [Fact]
    public async Task RejectedWrapperAsyncCleanupExceptionFaultsAndDrainsParent()
    {
        var primary = new IOException("rejected cleanup");
        var parent = new CountingStream([]) { DisposeFailure = new IOException("parent cleanup") };
        var rejected = new CountingStream([]) { DisposeFailure = primary };
        var i = 0;
        await using var session = Session(new AsyncResolver(_ => Task.FromResult<XmlResolvedResource?>(new(i++ == 0 ? parent : rejected, Source))));
        await session.OpenAsync(Request(), Location);
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(async () => await session.OpenAsync(Request(), Location)));
        Assert.Equal(1, parent.AsyncDisposals);
        Assert.Equal(1, rejected.AsyncDisposals);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.OpenAsync(Request(), Location));
    }

    [Fact]
    public void ChildDisposeFailureDrainsParentsBeforeNestedUsingUnwinds()
    {
        var primary = new IOException("child cleanup");
        var parent = new CountingStream([]) { DisposeFailure = new IOException("parent cleanup") };
        var child = new CountingStream([]) { DisposeFailure = primary };
        var i = 0;
        var session = Session(new DelegateResolver(_ => new(i == 0 ? parent : child, new Uri($"urn:nested-dispose:{i++}"))));
        Assert.Same(primary, Assert.Throws<IOException>(() =>
        {
            using (session)
            {
                session.Open(Request(), Location);
                using (session.Open(Request(), Location).Resource!)
                {
                }
            }
        }));
        Assert.Equal(1, parent.SyncDisposals);
        Assert.Equal(1, child.SyncDisposals);
        session.Dispose();
    }
    [Fact]
    public async Task ChildDisposeAsyncFailureDrainsParentsBeforeNestedUsingUnwinds()
    {
        var primary = new IOException("child cleanup");
        var parent = new CountingStream([]) { DisposeFailure = new IOException("parent cleanup") };
        var child = new CountingStream([]) { DisposeFailure = primary };
        parent.YieldOnDispose = true;
        child.YieldOnDispose = true;
        var i = 0;
        var session = Session(new AsyncResolver(_ => Task.FromResult<XmlResolvedResource?>(new(i == 0 ? parent : child, new Uri($"urn:nested-async-dispose:{i++}")))));
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(async () =>
        {
            await using (session)
            {
                await session.OpenAsync(Request(), Location);
                await using ((await session.OpenAsync(Request(), Location)).Resource!)
                {
                }
            }
        }));
        Assert.Equal(1, parent.AsyncDisposals);
        Assert.Equal(1, child.AsyncDisposals);
        Assert.Equal(0, parent.SyncDisposals);
        Assert.Equal(0, child.SyncDisposals);
        await session.DisposeAsync();
    }
    [Fact]
    public void IndividualDisposeFailureFaultsSessionWithoutRecursiveCleanup()
    {
        var streams = new List<CountingStream>();
        var i = 0;
        var session = Session(new DelegateResolver(_ => { var stream = new CountingStream([]) { DisposeFailure = new IOException("cleanup") }; streams.Add(stream); return new(stream, new Uri($"urn:drain:{i++}")); }), count: null);
        for (var n = 0; n < 200; n++)
        {
            session.Open(Request(), Location);
        }

        var last = session.Open(Request(), Location).Resource!;
        Assert.Throws<IOException>(last.Dispose);
        Assert.All(streams, stream => Assert.Equal(1, stream.SyncDisposals));
        Assert.Throws<InvalidOperationException>(() => session.Open(Request(), Location));
        session.Dispose();
    }

    private sealed class DelegateResolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
    {
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return resolve(request);
        }
    }
    private sealed class AsyncResolver(Func<CancellationToken, Task<XmlResolvedResource?>> resolve) : XmlResourceResolver
    {
        internal int SyncCalls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            SyncCalls++;
            throw new InvalidOperationException("Sync fallback was used.");
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken) => await resolve(cancellationToken);
    }
    private sealed class CountingStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal int SyncDisposals, AsyncDisposals, AsyncReads;
        internal bool YieldOnDispose;
        internal readonly List<int> Requested = [];
        internal Exception? ReadFailure, DisposeFailure;
        internal Action? OnRead;
        private bool readable = true;
        public override bool CanRead => readable && base.CanRead;
        internal void SetUnreadable() => readable = false;
        public override int Read(Span<byte> buffer)
        {
            Requested.Add(buffer.Length);
            if (ReadFailure is not null)
            {
                throw ReadFailure;
            }

            var n = base.Read(buffer);
            OnRead?.Invoke();
            return n;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            AsyncReads++;
            return ValueTask.FromResult(Read(buffer.Span));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SyncDisposals++;
                base.Dispose(disposing);
                if (DisposeFailure is not null)
                {
                    throw DisposeFailure;
                }
            }
        }
        public override async ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            if (YieldOnDispose)
            {
                await Task.Yield();
            }

            base.Dispose(true);
            if (DisposeFailure is not null)
            {
                throw DisposeFailure;
            }
        }
    }
    private sealed class ZeroStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }
        public override int Read(Span<byte> buffer)
        {
            buffer.Clear();
            return buffer.Length;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

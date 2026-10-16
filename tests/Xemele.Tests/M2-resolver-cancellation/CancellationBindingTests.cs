using Xemele.Internal.Operations;
using Xemele.Resolution.Core;
using Xunit;

namespace Xemele.Tests.Resolution;

public sealed class CancellationBindingTests
{
    private static readonly Uri ParentUri = new("urn:binding:parent");
    private static readonly Uri ChildUri = new("urn:binding:child");
    private static XmlResourceRequest Request(Uri? uri = null) => new(uri ?? ParentUri, ParentUri, XmlResourceKind.ExternalEntity);
    private static XmlResolutionSession Session(Resolver resolver, CancellationToken token, OperationBudget? budget = null) =>
        new(resolver, budget ?? new OperationBudget(new(), default), token);

    [Fact]
    public void SequentialSyncCallsUseExactNewTokenAndKeepParent()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        var parent = new ProbeStream();
        var child = new ProbeStream();
        var calls = 0;
        var resolver = new Resolver((r, token) => { Assert.Equal(calls++ == 0 ? a.Token : b.Token, token); return new(calls == 1 ? parent : child, r.Uri); });
        using var session = Session(resolver, a.Token);
        using var first = session.Open(Request(), default).Resource!;
        session.BindCancellationToken(b.Token);
        a.Cancel();
        using var second = session.Open(Request(ChildUri), default).Resource!;
        Assert.Equal(1, first.Read(new byte[1], default).Count);
        Assert.Equal(0, parent.Disposals);
        Assert.Equal(2, session.ResourcesOpened);
    }

    [Fact]
    public async Task SequentialAsyncCallsAndSuspendedReadUseExactNewToken()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        var parent = new ProbeStream();
        var child = new ProbeStream { SuspendRead = true };
        var calls = 0;
        var resolver = new Resolver((_, _) => throw new InvalidOperationException("Sync fallback forbidden."),
            (r, token) => { Assert.Equal(calls++ == 0 ? a.Token : b.Token, token); return ValueTask.FromResult<XmlResolvedResource?>(new(calls == 1 ? parent : child, r.Uri)); });
        await using var session = Session(resolver, a.Token);
        await session.OpenAsync(Request(), default);
        session.BindCancellationToken(b.Token);
        a.Cancel();
        var handle = (await session.OpenAsync(Request(ChildUri), default)).Resource!;
        var read = handle.ReadAsync(new byte[1], default);
        Assert.False(read.IsCompleted);
        Assert.Equal(b.Token, child.ReadToken);
        child.ResumeRead.SetResult();
        Assert.Equal(1, (await read).Count);
        Assert.Equal(0, parent.Disposals);
        Assert.Equal(0, resolver.SyncCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanceledBindingDrainsAllOnceAndPreservesExactToken(bool cleanupThrows)
    {
        using var token = new CancellationTokenSource();
        var parent = new ProbeStream { CleanupThrows = cleanupThrows };
        var child = new ProbeStream { CleanupThrows = cleanupThrows };
        var resolver = new Resolver((r, _) => new(r.Uri == ParentUri ? parent : child, r.Uri));
        using var session = Session(resolver, TestContext.Current.CancellationToken);
        var first = session.Open(Request(), default).Resource!;
        var second = session.Open(Request(ChildUri), default).Resource!;
        token.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => session.BindCancellationToken(token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
        Assert.Equal(1, parent.Disposals);
        Assert.Equal(1, child.Disposals);
        Assert.Throws<InvalidOperationException>(() => session.BindCancellationToken(TestContext.Current.CancellationToken));
        first.Dispose();
        second.Dispose();
        session.Dispose();
        Assert.Equal(1, parent.Disposals);
        Assert.Equal(1, child.Disposals);
    }

    [Fact]
    public void BudgetTokenRemainsIndependentAfterBinding()
    {
        using var budgetToken = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        var stream = new ProbeStream { CleanupThrows = true };
        var budget = new OperationBudget(new(), budgetToken.Token);
        using var session = Session(new Resolver((_, _) => new(stream, ParentUri)), TestContext.Current.CancellationToken, budget);
        session.Open(Request(), default);
        budgetToken.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => session.BindCancellationToken(b.Token));
        Assert.Equal(budgetToken.Token, error.CancellationToken);
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public void ConstructorLimitOverloadRetainsOriginalBudgetCancellation()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        using var session = new XmlResolutionSession(new Resolver((_, _) => null), new XmlResourceLimits(), a.Token);
        session.BindCancellationToken(b.Token);
        a.Cancel();
        Assert.Equal(a.Token, Assert.Throws<OperationCanceledException>(() => session.Open(Request(), default)).CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindingRejectsUnavailableSessionBeforeCanceledToken(bool disposed)
    {
        using var token = new CancellationTokenSource();
        token.Cancel();
        using var session = Session(new Resolver((_, _) => null), TestContext.Current.CancellationToken);
        if (disposed)
        {
            session.Dispose();
        }
        else
        {
            session.Abort();
        }

        if (disposed)
        {
            Assert.Throws<ObjectDisposedException>(() => session.BindCancellationToken(token.Token));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => session.BindCancellationToken(token.Token));
        }
    }

    [Fact]
    public void ReentrantResolverBindingDoesNotMutateOrFaultActiveWork()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        b.Cancel();
        XmlResolutionSession? session = null;
        var stream = new ProbeStream();
        var resolver = new Resolver((_, token) =>
        {
            Assert.Equal(a.Token, token);
            Assert.Throws<InvalidOperationException>(() => session!.BindCancellationToken(b.Token));
            return new(stream, ParentUri);
        });
        using (session = Session(resolver, a.Token))
        {
            var handle = session.Open(Request(), default).Resource!;
            Assert.Equal(1, handle.Read(new byte[1], default).Count);
            Assert.Equal(0, stream.Disposals);
        }
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public async Task SuspendedResolverBindingRejectsThenOriginalTokenSurvives()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        b.Cancel();
        var completion = new TaskCompletionSource<XmlResolvedResource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new ProbeStream();
        var resolver = new Resolver((_, _) => throw new InvalidOperationException(), (_, token) => { Assert.Equal(a.Token, token); return new(completion.Task); });
        await using var session = Session(resolver, a.Token);
        var open = session.OpenAsync(Request(), default);
        Assert.False(open.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => session.BindCancellationToken(b.Token));
        completion.SetResult(new(stream, ParentUri));
        var handle = (await open).Resource!;
        Assert.Equal(1, (await handle.ReadAsync(new byte[1], default)).Count);
        Assert.Equal(a.Token, stream.ReadToken);
    }

    [Fact]
    public async Task SuspendedStreamBindingRejectsWithoutMutatingToken()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        b.Cancel();
        var stream = new ProbeStream { SuspendRead = true };
        await using var session = Session(new Resolver((_, _) => new(stream, ParentUri)), a.Token);
        var handle = session.Open(Request(), default).Resource!;
        var read = handle.ReadAsync(new byte[1], default);
        Assert.False(read.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => session.BindCancellationToken(b.Token));
        stream.ResumeRead.SetResult();
        Assert.Equal(1, (await read).Count);
        Assert.Equal(a.Token, stream.ReadToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringResolverDrainsAcceptedParentAndChild(bool asynchronous)
    {
        using var b = new CancellationTokenSource();
        var parent = new ProbeStream { CleanupThrows = true };
        var child = new ProbeStream { CleanupThrows = true };
        var calls = 0;
        XmlResolvedResource? Resolve(XmlResourceRequest r, CancellationToken token)
        {
            if (++calls == 2)
            {
                Assert.Equal(b.Token, token);
                b.Cancel();
            }
            return new(calls == 1 ? parent : child, r.Uri);
        }
        var resolver = new Resolver(Resolve, (r, t) => ValueTask.FromResult(Resolve(r, t)));
        await using var session = Session(resolver, TestContext.Current.CancellationToken);
        session.Open(Request(), default);
        session.BindCancellationToken(b.Token);
        var error = asynchronous
            ? await Assert.ThrowsAsync<OperationCanceledException>(async () => await session.OpenAsync(Request(ChildUri), default))
            : Assert.Throws<OperationCanceledException>(() => session.Open(Request(ChildUri), default));
        Assert.Equal(b.Token, error.CancellationToken);
        Assert.Equal(1, parent.Disposals);
        Assert.Equal(1, child.Disposals);
        Assert.Equal(asynchronous ? 1 : 0, child.AsyncDisposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolverPrimaryExceptionIdentitySurvivesCleanup(bool asynchronous)
    {
        using var b = new CancellationTokenSource();
        var primary = new OperationCanceledException("resolver primary", b.Token);
        var stream = new ProbeStream { CleanupThrows = true };
        var calls = 0;
        XmlResolvedResource? Resolve(XmlResourceRequest r, CancellationToken token)
        {
            if (++calls == 2)
            {
                throw primary;
            }

            return new(stream, r.Uri);
        }
        await using var session = Session(new Resolver(Resolve, (r, t) => ValueTask.FromResult(Resolve(r, t))), TestContext.Current.CancellationToken);
        session.Open(Request(), default);
        session.BindCancellationToken(b.Token);
        var error = asynchronous
            ? await Assert.ThrowsAsync<OperationCanceledException>(async () => await session.OpenAsync(Request(ChildUri), default))
            : (Exception)Assert.Throws<OperationCanceledException>(() => session.Open(Request(ChildUri), default));
        Assert.Same(primary, error);
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public async Task CancellationDuringSuspendedReadPreservesOriginalExceptionAndDrainsStack()
    {
        using var b = new CancellationTokenSource();
        var parent = new ProbeStream { CleanupThrows = true };
        var child = new ProbeStream { SuspendRead = true, CleanupThrows = true };
        var primary = new OperationCanceledException("stream primary", b.Token);
        child.ReadError = primary;
        await using var session = Session(new Resolver((r, _) => new(r.Uri == ParentUri ? parent : child, r.Uri)), TestContext.Current.CancellationToken);
        session.Open(Request(), default);
        session.BindCancellationToken(b.Token);
        var handle = session.Open(Request(ChildUri), default).Resource!;
        var read = handle.ReadAsync(new byte[1], default);
        Assert.False(read.IsCompleted);
        Assert.Equal(b.Token, child.ReadToken);
        b.Cancel();
        child.ResumeRead.SetResult();
        Assert.Same(primary, await Assert.ThrowsAsync<OperationCanceledException>(async () => await read));
        Assert.Equal(1, parent.AsyncDisposals);
        Assert.Equal(1, child.AsyncDisposals);
    }

    [Fact]
    public void PausedSessionRetainsCountersCyclesTerminalAndParentHandles()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        var streams = new List<ProbeStream>();
        var budget = new OperationBudget(new()
        {
            Limits = new()
            {
                ExternalBytes = 2,
                ExternalResources = 3
            }
        }, default);
        var resolver = new Resolver((r, _) => { var stream = new ProbeStream(); streams.Add(stream); return new(stream, r.Uri); });
        using var session = Session(resolver, a.Token, budget);
        var parent = session.Open(Request(), default).Resource!;
        Assert.Equal(1, parent.Read(new byte[1], default).Count);
        session.BindCancellationToken(b.Token);
        a.Cancel();
        Assert.Equal(1, budget.Count(Quota.ExternalBytes));
        Assert.Equal(1, session.BytesRead);
        var cycle = session.Open(Request(new Uri("urn:binding:parent#alias")), default);
        Assert.Equal("XML.ResourceCycle", cycle.Diagnostic!.Code);
        Assert.Equal(1, streams[1].Disposals);
        var child = session.Open(Request(ChildUri), default).Resource!;
        Assert.Equal(1, child.Read(new byte[1], default).Count);
        Assert.Equal(2, budget.Count(Quota.ExternalBytes));
        var limit = parent.Read(new byte[1], default).Diagnostic;
        Assert.Equal("XML.ExternalBytesLimit", limit!.Code);
        Assert.Equal(3, session.BytesRead);
        session.BindCancellationToken(TestContext.Current.CancellationToken);
        Assert.Same(limit, child.Read(new byte[1], default).Diagnostic);
        Assert.Equal(3, session.ResourcesOpened);
        Assert.Equal(2, budget.Count(Quota.ExternalBytes));
        Assert.Equal(0, streams[0].Disposals);
    }

    [Fact]
    public void BindingDoesNotResetResourceQuotaOrOtherBudgetCounters()
    {
        var budget = new OperationBudget(new()
        {
            Limits = new()
            {
                ExternalResources = 1
            }
        }, default);
        Assert.True(budget.TryCharge(Quota.ExpandedCharacters, 17));
        Assert.True(budget.TryCharge(Quota.EntityDepth, 2));
        using var session = Session(new Resolver((r, _) => new(new ProbeStream(), r.Uri)), TestContext.Current.CancellationToken, budget);
        session.Open(Request(), default).Resource!.Dispose();
        session.BindCancellationToken(TestContext.Current.CancellationToken);
        Assert.Equal("XML.ExternalResourcesLimit", session.Open(Request(ChildUri), default).Diagnostic!.Code);
        Assert.Equal(17, budget.Count(Quota.ExpandedCharacters));
        Assert.Equal(2, budget.Count(Quota.EntityDepth));
        Assert.Equal(1, session.ResourcesOpened);
    }

    private sealed class Resolver(Func<XmlResourceRequest, CancellationToken, XmlResolvedResource?> sync,
        Func<XmlResourceRequest, CancellationToken, ValueTask<XmlResolvedResource?>>? async = null) : XmlResourceResolver
    {
        internal int SyncCalls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            SyncCalls++;
            return sync(request, cancellationToken);
        }
        public override ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken) =>
            async is null ? base.ResolveAsync(request, cancellationToken) : async(request, cancellationToken);
    }
    private sealed class ProbeStream : MemoryStream
    {
        internal int Disposals, AsyncDisposals;
        internal bool CleanupThrows, SuspendRead;
        internal Exception? ReadError;
        internal CancellationToken ReadToken;
        internal readonly TaskCompletionSource ResumeRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ProbeStream() : base([1, 2, 3, 4]) { }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadToken = cancellationToken;
            if (SuspendRead)
            {
                await ResumeRead.Task.ConfigureAwait(false);
            }

            if (ReadError is not null)
            {
                throw ReadError;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Read(buffer.Span);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
                if (CleanupThrows)
                {
                    throw new IOException("cleanup");
                }
            }
            base.Dispose(disposing);
        }
        public override ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            Disposals++;
            return CleanupThrows ? ValueTask.FromException(new IOException("async cleanup")) : ValueTask.CompletedTask;
        }
    }
}

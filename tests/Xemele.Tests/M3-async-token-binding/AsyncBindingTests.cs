using Xemele.Internal.Operations;
using Xemele.Resolution.Core;
using Xunit;

namespace Xemele.Tests.AsyncTokenBinding;

public sealed class AsyncBindingTests(ITestOutputHelper output)
{
    private static XmlResourceRequest Request(string name = "parent") =>
        new(new Uri("urn:async-binding:" + name), null, XmlResourceKind.ExternalEntity);
    private static OperationBudget Budget(CancellationToken token) => new(new(), token);

    [Fact]
    public async Task SuccessfulBindingPreservesLiveHandleCountersAndUsesExactNewToken()
    {
        using var a = new CancellationTokenSource();
        using var b = new CancellationTokenSource();
        var source = new AsyncOnlyStream();
        var budget = Budget(TestContext.Current.CancellationToken);
        await using var session = new XmlResolutionSession(new Resolver(source), budget, a.Token);
        var handle = (await session.OpenAsync(Request(), default)).Resource!;
        Assert.Equal(1, (await handle.ReadAsync(new byte[1], default)).Count);
        await session.BindCancellationTokenAsync(b.Token);
        a.Cancel();
        Assert.Equal(1, (await handle.ReadAsync(new byte[1], default)).Count);
        Assert.Equal(b.Token, source.ReadToken);
        Assert.Equal(2, session.BytesRead);
        Assert.Equal(2, budget.Count(Quota.ExternalBytes));
        Assert.Equal(1, session.ResourcesOpened);
        Assert.Equal(0, source.AsyncDisposals);
        Assert.Equal(0, source.SyncCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledBindingSuspendsAsyncDrainAndPreservesPrimaryToken(bool independentBudget)
    {
        using var bound = new CancellationTokenSource();
        using var budgetToken = new CancellationTokenSource();
        var first = new AsyncOnlyStream { DelayedDispose = true, ThrowCleanup = true };
        var second = new AsyncOnlyStream { ThrowCleanup = true };
        var opens = 0;
        await using var session = new XmlResolutionSession(new Resolver(() => ++opens == 1 ? first : second),
            Budget(budgetToken.Token), TestContext.Current.CancellationToken);
        await session.OpenAsync(Request(), default);
        await session.OpenAsync(Request("child"), default);
        if (independentBudget)
        {
            budgetToken.Cancel();
        }
        else
        {
            bound.Cancel();
        }

        var binding = session.BindCancellationTokenAsync(bound.Token);
        Assert.False(binding.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.BindCancellationTokenAsync(TestContext.Current.CancellationToken).AsTask());
        first.ResumeDispose.SetResult();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => binding.AsTask());
        Assert.Equal(independentBudget ? budgetToken.Token : bound.Token, error.CancellationToken);
        Assert.Equal(1, first.AsyncDisposals);
        Assert.Equal(1, second.AsyncDisposals);
        Assert.Equal(0, first.SyncCalls);
        Assert.Equal(0, second.SyncCalls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.BindCancellationTokenAsync(TestContext.Current.CancellationToken).AsTask());
        await session.DisposeAsync();
        Assert.Equal(1, first.AsyncDisposals);
        Assert.Equal(1, second.AsyncDisposals);
    }

    [Fact]
    public async Task SuspendedOpenRejectsRebindingWithoutChangingOriginalToken()
    {
        using var a = new CancellationTokenSource();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var source = new AsyncOnlyStream();
        var completion = new TaskCompletionSource<XmlResolvedResource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = new Resolver(() => source) { PendingOpen = completion };
        await using var session = new XmlResolutionSession(resolver, Budget(TestContext.Current.CancellationToken), a.Token);
        var open = session.OpenAsync(Request(), default);
        Assert.False(open.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.BindCancellationTokenAsync(canceled.Token).AsTask());
        completion.SetResult(new(source, Request().Uri));
        var handle = (await open).Resource!;
        Assert.Equal(1, (await handle.ReadAsync(new byte[1], default)).Count);
        Assert.Equal(a.Token, source.ReadToken);
        Assert.Equal(a.Token, resolver.Token);
    }

    [Fact]
    public async Task DisposedBindingRejectsBeforeObservingCancellation()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var session = new XmlResolutionSession(new Resolver(new AsyncOnlyStream()), Budget(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await session.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.BindCancellationTokenAsync(canceled.Token).AsTask());
    }

    [Fact]
    public async Task WarmSuccessfulBindingCompletesSynchronouslyWithoutAllocations()
    {
        await using var session = new XmlResolutionSession(null, Budget(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        for (var i = 0; i < 128; i++)
        {
            await session.BindCancellationTokenAsync(TestContext.Current.CancellationToken);
        }

        var token = TestContext.Current.CancellationToken;
        var complete = true;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++)
        {
            complete &= session.BindCancellationTokenAsync(token).IsCompletedSuccessfully;
        }

        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        var positive = new byte[64];
        var control = GC.GetAllocatedBytesForCurrentThread() - start;
        GC.KeepAlive(positive);
        Assert.True(complete);
        Assert.Equal(0, bytes);
        Assert.True(control >= 64);
        output.WriteLine($"warm binding:1024ops/128warmups={bytes}B;positive={control}B; no await suspension in measured fast path");
    }

    private sealed class Resolver(Func<AsyncOnlyStream> next) : XmlResourceResolver
    {
        internal Resolver(AsyncOnlyStream source) : this(() => source) { }
        internal TaskCompletionSource<XmlResolvedResource?>? PendingOpen;
        internal CancellationToken Token;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synchronous resolution forbidden.");
        public override ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return PendingOpen is not null ? new(PendingOpen.Task) : ValueTask.FromResult<XmlResolvedResource?>(new(next(), request.Uri));
        }
    }

    private sealed class AsyncOnlyStream : MemoryStream
    {
        internal int AsyncDisposals, SyncCalls;
        internal bool DelayedDispose, ThrowCleanup;
        internal CancellationToken ReadToken;
        internal readonly TaskCompletionSource ResumeDispose = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal AsyncOnlyStream() : base([1, 2, 3, 4]) { }
        public override int Read(Span<byte> buffer)
        {
            SyncCalls++;
            throw new InvalidOperationException("Sync read forbidden.");
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(base.Read(buffer.Span));
        }
        protected override void Dispose(bool disposing)
        {
            SyncCalls++;
            throw new InvalidOperationException("Sync dispose forbidden.");
        }
        public override async ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            if (DelayedDispose)
            {
                await ResumeDispose.Task.ConfigureAwait(false);
            }

            if (ThrowCleanup)
            {
                throw new IOException("secondary cleanup failure");
            }

            base.Dispose(true);
        }
    }
}

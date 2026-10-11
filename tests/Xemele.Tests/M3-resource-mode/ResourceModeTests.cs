using System.Reflection;
using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Syntax;
using Xemele.Streaming;
using Xemele.Tests.StreamingPump;
using Xunit;

namespace Xemele.Tests.ResourceMode;

public sealed class ResourceModeTests
{
    private static readonly Uri Source = new("https://example.test/doc.xml");
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private const string Nested = "<!DOCTYPE r [<!ENTITY outer SYSTEM 'outer.ent'><!ENTITY inner SYSTEM 'inner.ent'>]><r>&outer;!</r>";
    private const string External = "<!DOCTYPE r SYSTEM 'd.dtd'><r>&outer;</r>";
    private static XmlStreamingPump Pump(Resolver resolver, XmlResourceLimits? limits = null) =>
        new(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver, Limits = limits ?? new() }, Source);
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static XmlParserCore Core(XmlStreamingPump pump) => Field<XmlParserCore>(pump, "core");
    private static string Work(XmlStreamingPump pump) => Field<object>(Core(pump), "resourceWork").ToString()!;
    private static void SameModes(XmlStreamingPump pump, bool expected)
    {
        Assert.Equal(expected, Field<bool>(pump, "asyncResources"));
        Assert.Equal(expected, Field<bool>(Core(pump), "asyncResources"));
    }
    private static void Released(XmlStreamingPump pump)
    {
        var core = Core(pump);
        foreach (string name in new[] { "asyncDtdParser", "asyncDtdPending", "asyncDtdDisposal", "asyncDtdSources", "entitySources", "dtdParser", "resolution", "entities", "entityBudget", "names", "sinkIdentity" })
            Assert.Null(core.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core));
        Assert.Empty(Field<char[]>(core, "token"));
        Assert.Empty(Field<Array>(core, "attributes"));
        Assert.Equal(0, Field<OperationBudget>(core, "budget").Count(Quota.Depth));
        Assert.Equal(0, Field<OperationBudget>(core, "budget").Count(Quota.EntityDepth));
        Assert.Equal("None", Work(pump));
        Assert.True(Field<ReadOnlyMemory<byte>>(pump, "bytes").IsEmpty);
    }
    private static async Task<StreamingStep> Drain(XmlStreamingPump pump, PumpTests.RecordingSink sink)
    {
        for (int i = 0; i < 10000; i++)
        {
            var step = await pump.AdvanceAsync(sink, Ct);
            if (step != StreamingStep.Paused) return step;
        }
        throw new InvalidOperationException("No progress.");
    }
    private static async Task PauseInsideInner(XmlStreamingPump pump, PumpTests.RecordingSink sink)
    {
        pump.SetAsyncResources(true);
        pump.Supply(Encoding.UTF8.GetBytes(Nested), true);
        for (int i = 0; i < 10000; i++)
        {
            Assert.Equal(StreamingStep.Paused, await pump.AdvanceAsync(sink, Ct));
            if (sink.Events.Last() == "text:β") return;
        }
        throw new InvalidOperationException("Inner source not reached.");
    }

    [Fact]
    public async Task DefaultSyncConstructionExplicitlySelectsDelayedAsyncOnlyResourcesAtEverySplit()
    {
        byte[] input = Encoding.UTF8.GetBytes(External);
        for (int split = 0; split <= input.Length; split++)
        {
            var resolver = new Resolver { AsyncOnly = true };
            await using var pump = Pump(resolver);
            SameModes(pump, false); pump.SetAsyncResources(true); SameModes(pump, true);
            var sink = new PumpTests.RecordingSink { Pause = true };
            pump.Supply(input.AsMemory(0, split), false);
            Assert.Equal(StreamingStep.NeedInput, await Drain(pump, sink));
            pump.Supply(input.AsMemory(split), true);
            Assert.Equal(StreamingStep.Completed, await Drain(pump, sink));
            Assert.Equal(3, resolver.AsyncOpens); Assert.Equal(0, resolver.SyncOpens);
            Assert.Equal("Aβ😀\nBZ", string.Concat(sink.Events.Where(x => x.StartsWith("text:", StringComparison.Ordinal)).Select(x => x[5..])));
            Assert.All(resolver.Streams, s => { Assert.True(s.AsyncReads > 0); Assert.Equal(1, s.AsyncDisposals); Assert.Equal(0, s.SyncReads); Assert.Equal(0, s.SyncDisposals); });
        }
    }

    [Fact]
    public async Task AlternatingModesPreservesNestedOwnersTokensLocationsAndAggregateQuotas()
    {
        var baselineResolver = new Resolver(); var resolver = new Resolver();
        await using var baseline = Pump(baselineResolver);
        var expected = new PumpTests.RecordingSink { Pause = true };
        baseline.SetAsyncResources(true); baseline.Supply(Encoding.UTF8.GetBytes(Nested), true);
        Assert.Equal(StreamingStep.Completed, await Drain(baseline, expected));
        using var pump = Pump(resolver, new() { ExternalResources = 2, EntityDepth = 2 });
        var actual = new PumpTests.RecordingSink { Pause = true };
        pump.Supply(Encoding.UTF8.GetBytes(Nested), true);
        bool async = false, switchedWithBoth = false;
        for (int i = 0; i < 10000; i++)
        {
            pump.SetAsyncResources(async); SameModes(pump, async);
            var step = async ? await pump.AdvanceAsync(actual, Ct) : pump.Advance(actual, Ct);
            Assert.NotEqual(StreamingStep.NeedResource, step);
            if (actual.Events.LastOrDefault() == "text:β")
            {
                Assert.Equal(2, Field<OperationBudget>(Core(pump), "budget").Count(Quota.EntityDepth));
                Assert.All(resolver.Streams, s => Assert.Equal(0, s.SyncDisposals + s.AsyncDisposals));
                switchedWithBoth = true;
            }
            if (step == StreamingStep.Completed) break;
            Assert.Equal(StreamingStep.Paused, step); async = !async;
        }
        Assert.True(switchedWithBoth);
        Assert.Equal(expected.Events, actual.Events); Assert.Equal(expected.Locations, actual.Locations);
        Assert.Equal(2, resolver.SyncOpens + resolver.AsyncOpens);
        Assert.All(resolver.Streams, s => Assert.Equal(1, s.SyncDisposals + s.AsyncDisposals));
        Assert.Equal(2, Field<OperationBudget>(Core(pump), "budget").Count(Quota.ExternalResources));
        Assert.Equal(0, Field<OperationBudget>(Core(pump), "budget").Count(Quota.EntityDepth));
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(true));
    }

    [Fact]
    public async Task PendingOpenActiveDtdAndDeferredDoctypeRejectSelectionWithoutMutation()
    {
        var resolver = new Resolver(); await using var pump = Pump(resolver);
        var sink = new PumpTests.RecordingSink(); pump.SetAsyncResources(true);
        pump.Supply(Encoding.UTF8.GetBytes(External), true);
        Assert.Equal(StreamingStep.NeedResource, pump.Advance(sink, Ct)); Assert.Equal("OpenSubset", Work(pump));
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false)); SameModes(pump, true);
        await pump.AdvanceResourceAsync(Ct);
        Assert.Equal("None", Work(pump)); Assert.NotNull(Field<object>(Core(pump), "asyncDtdParser"));
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false)); SameModes(pump, true);
        for (int i = 0; i < 10000; i++)
        {
            Assert.Equal(StreamingStep.NeedResource, pump.Advance(sink, Ct));
            bool finish = Work(pump) == "FinishDtd";
            Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false)); SameModes(pump, true);
            await pump.AdvanceResourceAsync(Ct);
            if (!finish) continue;
            Assert.Equal("None", Work(pump)); Assert.True(Field<bool>(Core(pump), "asyncFinishDoctype"));
            Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false)); SameModes(pump, true);
            break;
        }
        Assert.True(Field<bool>(Core(pump), "asyncSubsetRead"));
        Assert.Equal(StreamingStep.Completed, await Drain(pump, sink));
        Assert.Equal(1, resolver.Streams[0].AsyncDisposals);
    }

    [Fact]
    public async Task SuspendedOpenRejectsBothGatesAndLeavesSelectedModeIntact()
    {
        var resolver = new Resolver { OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var pump = Pump(resolver); pump.SetAsyncResources(true);
        var sink = new PumpTests.RecordingSink(); pump.Supply(Encoding.UTF8.GetBytes(External), true);
        var advancement = pump.AdvanceAsync(sink, Ct); Assert.False(advancement.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false));
        Assert.Throws<InvalidOperationException>(() => Core(pump).SetAsyncResources(false));
        Assert.Throws<InvalidOperationException>(pump.Dispose); SameModes(pump, true);
        resolver.OpenGate.SetResult(); Assert.Equal(StreamingStep.Completed, await advancement);
    }

    [Fact]
    public async Task IdleSelectionPreservesReadSubsetModelSessionScratchAndEveryQuota()
    {
        var resolver = new Resolver(); await using var pump = Pump(resolver);
        var sink = new PumpTests.RecordingSink { Pause = true };
        pump.SetAsyncResources(true); pump.Supply(Encoding.UTF8.GetBytes(External), true);
        for (int i = 0; sink.Events.LastOrDefault() != "text:β" && i < 10000; i++)
            Assert.Equal(StreamingStep.Paused, await pump.AdvanceAsync(sink, Ct));
        Assert.Equal("text:β", sink.Events.Last());
        var core = Core(pump); var budget = Field<OperationBudget>(core, "budget");
        var names = new[] { "resolution", "entities", "entitySources", "frames", "token", "attributes", "entityBudget" };
        var retained = names.Select(name => Field<object>(core, name)).ToArray();
        var counts = Enum.GetValues<Quota>().Select(budget.Count).ToArray();
        Assert.True(Field<bool>(core, "asyncSubsetRead"));
        pump.SetAsyncResources(false); pump.SetAsyncResources(true); pump.SetAsyncResources(true);
        for (int i = 0; i < names.Length; i++) Assert.Same(retained[i], Field<object>(core, names[i]));
        Assert.Equal(counts, Enum.GetValues<Quota>().Select(budget.Count).ToArray());
        Assert.True(Field<bool>(core, "asyncSubsetRead"));
        Assert.Equal(StreamingStep.Completed, await Drain(pump, sink)); Assert.Equal(3, resolver.Streams.Count);
    }

    [Theory]
    [InlineData(false, "XML.ExternalResourcesLimit")]
    [InlineData(true, "XML.ResourceLimit")]
    public async Task AlternatingModesRetainsAggregateResourceAndEntityDepthLimits(bool depth, string diagnostic)
    {
        var resolver = new Resolver();
        await using var pump = Pump(resolver, depth ? new() { EntityDepth = 1 } : new() { ExternalResources = 1 });
        var sink = new PumpTests.RecordingSink { Pause = true }; pump.Supply(Encoding.UTF8.GetBytes(Nested), true);
        bool async = false;
        for (int i = 0; i < 10000; i++)
        {
            pump.SetAsyncResources(async);
            var step = async ? await pump.AdvanceAsync(sink, Ct) : pump.Advance(sink, Ct);
            if (step == StreamingStep.Rejected) break;
            if (pump.Diagnostic is not null)
            {
                Assert.Equal(StreamingStep.Rejected, async ? await pump.AdvanceAsync(sink, Ct) : pump.Advance(sink, Ct));
                break;
            }
            Assert.Equal(StreamingStep.Paused, step); async = !async;
        }
        Assert.Equal(diagnostic, pump.Diagnostic!.Code);
        Assert.Equal(1, Assert.Single(resolver.Streams).SyncDisposals + resolver.Streams[0].AsyncDisposals);
        Assert.DoesNotContain("done", sink.Events);
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousAbortAttemptsEveryLiveNestedOwnerAndReleasesAllState(bool throwing)
    {
        var error = new IOException("inner cleanup");
        var resolver = new Resolver { SyncCleanupError = throwing ? error : null, ParentSyncError = throwing ? new IOException("outer cleanup") : null };
        var pump = Pump(resolver); var sink = new PumpTests.RecordingSink { Pause = true };
        await PauseInsideInner(pump, sink);
        Assert.Equal(2, resolver.Streams.Count);
        if (throwing) Assert.Same(error, Assert.Throws<IOException>(pump.Dispose)); else pump.Dispose();
        Assert.All(resolver.Streams, s => { Assert.Equal(1, s.SyncDisposals); Assert.Equal(0, s.AsyncDisposals); });
        Released(pump); pump.Dispose(); await pump.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => pump.SetAsyncResources(false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousAbortReleasesActiveOrDetachedDtdOwner(bool detached)
    {
        var error = new IOException("DTD cleanup"); var resolver = new Resolver { DtdCleanupError = error };
        var pump = Pump(resolver); pump.SetAsyncResources(true);
        var sink = new PumpTests.RecordingSink(); pump.Supply(Encoding.UTF8.GetBytes(External), true);
        Assert.Equal(StreamingStep.NeedResource, pump.Advance(sink, Ct)); await pump.AdvanceResourceAsync(Ct);
        if (detached)
        {
            for (int i = 0; i < 10000; i++)
            {
                Assert.Equal(StreamingStep.NeedResource, pump.Advance(sink, Ct));
                if (Work(pump) == "FinishDtd") break;
                await pump.AdvanceResourceAsync(Ct);
            }
            Assert.Equal("FinishDtd", Work(pump)); Assert.NotNull(Field<object>(Core(pump), "asyncDtdDisposal"));
        }
        Assert.Same(error, Assert.Throws<IOException>(pump.Dispose)); Released(pump);
        Assert.Equal(1, Assert.Single(resolver.Streams).SyncDisposals); pump.Dispose();
    }

    [Fact]
    public async Task AsyncAbortStillAwaitsOwnersAfterSelectingSyncMode()
    {
        var resolver = new Resolver { AsyncOnly = true, DisposalGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var pump = Pump(resolver); var sink = new PumpTests.RecordingSink { Pause = true };
        await PauseInsideInner(pump, sink); pump.SetAsyncResources(false);
        var cleanup = pump.DisposeAsync(); Assert.False(cleanup.IsCompleted);
        Assert.Equal(1, resolver.Streams[1].AsyncDisposals); Assert.Equal(0, resolver.Streams[0].AsyncDisposals);
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(true));
        resolver.DisposalGate.SetResult(); await cleanup;
        Assert.All(resolver.Streams, s => { Assert.Equal(1, s.AsyncDisposals); Assert.Equal(0, s.SyncDisposals); });
        Released(pump); await pump.DisposeAsync();
    }

    [Fact]
    public async Task PendingParameterRequestRejectsEvenRepeatedSelection()
    {
        var resolver = new Resolver { DtdText = "<!ENTITY % p SYSTEM 'p.dtd'>%p;" };
        var pump = Pump(resolver); pump.SetAsyncResources(true);
        var sink = new PumpTests.RecordingSink(); pump.Supply(Encoding.UTF8.GetBytes(External), true);
        for (int i = 0; i < 10000; i++)
        {
            Assert.Equal(StreamingStep.NeedResource, pump.Advance(sink, Ct));
            if (Work(pump) == "OpenParameter") break;
            await pump.AdvanceResourceAsync(Ct);
        }
        Assert.Equal("OpenParameter", Work(pump)); Assert.NotNull(Field<object>(Core(pump), "asyncDtdPending"));
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false));
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(true)); SameModes(pump, true);
        pump.Dispose(); Released(pump); Assert.Equal(1, Assert.Single(resolver.Streams).SyncDisposals);
    }

    [Fact]
    public async Task SynchronousAbortAttemptsAsyncOnlyDisposeAndKeepsCallerPrimaryOpen()
    {
        var resolver = new Resolver { AsyncOnly = true };
        var pump = Pump(resolver); pump.SetAsyncResources(true);
        byte[] input = Encoding.UTF8.GetBytes(Nested);
        using var primary = new MemoryStream(input);
        pump.Supply(input, true);
        var sink = new PumpTests.RecordingSink { Pause = true };
        for (int i = 0; sink.Events.LastOrDefault() != "text:β" && i < 10000; i++)
            Assert.Equal(StreamingStep.Paused, await pump.AdvanceAsync(sink, Ct));
        Assert.Equal("text:β", sink.Events.Last());
        var error = Assert.Throws<InvalidOperationException>(pump.Dispose);
        Assert.Equal("Sync dispose forbidden.", error.Message);
        Assert.All(resolver.Streams, s => { Assert.Equal(1, s.SyncDisposals); Assert.Equal(0, s.AsyncDisposals); });
        Assert.True(primary.CanRead); Assert.Equal((int)'<', primary.ReadByte()); Released(pump); pump.Dispose();
    }

    [Fact]
    public async Task NewCanceledCallTokenFaultsAndAsyncCleanupPreservesCancellation()
    {
        var resolver = new Resolver { AsyncOnly = true, AsyncCleanupError = new IOException("cleanup") };
        var pump = Pump(resolver); var sink = new PumpTests.RecordingSink { Pause = true };
        await PauseInsideInner(pump, sink);
        using var call = new CancellationTokenSource(); call.Cancel();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(async () => await pump.AdvanceAsync(sink, call.Token));
        Assert.Equal(call.Token, error.CancellationToken);
        Assert.All(resolver.Streams, s => { Assert.Equal(1, s.AsyncDisposals); Assert.Equal(0, s.SyncDisposals); });
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false));
        Released(pump); pump.Dispose();
    }

    [Fact]
    public async Task SwitchingToAsyncRebindsNewTokenAfterPreviousSyncTokenWasCanceled()
    {
        var resolver = new Resolver(); await using var pump = Pump(resolver);
        var sink = new PumpTests.RecordingSink { Pause = true }; await PauseInsideInner(pump, sink);
        using var previous = new CancellationTokenSource(); using var next = new CancellationTokenSource();
        pump.SetAsyncResources(false);
        Assert.Equal(StreamingStep.Paused, pump.Advance(sink, previous.Token));
        Assert.Equal(previous.Token, Field<CancellationToken>(Field<object>(Core(pump), "resolution"), "cancellation"));
        previous.Cancel(); pump.SetAsyncResources(true);
        for (int i = 0; resolver.Streams[1].LastAsyncToken != next.Token && i < 10000; i++)
            Assert.Equal(StreamingStep.Paused, await pump.AdvanceAsync(sink, next.Token));
        Assert.Equal(next.Token, resolver.Streams[1].LastAsyncToken);
        Assert.Equal(next.Token, Field<CancellationToken>(Field<object>(Core(pump), "resolution"), "cancellation"));
        Assert.Equal(StreamingStep.Completed, await Drain(pump, sink));
        Assert.All(resolver.Streams, s => Assert.Equal(1, s.SyncDisposals + s.AsyncDisposals));
    }

    [Fact]
    public async Task SyncAbortAfterCallbackFaultPreservesPrimaryExceptionAndDoesNotRetryCleanup()
    {
        var primary = new IOException("callback"); var resolver = new Resolver { SyncCleanupError = new IOException("cleanup") };
        var pump = Pump(resolver); var sink = new PumpTests.RecordingSink { Pause = true };
        await PauseInsideInner(pump, sink);
        sink.OnText = () => throw primary;
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(async () =>
        {
            for (int i = 0; i < 10000; i++)
            {
                var step = pump.Advance(sink, Ct);
                if (step == StreamingStep.NeedResource) await pump.AdvanceResourceAsync(Ct);
                else Assert.Equal(StreamingStep.Paused, step);
            }
        }));
        Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(false));
        pump.Dispose(); Released(pump);
        Assert.All(resolver.Streams, s => Assert.Equal(1, s.SyncDisposals));
        pump.Dispose();
    }

    [Fact]
    public void ModeSelectionRejectsReentrancyAndDisposedCore()
    {
        using var pump = new XmlStreamingPump(new());
        var sink = new PumpTests.RecordingSink { OnText = () => Assert.Throws<InvalidOperationException>(() => pump.SetAsyncResources(true)) };
        pump.Supply("<r>x</r>"u8.ToArray(), true); Assert.Equal(StreamingStep.Completed, pump.Advance(sink, Ct));
        using var core = new XmlParserCore(new()); core.SetAsyncResources(true); core.SetAsyncResources(false);
        core.Dispose(); Assert.Throws<ObjectDisposedException>(() => core.SetAsyncResources(true));
    }

    private sealed class Resolver : XmlResourceResolver
    {
        internal bool AsyncOnly;
        internal int SyncOpens, AsyncOpens;
        internal TaskCompletionSource? OpenGate, DisposalGate;
        internal Exception? SyncCleanupError, ParentSyncError, AsyncCleanupError, DtdCleanupError;
        internal string DtdText = "<!ENTITY outer SYSTEM 'outer.ent'><!ENTITY inner SYSTEM 'inner.ent'>";
        internal readonly List<ProbeStream> Streams = [];
        private XmlResolvedResource Open(XmlResourceRequest request)
        {
            string file = request.Uri.AbsolutePath;
            string text = file.EndsWith("d.dtd", StringComparison.Ordinal) ? DtdText :
                file.EndsWith("outer.ent", StringComparison.Ordinal) ? "A&inner;Z" : "β😀\r\nB";
            var stream = new ProbeStream(text)
            {
                AsyncOnly = AsyncOnly, DisposalGate = DisposalGate,
                SyncError = file.EndsWith("d.dtd", StringComparison.Ordinal) ? DtdCleanupError : file.EndsWith("outer.ent", StringComparison.Ordinal) ? ParentSyncError ?? SyncCleanupError : SyncCleanupError,
                AsyncError = AsyncCleanupError
            };
            Streams.Add(stream); return new(stream, new Uri("https://example.test/redirect" + file));
        }
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        { if (AsyncOnly) throw new InvalidOperationException("Sync resolver forbidden."); token.ThrowIfCancellationRequested(); SyncOpens++; return Open(request); }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken token)
        { await Task.Yield(); if (OpenGate is not null) await OpenGate.Task.WaitAsync(token); token.ThrowIfCancellationRequested(); AsyncOpens++; return Open(request); }
    }

    private sealed class ProbeStream(string text) : Stream
    {
        private readonly byte[] bytes = Encoding.UTF8.GetBytes(text);
        private int cursor;
        internal bool AsyncOnly;
        internal int SyncReads, AsyncReads, SyncDisposals, AsyncDisposals;
        internal CancellationToken LastAsyncToken;
        internal Exception? SyncError, AsyncError;
        internal TaskCompletionSource? DisposalGate;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        private int Copy(Span<byte> buffer)
        { int count = Math.Min(4, Math.Min(buffer.Length, bytes.Length - cursor)); bytes.AsSpan(cursor, count).CopyTo(buffer); cursor += count; return count; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        { if (AsyncOnly) throw new InvalidOperationException("Sync read forbidden."); SyncReads++; return Copy(buffer); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        { await Task.Yield(); token.ThrowIfCancellationRequested(); LastAsyncToken = token; AsyncReads++; return Copy(buffer.Span); }
        protected override void Dispose(bool disposing)
        { SyncDisposals++; if (AsyncOnly) throw new InvalidOperationException("Sync dispose forbidden."); if (SyncError is not null) throw SyncError; }
        public override async ValueTask DisposeAsync()
        { AsyncDisposals++; await Task.Yield(); if (DisposalGate is not null) await DisposalGate.Task; if (AsyncError is not null) throw AsyncError; }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

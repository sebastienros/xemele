using System.Text;
using Xemele.Streaming;
using Xunit;

namespace Xemele.Tests.StreamingPump;

public sealed class AsyncPumpTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly Uri Source = new("https://example.test/doc.xml");
    private const string Document = "\r\n<!DOCTYPE r SYSTEM 'd.dtd'><r>&e;!</r>";
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AsyncOnlyResourcesPauseAndResumeEveryPrimarySplitWithExactDtdEnd(bool utf16, bool manual)
    {
        byte[] bytes = utf16 ? Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Document)).ToArray() : Encoding.UTF8.GetBytes(Document);
        for (int split = 0; split <= bytes.Length; split++)
        {
            var resolver = new Resolver();
            await using var pump = new XmlStreamingPump(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Source, asyncResources: true);
            var sink = new PumpTests.RecordingSink { Pause = true };
            pump.Supply(bytes.AsMemory(0, split), false); Assert.Equal(StreamingStep.NeedInput, await Drain(pump, sink, manual));
            pump.Supply(bytes.AsMemory(split), true); Assert.Equal(StreamingStep.Completed, await Drain(pump, sink, manual));
            Assert.Equal(2, resolver.Opens); Assert.Equal(2, resolver.Streams.Count);
            Assert.All(resolver.Streams, x => { Assert.True(x.Reads > 0); Assert.Equal(1, x.Disposals); });
            Assert.Equal("document", sink.Events[0]); Assert.Equal("done", sink.Events[^1]);
            Assert.Equal("\nβ😀!", string.Concat(sink.Events.Where(x => x.StartsWith("text:", StringComparison.Ordinal)).Select(x => x[5..])));
            int dtdEnd = sink.Events.IndexOf("dtd:end");
            int closing = Document.IndexOf('>');
            Assert.Equal(new XmlLocation(Source, 2, closing - 1, utf16 ? 2 + closing * 2 : closing), sink.Locations[dtdEnd]);
            int beta = sink.Events.IndexOf("text:β"); Assert.Equal(new Uri("https://example.test/redirect/e.ent"), sink.Locations[beta].SourceUri); Assert.Equal(0, sink.Locations[beta].ByteOffset);
        }
    }
    [Fact]
    public async Task SuspendedOpenProtectsPumpGateAndResourceServiceInvokesNoCallbacks()
    {
        var resolver = new Resolver { OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var pump = new XmlStreamingPump(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Source, asyncResources: true);
        var sink = new PumpTests.RecordingSink(); pump.Supply(Encoding.UTF8.GetBytes(Document), true);
        Assert.Equal(StreamingStep.NeedResource, pump.Advance(sink, Ct)); Assert.Equal(0, resolver.Opens);
        var prefixEvents = sink.Events.ToArray();
        var service = pump.AdvanceResourceAsync(Ct); Assert.False(service.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => pump.Advance(sink, Ct));
        Assert.Throws<InvalidOperationException>(() => pump.Supply(default, true));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pump.DisposeAsync());
        resolver.OpenGate.SetResult(); await service; Assert.Equal(prefixEvents, sink.Events);
        Assert.Equal(StreamingStep.Completed, await Drain(pump, sink, false));
    }
    [Fact]
    public async Task CallbackExceptionCleanupIsAwaitedAndPreservesOriginalIdentity()
    {
        var resolver = new Resolver { CleanupError = new IOException("cleanup") };
        var error = new IOException("callback"); var sink = new PumpTests.RecordingSink { OnText = () => { if (resolver.Opens == 2) throw error; } };
        var pump = new XmlStreamingPump(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Source, asyncResources: true);
        pump.Supply(Encoding.UTF8.GetBytes(Document), true);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(async () => await pump.AdvanceAsync(sink, Ct)));
        Assert.Equal(2, resolver.Streams.Count); Assert.All(resolver.Streams, x => Assert.Equal(1, x.Disposals)); Assert.Null(pump.Diagnostic); Assert.DoesNotContain("done", sink.Events);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await pump.AdvanceAsync(sink, Ct));
        await pump.DisposeAsync(); await pump.DisposeAsync();
    }
    [Fact]
    public async Task CancellationAfterEntityCallbackAwaitsCleanupAndFaults()
    {
        using var cts = new CancellationTokenSource(); var resolver = new Resolver();
        var sink = new PumpTests.RecordingSink { OnText = () => { if (resolver.Opens == 2) cts.Cancel(); } };
        await using var pump = new XmlStreamingPump(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Source, asyncResources: true);
        pump.Supply(Encoding.UTF8.GetBytes(Document), true);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pump.AdvanceAsync(sink, cts.Token));
        Assert.All(resolver.Streams, x => Assert.Equal(1, x.Disposals)); Assert.DoesNotContain("done", sink.Events);
        Assert.Throws<InvalidOperationException>(() => pump.Advance(sink, Ct));
    }
    [Fact]
    public async Task AsyncStructuredRejectionDeliversOneDiagnosticAfterResourceCleanup()
    {
        var resolver = new Resolver { EntityBody = "<broken>" };
        await using var pump = new XmlStreamingPump(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Source, asyncResources: true);
        var sink = new PumpTests.RecordingSink { Pause = true }; pump.Supply(Encoding.UTF8.GetBytes(Document), true);
        Assert.Equal(StreamingStep.Rejected, await Drain(pump, sink, false));
        Assert.Single(sink.Events, x => x.StartsWith("diagnostic:", StringComparison.Ordinal));
        Assert.All(resolver.Streams, x => Assert.Equal(1, x.Disposals)); Assert.DoesNotContain("done", sink.Events);
    }
    private static async Task<StreamingStep> Drain(XmlStreamingPump pump, PumpTests.RecordingSink sink, bool manual)
    {
        for (int i = 0; i < 10000; i++)
        {
            var step = manual ? pump.Advance(sink, Ct) : await pump.AdvanceAsync(sink, Ct);
            if (step == StreamingStep.NeedResource) { await pump.AdvanceResourceAsync(Ct); continue; }
            if (step != StreamingStep.Paused) return step;
        }
        throw new InvalidOperationException("Non-progressing pump.");
    }
    private sealed class Resolver : XmlResourceResolver
    {
        internal int Opens;
        internal TaskCompletionSource? OpenGate;
        internal Exception? CleanupError;
        internal string EntityBody = "β😀";
        internal readonly List<AsyncStream> Streams = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) => throw new InvalidOperationException("Synchronous resolve is forbidden.");
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken token)
        {
            await Task.Yield(); if (OpenGate is not null) await OpenGate.Task.WaitAsync(token); token.ThrowIfCancellationRequested(); Opens++;
            var stream = new AsyncStream(request.Kind == XmlResourceKind.ExternalSubset ? "<!ENTITY e SYSTEM 'e.ent'>" : EntityBody) { CleanupError = request.Kind == XmlResourceKind.ExternalEntity ? CleanupError : null };
            Streams.Add(stream); return new(stream, new Uri("https://example.test/redirect/" + (request.Kind == XmlResourceKind.ExternalSubset ? "d.dtd" : "e.ent")));
        }
    }
    private sealed class AsyncStream(string text) : Stream
    {
        private readonly byte[] bytes = Encoding.UTF8.GetBytes(text);
        private int cursor;
        internal int Reads, Disposals;
        internal Exception? CleanupError;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous read forbidden.");
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            await Task.Yield(); token.ThrowIfCancellationRequested(); Reads++;
            int count = Math.Min(3, Math.Min(buffer.Length, bytes.Length - cursor)); bytes.AsMemory(cursor, count).CopyTo(buffer); cursor += count; return count;
        }
        protected override void Dispose(bool disposing) => throw new InvalidOperationException("Synchronous dispose forbidden.");
        public override async ValueTask DisposeAsync() { await Task.Yield(); Disposals++; if (CleanupError is not null) throw CleanupError; }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

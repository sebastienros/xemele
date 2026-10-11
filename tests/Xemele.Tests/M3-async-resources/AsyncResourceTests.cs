using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.AsyncResources;

public sealed class AsyncResourceTests
{
    private static readonly Uri DocumentUri = new("https://example.test/doc.xml");
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static XmlParseOptions Options(Resolver resolver, XmlResourceLimits? limits = null) =>
        new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver, Limits = limits ?? new() };

    [Fact]
    public async Task OpenBoundaryConsumesExactlyOnceAndProtectsGateAcrossAwait()
    {
        const string prefix = "<!DOCTYPE r SYSTEM 'd.dtd'>";
        var resolver = new Resolver(_ => new StreamProbe("<!ENTITY e 'ok'>")) { SuspendOpen = true };
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        var sink = new Sink();
        var result = core.Append(prefix + "<r>&e;</r>", [], true, sink, Ct);
        Assert.Equal(ScanStep.NeedResource, result.Step); Assert.Equal(prefix.Length, result.Consumed);
        Assert.Equal(0, resolver.Opens); Assert.Equal(new[] { "document" }, sink.Events);
        var repeated = core.Append("<r>&e;</r>", [], true, sink, Ct);
        Assert.Equal(ScanStep.NeedResource, repeated.Step); Assert.Equal(0, repeated.Consumed);
        var opening = core.AdvanceResourceAsync(Ct); Assert.False(opening.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, Ct));
        Assert.Throws<InvalidOperationException>(() => core.CompleteByteSource(0));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await core.DisposeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await core.AdvanceResourceAsync(Ct));
        resolver.OpenGate.SetResult(); await opening;
        Assert.Equal(new[] { "document" }, sink.Events); // Resource service invokes no sinks.
        Assert.Equal(ScanStep.Completed, (await Feed(core, "<r>&e;</r>", true, sink)).Step);
        Assert.Equal("ok", sink.Text.ToString()); Assert.Equal(1, resolver.Opens);
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.AsyncDisposals));
    }

    [Fact]
    public async Task NestedSubsetParameterAndGeneralSourcesResumeAtEveryPrimarySplitAndPause()
    {
        const string xml = "<!DOCTYPE r SYSTEM 'd.dtd' [<!ENTITY local 'L'>]><r>&e;!&local;</r>";
        for (int split = 0; split <= xml.Length; split++)
        foreach (bool pause in new[] { false, true })
        {
            var resolver = NestedResolver(); var sink = new Sink { Pause = pause };
            await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
            Assert.Equal(ScanStep.NeedInput, (await Feed(core, xml[..split], false, sink)).Step);
            Assert.Equal(ScanStep.Completed, (await Feed(core, xml[split..], true, sink)).Step);
            Assert.Equal("Aβ😀B!L", sink.Text.ToString());
            Assert.Equal(new[] { "document", "doctype", "start:r", "content", "text", "end:r", "done" }, sink.Events);
            Assert.Equal(5, resolver.Opens);
            Assert.All(resolver.Streams, s => { Assert.True(s.AsyncReads > 0); Assert.Equal(0, s.SyncReads); Assert.Equal(0, s.SyncDisposals); Assert.Equal(1, s.AsyncDisposals); });
            Assert.Contains(sink.Locations, at => at.SourceUri == new Uri("https://example.test/redirect/inner.ent") && at.ByteOffset == 0);
        }
    }

    [Fact]
    public async Task InternalSubsetParameterTriggerAndDelimiterAreNotReplayed()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p SYSTEM 'p.dtd'>%p;<!ENTITY local 'L'>]><r>&e;&local;</r>";
        var resolver = new Resolver(_ => new StreamProbe("<!ENTITY e 'E'>")); var sink = new Sink { Pause = true };
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        var boundary = core.Append(xml, [], true, sink, Ct);
        Assert.Equal(ScanStep.Paused, boundary.Step); Assert.Equal(0, boundary.Consumed);
        boundary = core.Append(xml, [], true, sink, Ct);
        Assert.Equal(ScanStep.NeedResource, boundary.Step); Assert.Equal(xml.IndexOf("%p;", StringComparison.Ordinal) + 3, boundary.Consumed);
        await core.AdvanceResourceAsync(Ct);
        Assert.Equal(ScanStep.Completed, (await Feed(core, xml[boundary.Consumed..], true, sink)).Step);
        Assert.Equal("EL", sink.Text.ToString()); Assert.Equal(1, resolver.Opens);
    }

    [Fact]
    public async Task GeneralFrameReservesDepthBeforeOpenAndRebindsExactToken()
    {
        var stream = new StreamProbe("abc") { SuspendRead = true };
        var resolver = new Resolver(_ => stream);
        using var a = new CancellationTokenSource(); using var b = new CancellationTokenSource();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        var sink = new Sink();
        const string prefix = "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;";
        var boundary = core.Append(prefix, [], false, sink, a.Token);
        Assert.Equal(ScanStep.NeedResource, boundary.Step); Assert.Equal(prefix.Length, boundary.Consumed);
        a.Cancel();
        var pending = core.AdvanceResourceAsync(b.Token); Assert.False(pending.IsCompleted);
        await stream.ReadEntered.Task;
        Assert.Equal(b.Token, stream.ReadToken); stream.ReadGate.SetResult(); await pending;
        Assert.Equal(b.Token, resolver.LastToken);
        Assert.Equal(ScanStep.Completed, (await Feed(core, "</r>", true, sink, b.Token)).Step);
        Assert.Equal("abc", sink.Text.ToString()); Assert.Equal(1, stream.AsyncDisposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndReadFailurePreservePrimaryAndAsyncCleanup(bool cancellation)
    {
        using var token = new CancellationTokenSource();
        Exception primary = cancellation ? new OperationCanceledException("read primary", token.Token) : new IOException("read primary");
        var stream = new StreamProbe("body") { SuspendRead = true, ReadError = primary, CleanupError = new IOException("cleanup") };
        var resolver = new Resolver(_ => stream); var sink = new Sink();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        var result = core.Append("<!DOCTYPE r SYSTEM 'd.dtd'><r/>", [], true, sink, token.Token);
        Assert.Equal(ScanStep.NeedResource, result.Step);
        var pending = core.AdvanceResourceAsync(token.Token); Assert.False(pending.IsCompleted);
        await stream.ReadEntered.Task;
        if (cancellation) token.Cancel(); stream.ReadGate.SetResult();
        Assert.Same(primary, await Assert.ThrowsAnyAsync<Exception>(async () => await pending));
        Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals);
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, Ct));
        Assert.DoesNotContain("done", sink.Events);
    }

    [Fact]
    public async Task CallbackFailureDefersAsyncOwnershipCleanupAndPreservesIdentity()
    {
        var stream = new StreamProbe("abcdef") { CleanupError = new IOException("cleanup") };
        var resolver = new Resolver(_ => stream); var sink = new Sink();
        var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        const string xml = "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;</r>";
        var boundary = core.Append(xml, [], true, sink, Ct); await core.AdvanceResourceAsync(Ct);
        var primary = new XmlParseException([new("TEST.Callback", XmlDiagnosticSeverity.Fatal, "callback", default)]);
        sink.FragmentError = primary;
        Assert.Same(primary, Assert.Throws<XmlParseException>(() => core.Append(xml.AsSpan(boundary.Consumed), [], true, sink, Ct)));
        Assert.Equal(0, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals);
        await core.DisposeAsync(); await core.DisposeAsync();
        Assert.Equal(1, stream.AsyncDisposals); Assert.DoesNotContain("done", sink.Events);
    }

    [Fact]
    public async Task EofDisposalGenuinelySuspendsWithoutCallbacksAndFaultsOnCleanupError()
    {
        var primary = new IOException("dispose primary");
        var stream = new StreamProbe("x") { SuspendDispose = true, CleanupError = primary };
        var resolver = new Resolver(_ => stream); var sink = new Sink();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        const string xml = "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;</r>";
        int offset = 0; ValueTask pending = default;
        for (int calls = 0; calls < 100; calls++)
        {
            var result = core.Append(xml.AsSpan(offset), [], true, sink, Ct); offset += result.Consumed;
            Assert.Equal(ScanStep.NeedResource, result.Step);
            pending = core.AdvanceResourceAsync(Ct);
            if (stream.AsyncDisposals != 0) break;
            await pending;
        }
        Assert.False(pending.IsCompleted); int events = sink.Events.Count;
        Assert.Equal(1, stream.AsyncDisposals); Assert.Equal("x", sink.Text.ToString());
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, Ct));
        Assert.Equal(events, sink.Events.Count); stream.DisposeGate.SetResult();
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(async () => await pending));
        Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals);
    }

    [Theory]
    [InlineData("refuse", "XML.ResourceDenied")]
    [InlineData("cycle", "XML.ResourceCycle")]
    [InlineData("bytes", "XML.ExternalBytesLimit")]
    [InlineData("resources", "XML.ExternalResourcesLimit")]
    [InlineData("depth", "XML.ResourceLimit")]
    public async Task AggregateQuotasRedirectCyclesAndRefusalRejectWithAsyncCleanup(string mode, string code)
    {
        var resolver = NestedResolver();
        if (mode == "refuse") resolver.Refuse = true;
        if (mode == "cycle") resolver.FinalUri = _ => new Uri("https://example.test/same");
        var limits = mode switch
        {
            "bytes" => new XmlResourceLimits { ExternalBytes = 32 },
            "resources" => new XmlResourceLimits { ExternalResources = 2 },
            "depth" => new XmlResourceLimits { EntityDepth = 1 },
            _ => new XmlResourceLimits()
        };
        await using var core = new XmlParserCore(Options(resolver, limits), DocumentUri, asyncResources: true);
        var sink = new Sink();
        var result = await Feed(core, "<!DOCTYPE r SYSTEM 'd.dtd'><r>&e;</r>", true, sink);
        Assert.Equal(ScanStep.Rejected, result.Step); Assert.Equal(code, result.Diagnostic!.Code);
        Assert.DoesNotContain("done", sink.Events);
        Assert.All(resolver.Streams, stream => { Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals); });
    }

    [Theory]
    [InlineData("<?xml encoding='made-up'?>x")]
    [InlineData("<?xml version='2.0' encoding='UTF-8'?>x")]
    [InlineData("<unclosed>")]
    public async Task MalformedExternalSourcesRejectWithoutSuccessOrSyncCleanup(string body)
    {
        var resolver = new Resolver(_ => new StreamProbe(body)); var sink = new Sink();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        var result = await Feed(core, "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;</r>", true, sink);
        Assert.Equal(ScanStep.Rejected, result.Step); Assert.NotNull(result.Diagnostic);
        Assert.Equal(1, resolver.Streams[0].AsyncDisposals); Assert.DoesNotContain("done", sink.Events);
    }

    [Fact]
    public async Task DecodeFailureDeliversValidPrefixAndOriginalByteLocation()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes("validβ"), 0xFF];
        var resolver = new Resolver(_ => new StreamProbe(bytes)); var sink = new Sink();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        var result = await Feed(core, "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;</r>", true, sink);
        Assert.Equal(ScanStep.Rejected, result.Step); Assert.Equal("validβ", sink.Text.ToString());
        Assert.Equal(7, result.Diagnostic!.Location.ByteOffset); Assert.Equal(new Uri("https://example.test/e.ent"), result.Diagnostic.Location.SourceUri);
        Assert.Equal(1, resolver.Streams[0].AsyncDisposals);
    }

    [Fact]
    public async Task CancellationDuringPausedAppendAndIdleDisposeUsesAsyncCleanupOnly()
    {
        var stream = new StreamProbe("abc"); var resolver = new Resolver(_ => stream); var sink = new Sink();
        var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        var result = core.Append("<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;", [], false, sink, Ct);
        Assert.Equal(ScanStep.NeedResource, result.Step); await core.AdvanceResourceAsync(Ct);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => core.Append([], [], false, sink, canceled.Token)).CancellationToken);
        await core.DisposeAsync(); Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals);
    }

    [Fact]
    public async Task ReentrantCallbackRejectionDoesNotFaultWhenHandledAndNoDtdAsyncModeWorks()
    {
        await using var core = new XmlParserCore(new(), asyncResources: true);
        var sink = new Sink();
        sink.OnDocument = () => Assert.Throws<InvalidOperationException>(() => core.Append([], [], false, sink, Ct));
        Assert.Equal(ScanStep.Completed, (await Feed(core, "<r>ok</r>", true, sink)).Step);
        Assert.Equal("ok", sink.Text.ToString());
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(7, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public async Task ExternalDeclarationUnicodeAndCrLfSurviveByteSplits(int chunk, bool utf16)
    {
        string declaration = utf16 ? "<?xml encoding='UTF-16'?>" : "<?xml encoding='UTF-8'?>";
        string body = declaration + "A\r\nβ😀";
        byte[] bytes = utf16 ? [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(body)] : Encoding.UTF8.GetBytes(body);
        var stream = new StreamProbe(bytes) { MaxRead = chunk };
        var resolver = new Resolver(_ => stream); var sink = new Sink { Pause = true };
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        Assert.Equal(ScanStep.Completed, (await Feed(core, "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;</r>", true, sink)).Step);
        Assert.Equal("A\nβ😀", sink.Text.ToString()); Assert.DoesNotContain("declaration", sink.Events);
        long bodyOffset = utf16 ? 2 + 2 * declaration.Length : declaration.Length;
        Assert.Equal(bodyOffset, sink.Locations[0].ByteOffset);
        Assert.Equal(declaration.Length + 1, sink.Locations[0].LinePosition);
        Assert.Contains(sink.Locations, at => at.LineNumber == 2 && at.LinePosition == 1 && at.ByteOffset == bodyOffset + (utf16 ? 6 : 3));
        Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncReads);
    }

    [Fact]
    public async Task GeneralDepthIsReservedBeforeOpeningNestedSource()
    {
        var resolver = new Resolver(_ => new StreamProbe("&inner;")); var sink = new Sink();
        await using var core = new XmlParserCore(Options(resolver, new() { EntityDepth = 1 }), DocumentUri, asyncResources: true);
        var result = await Feed(core, "<!DOCTYPE r [<!ENTITY outer SYSTEM 'outer.ent'><!ENTITY inner SYSTEM 'inner.ent'>]><r>&outer;</r>", true, sink);
        Assert.Equal(ScanStep.Rejected, result.Step); Assert.Equal("XML.ResourceLimit", result.Diagnostic!.Code);
        Assert.Equal(1, resolver.Opens); Assert.Equal(1, resolver.Streams[0].AsyncDisposals);
    }

    [Fact]
    public async Task ResolverExceptionAfterNestedParentOpenPreservesIdentityAndDrainsAsync()
    {
        var parent = new StreamProbe("&inner;") { CleanupError = new IOException("parent cleanup") };
        var primary = new IOException("resolver primary");
        var resolver = new Resolver(r => r.Uri.AbsolutePath == "/outer.ent" ? parent : throw primary);
        var sink = new Sink();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(async () => await Feed(core,
            "<!DOCTYPE r [<!ENTITY outer SYSTEM 'outer.ent'><!ENTITY inner SYSTEM 'inner.ent'>]><r>&outer;</r>", true, sink)));
        Assert.Equal(1, parent.AsyncDisposals); Assert.Equal(0, parent.SyncDisposals);
    }

    [Fact]
    public async Task CancellationAtPendingRefillCleansAlreadyOwnedSourceAsynchronously()
    {
        var stream = new StreamProbe("abc"); var resolver = new Resolver(_ => stream); var sink = new Sink();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        const string xml = "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;</r>";
        var result = core.Append(xml, [], true, sink, Ct); await core.AdvanceResourceAsync(Ct);
        result = core.Append(xml.AsSpan(result.Consumed), [], true, sink, Ct);
        Assert.Equal(ScanStep.NeedResource, result.Step);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(async () => await core.AdvanceResourceAsync(canceled.Token));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.SyncDisposals);
    }

    [Fact]
    public async Task RebindingAfterOwnedSourceOpenDoesNotReuseCanceledPriorToken()
    {
        var stream = new StreamProbe("abcdef"); var resolver = new Resolver(_ => stream); var sink = new Sink();
        using var a = new CancellationTokenSource(); using var b = new CancellationTokenSource();
        await using var core = new XmlParserCore(Options(resolver), DocumentUri, asyncResources: true);
        const string xml = "<!DOCTYPE r [<!ENTITY e SYSTEM 'e.ent'>]><r>&e;";
        Assert.Equal(ScanStep.NeedResource, core.Append(xml, [], false, sink, a.Token).Step);
        await core.AdvanceResourceAsync(a.Token);
        a.Cancel();
        Assert.Equal(ScanStep.NeedResource, core.Append([], [], false, sink, b.Token).Step);
        await core.AdvanceResourceAsync(b.Token);
        Assert.Equal(b.Token, stream.ReadToken);
        Assert.Equal(ScanStep.Completed, (await Feed(core, "</r>", true, sink, b.Token)).Step);
        Assert.Equal("abcdef", sink.Text.ToString()); Assert.Equal(1, stream.AsyncDisposals);
    }

    private static Resolver NestedResolver() => new(request => new StreamProbe(request.Uri.AbsolutePath switch
    {
        "/d.dtd" => "<?xml encoding='UTF-8'?><!ENTITY % p SYSTEM 'p.dtd'>%p;<!ENTITY e SYSTEM 'outer.ent'>",
        "/p.dtd" => "<!ENTITY % q SYSTEM 'q.dtd'>%q;",
        "/q.dtd" => "<!ENTITY inner SYSTEM 'inner.ent'>",
        "/outer.ent" => "A&inner;B",
        "/inner.ent" => "β😀",
        _ => throw new InvalidOperationException(request.Uri.ToString())
    })) { FinalUri = request => request.Uri.AbsolutePath == "/inner.ent" ? new Uri("https://example.test/redirect/inner.ent") : request.Uri };

    private static async Task<ScanResult> Feed(XmlParserCore core, string input, bool final, Sink sink, CancellationToken? token = null)
    {
        int offset = 0;
        for (int calls = 0; calls < 10000; calls++)
        {
            var result = core.Append(input.AsSpan(offset), [], final, sink, token ?? Ct);
            Assert.InRange(result.Consumed, 0, input.Length - offset); offset += result.Consumed;
            if (result.Step == ScanStep.NeedResource) { await core.AdvanceResourceAsync(token ?? Ct); continue; }
            if (result.Step == ScanStep.Paused) continue;
            if (result.Step != ScanStep.Rejected) Assert.Equal(input.Length, offset);
            return result;
        }
        throw new InvalidOperationException("Core did not make bounded progress.");
    }

    private sealed class Resolver(Func<XmlResourceRequest, StreamProbe> factory) : XmlResourceResolver
    {
        internal int Opens;
        internal bool SuspendOpen, Refuse;
        internal CancellationToken LastToken;
        internal Func<XmlResourceRequest, Uri>? FinalUri;
        internal readonly List<StreamProbe> Streams = [];
        internal readonly TaskCompletionSource OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("Sync resolver forbidden.");
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Opens++; LastToken = cancellationToken;
            if (SuspendOpen) await OpenGate.Task.WaitAsync(cancellationToken); else await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (Refuse) return null;
            var stream = factory(request); Streams.Add(stream); return new(stream, FinalUri?.Invoke(request) ?? request.Uri);
        }
    }

    private sealed class StreamProbe(byte[] bytes) : Stream
    {
        internal StreamProbe(string text) : this(Encoding.UTF8.GetBytes(text)) { }
        private int index;
        internal int SyncReads, AsyncReads, SyncDisposals, AsyncDisposals;
        internal int MaxRead = int.MaxValue;
        internal bool SuspendRead, SuspendDispose;
        internal Exception? ReadError, CleanupError;
        internal CancellationToken ReadToken;
        internal readonly TaskCompletionSource ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource DisposeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => index; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { SyncReads++; throw new InvalidOperationException("Sync read forbidden."); }
        public override int Read(Span<byte> buffer) { SyncReads++; throw new InvalidOperationException("Sync read forbidden."); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            AsyncReads++; ReadToken = cancellationToken; ReadEntered.TrySetResult();
            if (SuspendRead) { SuspendRead = false; await ReadGate.Task; }
            else await Task.Yield();
            if (ReadError is not null) throw ReadError;
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(Math.Min(buffer.Length, MaxRead), bytes.Length - index); bytes.AsMemory(index, count).CopyTo(buffer); index += count;
            return count;
        }
        protected override void Dispose(bool disposing) { if (disposing) { SyncDisposals++; throw new InvalidOperationException("Sync dispose forbidden."); } }
        public override async ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            if (SuspendDispose) await DisposeGate.Task; else await Task.Yield();
            if (CleanupError is not null) throw CleanupError;
            GC.SuppressFinalize(this);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class Sink : IXmlEventSink, IXmlDtdSink
    {
        internal bool Pause;
        internal Action? OnDocument;
        internal Exception? FragmentError;
        internal readonly List<string> Events = [];
        internal readonly List<XmlLocation> Locations = [];
        internal readonly StringBuilder Text = new();
        private bool Event(string text) { Events.Add(text); return !Pause; }
        public bool StartDocument(XmlLocation l) { OnDocument?.Invoke(); return Event("document"); }
        public bool DocumentType(XmlDocumentType t, Xemele.Parsing.Dtd.DtdModel m, XmlLocation l) => Event("doctype");
        public bool Declaration(XmlDeclarationView d) => Event("declaration");
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) => Event("start:" + n.LocalName.ToString());
        public bool NamespaceDeclaration(XmlNamespaceView n) => Event("namespace");
        public bool Attribute(XmlAttributeView a) => Event("attribute");
        public bool StartElementContent() => Event("content");
        public bool EndElement(XmlNameView n, XmlLocation l) => Event("end:" + n.LocalName.ToString());
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> t, XmlLocation l) => Event("text");
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l)
        {
            if (FragmentError is not null) throw FragmentError;
            Text.Append(t); if (!t.IsEmpty) Locations.Add(l); return !Pause;
        }
        public bool EndDocument(XmlLocation l) => Event("done");
    }
}

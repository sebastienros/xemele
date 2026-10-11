using System.Text;
using Xemele.Streaming;
using Xunit;
using Reader = Xemele.Streaming.XmlReader;

namespace Xemele.Tests.AsyncInput;

public sealed class AsyncInputTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Corpus = "<?xml version='1.0'?><!--p--><!DOCTYPE r [<!ELEMENT r ANY><!ATTLIST r a CDATA 'v'><!ENTITY e 'E'>]><r xmlns='u' xmlns:p='p' p:b='β😀'>a\r\n&e;<![CDATA[x😀]]><![CDATA[]]><!--c--><?pi data?><c/></r>";
    private static XmlParseOptions Options => new() { DtdProcessing = XmlDtdProcessing.Parse };

    [Fact]
    public async Task EveryShortReadMatchesSaxEventsReaderTokensAndDomLogicalNodes()
    {
        for (int chunk = 1; chunk <= 32; chunk++)
        {
            using var sync = new Probe(Corpus, chunk) { AllowSync = true };
            var expected = new Recorder(); XmlSaxParser.Parse(sync, expected, Options, Ct);
            using var input = new Probe(Corpus, chunk);
            var actual = new Recorder(); await XmlSaxParser.ParseAsync(input, actual, Options, Ct);
            Assert.Equal(expected.Events, actual.Events); Assert.Equal(0, input.SyncReads); Assert.Equal(0, input.Disposals);
            using var a = new Probe(Corpus, chunk) { AllowSync = true }; using var sr = Reader.Create(a, Options, Ct);
            using var b = new Probe(Corpus, chunk); await using var ar = Reader.Create(b, Options, Ct);
            var s = new List<string>(); var t = new List<string>();
            while (sr.Read()) s.Add(Token(sr));
            while (await ar.ReadAsync(Ct)) t.Add(Token(ar));
            Assert.Equal(s, t); Assert.Equal(XmlTokenKind.None, ar.TokenKind); Assert.False(await ar.ReadAsync(Ct));
            using var c = new Probe(Corpus, chunk) { AllowSync = true }; var sd = XmlDocument.Load(c, Options, Ct);
            using var d = new Probe(Corpus, chunk); var ad = await XmlDocument.LoadAsync(d, Options, Ct);
            Assert.Equal(Tree(sd.DocumentNode), Tree(ad.DocumentNode)); Assert.Equal(sd.DocumentType, ad.DocumentType);
            Assert.Equal(Model(sd), Model(ad));
            Assert.Equal(0, d.SyncReads); Assert.Equal(0, d.Disposals);
        }
    }

    [Theory]
    [InlineData("<r a='1' a='2'><c></r>", true)]
    [InlineData("<r><", true)]
    [InlineData("<r>", false)]
    [InlineData("<!DOCTYPE r><r/>", false)]
    public async Task RecoveryFlagsAndExactDiagnosticsMatchExistingFactories(string xml, bool recover)
    {
        var options = new XmlParseOptions { Recover = recover };
        using var s = new Probe(xml, 1) { AllowSync = true }; using var a = new Probe(xml, 1);
        var sd = XmlDocument.LoadWithDiagnostics(s, options, Ct); var ad = await XmlDocument.LoadWithDiagnosticsAsync(a, options, Ct);
        Assert.Equal(sd.IsComplete, ad.IsComplete); Assert.Equal(sd.IsWellFormed, ad.IsWellFormed); Assert.Equal(sd.Diagnostics, ad.Diagnostics);
        Assert.Equal(sd.Value is null ? null : Tree(sd.Value.DocumentNode), ad.Value is null ? null : Tree(ad.Value.DocumentNode));
        var sh = new Recorder(); var ah = new Recorder();
        using var si = new Probe(xml, 1) { AllowSync = true }; using var ai = new Probe(xml, 1);
        var ss = XmlSaxParser.ParseWithDiagnostics(si, sh, options, Ct); var aa = await XmlSaxParser.ParseWithDiagnosticsAsync(ai, ah, options, Ct);
        Assert.Equal(ss.IsComplete, aa.IsComplete); Assert.Equal(ss.IsWellFormed, aa.IsWellFormed); Assert.Equal(ss.Diagnostics, aa.Diagnostics); Assert.Equal(sh.Events, ah.Events);
        Assert.Equal(aa.Diagnostics, ah.Diagnostics); Assert.DoesNotContain("end-document", ah.Events);
    }

    [Fact]
    public async Task LongLogicalTextAndIncompleteDtdMetadataMatchDomWithoutNodeSplitting()
    {
        string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA 'before&missing;after'>]><r b='a&unknown;b'>" + new string('x', 12000) + "<![CDATA[β😀]]>&unknown;<c/></r>";
        using var sync = new Probe(xml, 13) { AllowSync = true }; using var input = new Probe(xml, 7);
        var expected = XmlDocument.Load(sync, Options, Ct); var actual = await XmlDocument.LoadAsync(input, Options, Ct);
        Assert.Equal(Model(expected), Model(actual)); Assert.Equal(expected.DocumentType, actual.DocumentType);
        Assert.Equal(expected.RootElement.FirstChild.Kind, actual.RootElement.FirstChild.Kind);
        Assert.Equal(new string('x', 12000), actual.RootElement.FirstChild.Value);
        Assert.Equal(expected.IncompleteAttributeDefaults.Select(x => x.ToString()), actual.IncompleteAttributeDefaults.Select(x => x.ToString()));
        // Unresolved reference nodes carry lexical data and physical locations.
        Assert.Equal(expected.UnresolvedReferences.Select(x => x.ToString()), actual.UnresolvedReferences.Select(x => x.ToString()));
        Assert.Equal(expected.IncompleteAttributeValues.Select(x => x.LexicalValue), actual.IncompleteAttributeValues.Select(x => x.LexicalValue));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ValidationPrecedesIoAndStrictRecoveryIsRejected(int transport)
    {
        using var input = new Probe("<r/>");
        if (transport == 0) await Assert.ThrowsAsync<ArgumentException>(() => XmlSaxParser.ParseAsync(input, new Recorder(), new() { Recover = true }, Ct));
        else if (transport == 1) Assert.Throws<ArgumentException>(() => Reader.Create(input, new() { Recover = true }, Ct));
        else await Assert.ThrowsAsync<ArgumentException>(() => XmlDocument.LoadAsync(input, new() { Recover = true }, Ct));
        Assert.Equal(0, input.AsyncReads); Assert.Equal(0, input.SyncReads);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task QuotaFailureCannotBecomeRecoverySuccess(bool recover)
    {
        var options = new XmlParseOptions { Recover = recover, Limits = new() { DocumentCharacters = 3 } };
        using var a = new Probe("<r/>", 1); using var s = new Probe("<r/>", 1) { AllowSync = true };
        var expected = XmlDocument.LoadWithDiagnostics(s, options, Ct); var actual = await XmlDocument.LoadWithDiagnosticsAsync(a, options, Ct);
        Assert.False(actual.IsComplete); Assert.False(actual.IsWellFormed); Assert.Equal(expected.Diagnostics, actual.Diagnostics);
        var handler = new Recorder(); using var sax = new Probe("<r/>", 1);
        var result = await XmlSaxParser.ParseWithDiagnosticsAsync(sax, handler, options, Ct);
        Assert.False(result.IsComplete); Assert.False(result.IsWellFormed); Assert.Equal(result.Diagnostics, handler.Diagnostics);
    }

    [Fact]
    public async Task ReadActuallySuspendsInvalidatesCurrentAtInvocationAndHoldsOneGate()
    {
        using var input = new Probe("<r/>") { DelayRead = true };
        await using var reader = Reader.Create(input, cancellationToken: Ct);
        Assert.Equal(0, input.AsyncReads); Assert.True(await reader.ReadAsync(Ct)); Assert.Equal(XmlTokenKind.StartDocument, reader.TokenKind);
        var pending = reader.ReadAsync(Ct); Assert.False(pending.IsCompleted); await input.ReadEntered.Task;
        Assert.Equal(XmlTokenKind.None, reader.TokenKind);
        Assert.Throws<InvalidOperationException>(() => reader.Read()); Assert.Throws<InvalidOperationException>(() => reader.Dispose());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.ReadAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.DisposeAsync());
        input.ReadGate.SetResult(); Assert.True(await pending); Assert.Equal(XmlTokenKind.StartElement, reader.TokenKind);
        await reader.DisposeAsync(); Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.Equal(0, input.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await reader.ReadAsync(Ct));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EitherReaderTokenCancelsAcrossAwaitAndPermanentlyFaults(bool operation)
    {
        using var op = new CancellationTokenSource(); using var call = new CancellationTokenSource();
        using var input = new Probe("<r/>") { DelayRead = true };
        await using var reader = Reader.Create(input, cancellationToken: op.Token);
        Assert.True(await reader.ReadAsync(call.Token)); var pending = reader.ReadAsync(call.Token); await input.ReadEntered.Task;
        (operation ? op : call).Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.Equal(0, input.Disposals);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task PreviousCallCancellationDoesNotPoisonNextReadAndMixedReadsPreserveCursor()
    {
        using var input = new Probe("<r>abc<c/>def</r>", 2) { AllowSync = true };
        await using var reader = Reader.Create(input, cancellationToken: Ct);
        using var first = new CancellationTokenSource(); Assert.True(await reader.ReadAsync(first.Token)); first.Cancel();
        var tokens = new List<XmlTokenKind>(); int calls = 0;
        while ((calls++ % 2 == 0) ? reader.Read() : await reader.ReadAsync(Ct)) tokens.Add(reader.TokenKind);
        Assert.Equal(XmlTokenKind.EndDocument, tokens[^1]); Assert.True(input.SyncReads > 0); Assert.True(input.AsyncReads > 0);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task AsyncOnlyNestedExternalResourcesAndAwaitedDisposal(int transport)
    {
        var resolver = new Resolver(); var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
        using var input = new Probe("<!DOCTYPE r SYSTEM 'https://example.test/d.dtd'><r>&e;</r>", 2);
        if (transport == 0) { var h = new Recorder(); await XmlSaxParser.ParseAsync(input, h, options, Ct); Assert.Equal("β😀", string.Concat(h.Events.Where(e => e.StartsWith("text:", StringComparison.Ordinal)).Select(e => e[5..]))); }
        else if (transport == 1) { await using var r = Reader.Create(input, options, Ct); var text = new StringBuilder(); while (await r.ReadAsync(Ct)) if (r.TokenKind == XmlTokenKind.Text) text.Append(r.ValueSpan); Assert.Equal("β😀", text.ToString()); }
        else { var d = await XmlDocument.LoadAsync(input, options, Ct); Assert.Equal("β😀", d.RootElement.GetTextContent()); Assert.NotNull(d.Dtd); }
        Assert.Equal(2, resolver.Streams.Count);
        Assert.All(resolver.Streams, stream => { Assert.Equal(0, stream.SyncReads); Assert.True(stream.AsyncReads > 0); Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.Disposals); });
        Assert.Equal(0, input.Disposals);
    }

    [Fact]
    public async Task CallbackPrimaryIncludingParseExceptionSurvivesAsyncResourceCleanupFailure()
    {
        var primary = new XmlParseException([new("USER", XmlDiagnosticSeverity.Fatal, "sentinel", default)]);
        var resolver = new Resolver { CleanupError = new IOException("cleanup") };
        using var input = new Probe("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/e.ent'>]><r>&e;</r>");
        var h = new Recorder { TextError = primary };
        var actual = await Assert.ThrowsAsync<XmlParseException>(() => XmlSaxParser.ParseAsync(input, h, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Ct));
        Assert.Same(primary, actual); Assert.Empty(h.Diagnostics); Assert.DoesNotContain("end-document", h.Events);
        Assert.Single(resolver.Streams); Assert.Equal(1, resolver.Streams[0].AsyncDisposals); Assert.Equal(0, resolver.Streams[0].Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReaderAbortReleasesLiveResourceOnceWithSelectedDisposePolicy(bool asyncDispose)
    {
        var resolver = new Resolver { EntityText = "abcdef", EntityChunk = 1, DelayDispose = asyncDispose };
        using var input = new Probe("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/e.ent'>]><r>&e;</r>");
        var reader = Reader.Create(input, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Ct);
        while (await reader.ReadAsync(Ct)) if (reader.TokenKind == XmlTokenKind.Text) break;
        var stream = Assert.Single(resolver.Streams);
        if (asyncDispose)
        {
            var pending = reader.DisposeAsync(); Assert.False(pending.IsCompleted); await stream.DisposeEntered.Task;
            Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.Throws<InvalidOperationException>(() => reader.Dispose());
            stream.DisposeGate.SetResult(); await pending;
            Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.Disposals);
        }
        else { reader.Dispose(); Assert.Equal(1, stream.Disposals); Assert.Equal(0, stream.AsyncDisposals); }
        reader.Dispose(); await reader.DisposeAsync(); Assert.Equal(1, stream.Disposals + stream.AsyncDisposals);
    }

    [Fact]
    public async Task LiveExternalCursorSurvivesAlternatingSyncAndAsyncReads()
    {
        var resolver = new Resolver { EntityText = "abcdef", EntityChunk = 1, AllowSync = true };
        using var input = new Probe("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/e.ent'>]><r>&e;</r>") { AllowSync = true };
        await using var reader = Reader.Create(input, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Ct);
        var text = new StringBuilder();
        while (await reader.ReadAsync(Ct)) if (reader.TokenKind == XmlTokenKind.Text) { text.Append(reader.ValueSpan); break; }
        var stream = Assert.Single(resolver.Streams); using var previous = new CancellationTokenSource();
        if (await reader.ReadAsync(previous.Token)) text.Append(reader.ValueSpan); previous.Cancel();
        int calls = 0;
        while ((calls++ % 2 == 0) ? reader.Read() : await reader.ReadAsync(Ct)) if (reader.TokenKind == XmlTokenKind.Text) text.Append(reader.ValueSpan);
        Assert.Equal("abcdef", text.ToString()); Assert.True(stream.AsyncReads > 0); Assert.True(stream.SyncReads > 0); Assert.Equal(1, stream.Disposals + stream.AsyncDisposals);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task InputIoFailureIdentityLeavesCallerOpen(int transport)
    {
        var primary = new IOException("input sentinel"); using var input = new Probe("<r/>") { ReadError = primary };
        Exception error;
        if (transport == 0) error = await Assert.ThrowsAsync<IOException>(() => XmlSaxParser.ParseAsync(input, new Recorder(), cancellationToken: Ct));
        else if (transport == 1) { await using var r = Reader.Create(input, cancellationToken: Ct); Assert.True(await r.ReadAsync(Ct)); error = await Assert.ThrowsAsync<IOException>(async () => await r.ReadAsync(Ct)); Assert.Equal(XmlTokenKind.None, r.TokenKind); }
        else error = await Assert.ThrowsAsync<IOException>(() => XmlDocument.LoadAsync(input, cancellationToken: Ct));
        Assert.Same(primary, error); Assert.Equal(0, input.Disposals);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task ExternalReadFailureAndCancellationAwaitCleanupWithoutReplacingPrimary(int transport, bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var primary = new IOException("external read");
        var resolver = new Resolver { DelayRead = true, ReadError = cancel ? null : primary, CleanupError = new IOException("cleanup") };
        using var input = new Probe("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/e.ent'>]><r>&e;</r>");
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
        async Task Work()
        {
            if (transport == 0) await XmlSaxParser.ParseAsync(input, new Recorder(), options, cancellation.Token);
            else if (transport == 1) { await using var r = Reader.Create(input, options, cancellation.Token); while (await r.ReadAsync(Ct)) { } }
            else await XmlDocument.LoadAsync(input, options, cancellation.Token);
        }
        var pending = Work(); await resolver.Opened.Task.WaitAsync(Ct);
        var stream = Assert.Single(resolver.Streams); await stream.ReadEntered.Task.WaitAsync(Ct); Assert.False(pending.IsCompleted);
        if (cancel) cancellation.Cancel(); else stream.ReadGate.SetResult();
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        else Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => pending));
        Assert.Equal(1, stream.AsyncDisposals); Assert.Equal(0, stream.Disposals); Assert.Equal(0, input.Disposals);
    }

    private static string Token(Reader r)
    {
        var b = new StringBuilder(); b.Append(r.TokenKind).Append('|').Append(r.Location).Append('|').Append(r.ValueSpan).Append('|').Append(r.LocalName).Append('|').Append(r.Prefix).Append('|').Append(r.NamespaceUri).Append('|').Append(r.Target).Append('|').Append(r.IsEmptyElement);
        foreach (var a in r.Attributes) { b.Append('|').Append(a.LocalName).Append(':').Append(a.Prefix).Append(':').Append(a.NamespaceUri).Append('=').Append(a.Value).Append(a.IsDefaulted).Append(a.IsValueComplete).Append(a.Location); foreach (var part in a.ValueParts) b.Append(part); }
        foreach (var n in r.NamespaceDeclarations) b.Append('|').Append(n.Prefix).Append('=').Append(n.NamespaceUri).Append(n.Location);
        if (r.TokenKind == XmlTokenKind.Declaration) b.Append(r.Declaration.Version).Append(r.Declaration.EncodingName).Append(r.Declaration.Standalone);
        if (r.TokenKind == XmlTokenKind.StartDtd) b.Append(r.DocumentType.Name).Append(r.DocumentType.SystemId).Append(r.DocumentType.PublicId).Append(r.DocumentType.HasSystemId).Append(r.DocumentType.HasPublicId);
        if (r.TokenKind == XmlTokenKind.DtdDeclaration) { var d = r.DtdDeclaration; b.Append(d.Kind).Append(d.Name).Append(d.ElementName).Append(d.ContentModel).Append(d.AttributeType).Append(d.DefaultKind).Append(d.Value).Append(d.HasValue).Append(d.SystemId).Append(d.HasSystemId).Append(d.PublicId).Append(d.HasPublicId).Append(d.NotationName).Append(d.IsParameter).Append(d.IsExternal); }
        if (r.TokenKind == XmlTokenKind.SkippedEntity) b.Append(r.SkippedEntity.Name).Append(r.SkippedEntity.LexicalReference).Append(r.SkippedEntity.Location);
        b.Append(r.Diagnostic).Append(r.IncompleteAttributeDefault); return b.ToString();
    }
    private static string Tree(XmlNode n)
    {
        var b = new StringBuilder(); b.Append(n.Kind).Append(':').Append(n.LocalName).Append(':').Append(n.NamespaceUri).Append(':').Append(n.Prefix).Append('=').Append(n.Value);
        foreach (var a in n.Attributes) b.Append('|').Append(a.LocalName).Append(a.NamespaceUri).Append(a.Prefix).Append(a.Value);
        foreach (var ns in n.NamespaceDeclarations) b.Append('|').Append(ns.Prefix).Append(ns.Value);
        foreach (var c in n.Children) b.Append('[').Append(Tree(c)).Append(']'); return b.ToString();
    }
    private static string Model(XmlDocument d)
    {
        var m = d.Dtd!; var b = new StringBuilder();
        foreach (var e in m.Elements) b.Append(e.Name).Append(e.Content.Kind).Append(e.Content.Name).Append(e.Content.Occurrence).Append(e.IsExternal).Append(e.Location);
        foreach (var a in m.Attributes) b.Append(a.ElementName).Append(a.Name).Append(a.Type).AppendJoin('|', a.Tokens).Append(a.DefaultKind).Append(a.DefaultValue).Append(a.IsExternal).Append(a.Location);
        foreach (var e in m.GeneralEntities) b.Append(e);
        foreach (var e in m.ParameterEntities) b.Append(e);
        foreach (var n in m.Notations) b.Append(n);
        return b.ToString();
    }

    private sealed class Recorder : XmlSaxHandler
    {
        internal readonly List<string> Events = []; internal readonly List<XmlDiagnostic> Diagnostics = []; internal Exception? TextError;
        public override void StartDocument() => Events.Add("document");
        public override void EndDocument() => Events.Add("end-document");
        public override void StartElement(XmlStartElement e) { Events.Add($"start:{e.LocalName}:{e.Prefix}:{e.NamespaceUri}:{e.IsEmptyElement}:{e.Location}"); foreach (var a in e.Attributes) Events.Add($"attr:{a.LocalName}:{a.Value}:{a.IsDefaulted}:{a.Location}"); }
        public override void EndElement(XmlEndElement e) => Events.Add($"end:{e.LocalName}:{e.Location}");
        public override void Text(ReadOnlySpan<char> text) { if (TextError is not null) throw TextError; Events.Add("text:" + text.ToString()); }
        public override void Comment(ReadOnlySpan<char> text) => Events.Add("comment:" + text.ToString());
        public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> text) => Events.Add("pi:" + target.ToString() + ":" + text.ToString());
        public override void StartCData() => Events.Add("cdata"); public override void EndCData() => Events.Add("/cdata");
        public override void Declaration(XmlDeclaration d) => Events.Add($"decl:{d.Version}:{d.EncodingName}:{d.Standalone}:{d.Location}");
        public override void StartDtd(XmlDocumentTypeView d) => Events.Add($"dtd:{d.Name}:{d.Location}"); public override void EndDtd() => Events.Add("/dtd");
        public override void DtdDeclaration(XmlDtdDeclaration d) => Events.Add($"model:{d.Kind}:{d.Name}:{d.Value}:{d.Location}");
        public override void SkippedEntity(XmlSkippedEntity e) => Events.Add($"skipped:{e.Name}:{e.LexicalReference}:{e.Location}");
        public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault d) => Events.Add("incomplete:" + d);
        public override void Diagnostic(XmlDiagnostic d) { Diagnostics.Add(d); Events.Add(d.ToString()); }
    }
    private sealed class Resolver : XmlResourceResolver
    {
        internal readonly List<Probe> Streams = []; internal Exception? CleanupError;
        internal string EntityText = "β😀"; internal int EntityChunk = 4096; internal bool DelayDispose, AllowSync;
        internal bool DelayRead; internal Exception? ReadError;
        internal readonly TaskCompletionSource Opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override XmlResolvedResource? Resolve(XmlResourceRequest r, CancellationToken ct) => throw new InvalidOperationException("sync resolver");
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest r, CancellationToken ct)
        {
            await Task.Yield(); ct.ThrowIfCancellationRequested();
            var stream = new Probe(r.Uri.AbsolutePath.EndsWith("d.dtd", StringComparison.Ordinal) ? "<!ENTITY e SYSTEM 'e.ent'>" : EntityText, EntityChunk) { CleanupError = CleanupError, DelayDispose = DelayDispose, AllowSync = AllowSync, DelayRead = DelayRead, ReadError = ReadError };
            Streams.Add(stream); Opened.TrySetResult(); return new(stream, r.Uri);
        }
    }
    private sealed class Probe(string xml, int chunk = 4096) : Stream
    {
        private readonly byte[] data = Encoding.UTF8.GetBytes(xml); private int offset;
        internal bool AllowSync, DelayRead, DelayDispose; internal int SyncReads, AsyncReads, Disposals, AsyncDisposals; internal Exception? ReadError, CleanupError;
        internal readonly TaskCompletionSource ReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously), ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource DisposeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously), DisposeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true; public override bool CanWrite => false; public override bool CanSeek => false;
        public override long Length => data.Length; public override long Position { get => offset; set => throw new NotSupportedException(); }
        private int Copy(Span<byte> b) { int n = Math.Min(Math.Min(chunk, b.Length), data.Length - offset); data.AsSpan(offset, n).CopyTo(b); offset += n; return n; }
        public override int Read(Span<byte> b) { SyncReads++; if (!AllowSync) throw new InvalidOperationException("sync input"); return Copy(b); }
        public override int Read(byte[] b, int o, int c) => Read(b.AsSpan(o, c));
        public override async ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default)
        { AsyncReads++; ReadEntered.TrySetResult(); if (DelayRead) { DelayRead = false; await ReadGate.Task.WaitAsync(ct); } else await Task.Yield(); ct.ThrowIfCancellationRequested(); if (ReadError is not null) throw ReadError; return Copy(b.Span); }
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { AsyncDisposals++; DisposeEntered.TrySetResult(); if (DelayDispose) await DisposeGate.Task; else await Task.Yield(); if (CleanupError is not null) throw CleanupError; }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long n) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}

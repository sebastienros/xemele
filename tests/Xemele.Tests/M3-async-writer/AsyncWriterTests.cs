using System.Reflection;
using System.Text;
using Xunit;
using Writer = Xemele.XmlWriter;
namespace Xemele.Tests.M3AsyncWriter;

public sealed class AsyncWriterTests
{
    private static XmlSaveOptions Omit => new() { OmitXmlDeclaration = true };
    private readonly record struct Op(string Kind, string A = "", string B = "", string C = "", string D = "");
    private static async ValueTask Apply(Writer w, Op o)
    {
        switch (o.Kind)
        {
            case "document": await w.WriteStartDocumentAsync(cancellationToken: TestContext.Current.CancellationToken); break;
            case "start": await w.WriteStartElementAsync(o.A, o.B, o.C, cancellationToken: TestContext.Current.CancellationToken); break;
            case "namespace": await w.WriteNamespaceAsync(o.A, o.B, cancellationToken: TestContext.Current.CancellationToken); break;
            case "attribute": await w.WriteAttributeAsync(o.A, o.B, o.C, o.D, cancellationToken: TestContext.Current.CancellationToken); break;
            case "text": await w.WriteTextAsync(o.A.AsMemory(), cancellationToken: TestContext.Current.CancellationToken); break;
            case "cdata": await w.WriteCDataAsync(o.A.AsMemory(), cancellationToken: TestContext.Current.CancellationToken); break;
            case "comment": await w.WriteCommentAsync(o.A.AsMemory(), cancellationToken: TestContext.Current.CancellationToken); break;
            case "pi": await w.WriteProcessingInstructionAsync(o.A, o.B.AsMemory(), cancellationToken: TestContext.Current.CancellationToken); break;
            case "end": await w.WriteEndElementAsync(cancellationToken: TestContext.Current.CancellationToken); break;
            case "flush": await w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken); break;
            default: throw new InvalidOperationException();
        }
    }
    private static void ApplySync(Writer w, Op o)
    {
        switch (o.Kind)
        {
            case "document": w.WriteStartDocument(); break;
            case "start": w.WriteStartElement(o.A, o.B, o.C); break;
            case "namespace": w.WriteNamespace(o.A, o.B); break;
            case "attribute": w.WriteAttribute(o.A, o.B, o.C, o.D); break;
            case "text": w.WriteText(o.A); break;
            case "cdata": w.WriteCData(o.A); break;
            case "comment": w.WriteComment(o.A); break;
            case "pi": w.WriteProcessingInstruction(o.A, o.B); break;
            case "end": w.WriteEndElement(); break;
            case "flush": w.Flush(); break;
        }
    }
    private static async Task<byte[]> Compare(Op[] ops, XmlSaveOptions options)
    {
        using var sync = new MemoryStream(); using (var w = Writer.Create(sync, options, cancellationToken: TestContext.Current.CancellationToken))
        { foreach (var op in ops) ApplySync(w, op); w.WriteEndDocument(); }
        var output = new AsyncOnlyStream(); await using (var w = Writer.Create(output, options, cancellationToken: TestContext.Current.CancellationToken))
        { foreach (var op in ops) await Apply(w, op); await w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken); }
        Assert.Equal(sync.ToArray(), output.Bytes); Assert.InRange(output.MaxWrite, 1, 8192);
        Assert.True(output.CanWrite); Assert.True(output.Writes > 0);
        return output.Bytes;
    }
    [Theory]
    [InlineData(65001, false)] [InlineData(65001, true)]
    [InlineData(1200, false)] [InlineData(1200, true)]
    [InlineData(1201, false)] [InlineData(1201, true)]
    public async Task DelayedTransportMatchesEncodingBomAndLexicalGolden(int page, bool bom)
    {
        string text = new string('a', 2047) + "😀α" + new string('b', 20000) + "]]";
        var options = new XmlSaveOptions { Encoding = Encoding.GetEncoding(page), EmitByteOrderMark = bom };
        byte[] result = await Compare([new("document"), new("comment", "p"), new("pi", "go", "yes"), new("start", "r"),
            new("attribute", "a", "\"<&\t\n\r"), new("text", text), new("flush"), new("text", ">"),
            new("start", "x"), new("end"), new("cdata"), new("comment", "c"), new("pi", "go"), new("end"), new("comment", "e")], options);
        int skip = bom ? page == 65001 ? 3 : 2 : 0;
        string label = page == 65001 ? "utf-8" : page == 1200 ? "utf-16le" : "utf-16be";
        Assert.Equal($"<?xml version=\"1.0\" encoding=\"{label}\"?><!--p--><?go yes?><r a=\"&quot;&lt;&amp;&#x9;&#xA;&#xD;\">{text}&gt;<x/><![CDATA[]]><!--c--><?go?></r><!--e-->", options.Encoding.GetString(result[skip..]));
    }
    [Fact]
    public async Task PendingBindingsRepairGeneratedPrefixesAndScopeRestoration()
    {
        await Compare([new("start", "r", "u", "p"), new("attribute", "a", "1", "v", "q"), new("attribute", "z", "2"),
            new("namespace", "p", "u"), new("namespace", "q", "v"), new("start", "x", "w", "p"), new("namespace", "p", "w"), new("end"),
            new("start", "y", "u", "p"), new("end"), new("end")], Omit);
        byte[] bytes = await Compare([new("start", "r", "u", "p"), new("namespace", "p", "other"), new("namespace", "z", "a"), new("namespace", "b", "a"),
            new("attribute", "a", "1", "a"), new("attribute", "keep", "3", "k", "ns1"), new("namespace", "ns1", "k"), new("end")], Omit with { NamespaceHandling = XmlNamespaceHandling.Repair });
        Assert.Equal("<ns2:r xmlns:p=\"other\" xmlns:z=\"a\" xmlns:b=\"a\" xmlns:ns1=\"k\" xmlns:ns2=\"u\" b:a=\"1\" ns1:keep=\"3\"/>", Encoding.UTF8.GetString(bytes));
        await Compare([new("start", "r", "u"), new("start", "x"), new("namespace", "", "conflict"), new("end"), new("end")], Omit with { NamespaceHandling = XmlNamespaceHandling.Repair });
    }
    [Fact]
    public async Task IndentationMixedEmptyAndCommitment()
    {
        await Compare([new("comment", "p"), new("start", "r"), new("start", "x"), new("end"), new("comment", "c"), new("end"), new("pi", "e")], Omit with { Indent = true });
        await Compare([new("start", "r"), new("text"), new("start", "x"), new("end"), new("end")], Omit with { Indent = true });
        var output = new AsyncOnlyStream(); await using var w = Writer.Create(output, Omit with { Indent = true }, cancellationToken: TestContext.Current.CancellationToken);
        await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken); await w.WriteCommentAsync("c".AsMemory(), cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteTextAsync("".AsMemory(), cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteCDataAsync("x".AsMemory(), cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.WriteEndElementAsync(cancellationToken: TestContext.Current.CancellationToken); await w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ValidationBeforeMutationAndAllWrongStates()
    {
        var output = new AsyncOnlyStream(); await using var w = Writer.Create(output, Omit, cancellationToken: TestContext.Current.CancellationToken);
        foreach (Op op in new Op[] { new("end"), new("text", "x"), new("cdata"), new("attribute", "a", "1"), new("namespace", "p", "u") })
            await Assert.ThrowsAsync<InvalidOperationException>(() => Apply(w, op).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => w.WriteStartElementAsync("a:b", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.WriteStartDocumentAsync(cancellationToken: TestContext.Current.CancellationToken); await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteStartDocumentAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentNullException>(() => w.WriteAttributeAsync("a", null!, cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => w.WriteNamespaceAsync("xml", "wrong", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => w.WriteNamespaceAsync("p", "", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => w.WriteAttributeAsync("xmlns", "bad", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.WriteNamespaceAsync("p", "u", cancellationToken: TestContext.Current.CancellationToken); await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteNamespaceAsync("p", "v", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.WriteAttributeAsync("a", "1", "u", "p", cancellationToken: TestContext.Current.CancellationToken); await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteAttributeAsync("a", "2", "u", "q", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        foreach (string invalid in new[] { "\ud800", "\udc00", "\u0001", "\ufffe" })
            await Assert.ThrowsAsync<ArgumentException>(() => w.WriteTextAsync(invalid.AsMemory(), cancellationToken: TestContext.Current.CancellationToken).AsTask());
        foreach (Op op in new Op[] { new("cdata", new string('x', 2047) + "]]>"), new("comment", new string('x', 2047) + "--"), new("comment", "x-"), new("pi", "XML"), new("pi", "p", "?>") })
            await Assert.ThrowsAsync<ArgumentException>(() => Apply(w, op).AsTask());
        Assert.Empty(output.Bytes); await w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(1, output.Flushes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteAttributeAsync("b", "2", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteNamespaceAsync("q", "v", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.WriteEndElementAsync(cancellationToken: TestContext.Current.CancellationToken); await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteStartElementAsync("second", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken); await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteCommentAsync("x".AsMemory(), cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(2, output.Flushes); await w.DisposeAsync(); await w.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
    }
    [Fact]
    public async Task StrictConflictFaultAndValidationEmitsNoDelimiters()
    {
        var output = new AsyncOnlyStream(); await using var w = Writer.Create(output, Omit, cancellationToken: TestContext.Current.CancellationToken);
        await w.WriteStartElementAsync("r", "u", cancellationToken: TestContext.Current.CancellationToken); await w.WriteNamespaceAsync("", "u", cancellationToken: TestContext.Current.CancellationToken); await w.WriteStartElementAsync("x", cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteEndElementAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask()); Assert.Empty(output.Bytes);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OperationAndCallCancellationReachPendingIo(bool operation)
    {
        using var op = new CancellationTokenSource(); using var call = new CancellationTokenSource();
        var output = new AsyncOnlyStream { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var w = Writer.Create(output, Omit, op.Token); await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken);
        Task pending = w.WriteTextAsync(new string('x', 30000).AsMemory(), call.Token).AsTask();
        await output.Entered.Task.WaitAsync(TestContext.Current.CancellationToken); Assert.False(pending.IsCompleted);
        if (operation) op.Cancel(); else call.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.True(output.CanWrite); Assert.Empty(output.Bytes);
    }
    [Fact]
    public async Task PrecancelledCallPermanentlyFaultsIdleWriter()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var output = new AsyncOnlyStream(); await using var w = Writer.Create(output, Omit, cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => w.WriteStartElementAsync("r", cancellationToken: cts.Token).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask()); Assert.Empty(output.Bytes);
    }
    [Fact]
    public async Task GuardHeldAcrossAwaitAndPrimaryExceptionIdentity()
    {
        var output = new AsyncOnlyStream { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var w = Writer.Create(output, Omit, cancellationToken: TestContext.Current.CancellationToken); await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken);
        Task pending = w.WriteTextAsync(new string('x', 30000).AsMemory(), cancellationToken: TestContext.Current.CancellationToken).AsTask();
        await output.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(w.Flush); Assert.Throws<InvalidOperationException>(w.Dispose);
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Throws<InvalidOperationException>(() => w.DisposeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.WriteTextAsync("other".AsMemory(), cancellationToken: TestContext.Current.CancellationToken).AsTask());
        output.Failure = new IOException("primary"); output.Gate.SetResult();
        Assert.Same(output.Failure, await Assert.ThrowsAsync<IOException>(() => pending));
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
    }
    [Fact]
    public async Task FlushFailureIdentityAndAbortReleaseExactlyOnceLeaveOpen()
    {
        var output = new AsyncOnlyStream(); var w = Writer.Create(output, new() { EmitByteOrderMark = true }, cancellationToken: TestContext.Current.CancellationToken);
        await w.FlushAsync(cancellationToken: TestContext.Current.CancellationToken); Assert.Empty(output.Bytes); Assert.Equal(1, output.Flushes);
        await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, output.Bytes);
        object scratch = typeof(Writer).GetField("output", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(w)!;
        FieldInfo bytes = scratch.GetType().GetField("Bytes", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.NotNull(bytes.GetValue(scratch)); await w.DisposeAsync(); Assert.Null(bytes.GetValue(scratch));
        w.Dispose(); await w.DisposeAsync(); Assert.Null(bytes.GetValue(scratch)); Assert.Equal(3, output.Bytes.Length); Assert.Equal(1, output.Flushes);
        var broken = new AsyncOnlyStream { FlushFailure = new IOException("flush") }; await using var f = Writer.Create(broken, Omit, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(broken.FlushFailure, await Assert.ThrowsAsync<IOException>(() => f.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.FlushAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
    }
    [Fact]
    public async Task SyncAndAsyncMayAlternateWithoutLosingState()
    {
        using var output = new MemoryStream(); await using var w = Writer.Create(output, Omit, cancellationToken: TestContext.Current.CancellationToken);
        w.WriteStartElement("r"); await w.WriteAttributeAsync("a", "v", cancellationToken: TestContext.Current.CancellationToken); w.WriteText("]]");
        await w.WriteTextAsync(">".AsMemory(), cancellationToken: TestContext.Current.CancellationToken); w.WriteStartElement("x"); await w.WriteEndElementAsync(cancellationToken: TestContext.Current.CancellationToken);
        w.WriteEndElement(); await w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken); Assert.Equal("<r a=\"v\">]]&gt;<x/></r>", Encoding.UTF8.GetString(output.ToArray()));
    }
    [Fact]
    public async Task DeepIterativeTraversalAndLargeAttributeBoundedTransport()
    {
        var output = new AsyncOnlyStream(); await using var w = Writer.Create(output, Omit, cancellationToken: TestContext.Current.CancellationToken);
        for (int i = 0; i < 1000; i++) await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken);
        await w.WriteAttributeAsync("a", new string('α', 100000), cancellationToken: TestContext.Current.CancellationToken);
        for (int i = 0; i < 1000; i++) await w.WriteEndElementAsync(cancellationToken: TestContext.Current.CancellationToken);
        await w.WriteEndDocumentAsync(cancellationToken: TestContext.Current.CancellationToken); Assert.InRange(output.MaxWrite, 1, 8192); Assert.True(output.Writes > 20);
    }
    [Fact]
    public async Task CompletedOperationsReleaseCallerOwnersAndSealedAttributes()
    {
        var output = new AsyncOnlyStream(); await using var w = Writer.Create(output, Omit, TestContext.Current.CancellationToken);
        await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken);
        var owners = await SupplyTransient(w);
        // Leave the completing continuation stack before assessing idle writer retention.
        await Task.Yield();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(owners.Text.IsAlive);
        Assert.False(owners.Attribute.IsAlive);
        await w.WriteEndElementAsync(TestContext.Current.CancellationToken); await w.WriteEndDocumentAsync(TestContext.Current.CancellationToken);
        GC.KeepAlive(w);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference Text, WeakReference Attribute)> SupplyTransient(Writer w)
    {
        string attribute = new('a', 1024 * 1024); await w.WriteAttributeAsync("a", attribute, cancellationToken: TestContext.Current.CancellationToken);
        char[] text = new char[1024 * 1024]; Array.Fill(text, 't'); await w.WriteTextAsync(text.AsMemory(), TestContext.Current.CancellationToken);
        await w.FlushAsync(TestContext.Current.CancellationToken);
        var result = (Text: new WeakReference(text), Attribute: new WeakReference(attribute));
        // Remove the supplier state machine's own references before testing writer retention.
        text = null!; attribute = null!; return result;
    }
    [Fact]
    public async Task StreamCallbackReentrancyRejectsWithoutFaultingOuterOperation()
    {
        var output = new AsyncOnlyStream(); await using var w = Writer.Create(output, Omit, TestContext.Current.CancellationToken);
        int callbacks = 0;
        output.OnWrite = () =>
        {
            Assert.Throws<InvalidOperationException>(w.Flush); Assert.Throws<InvalidOperationException>(w.Dispose);
            ValueTask overlap = w.FlushAsync(TestContext.Current.CancellationToken);
            Assert.True(overlap.IsCompleted); Assert.Throws<InvalidOperationException>(() => overlap.GetAwaiter().GetResult());
            callbacks++;
        };
        await w.WriteStartElementAsync("r", cancellationToken: TestContext.Current.CancellationToken); await w.WriteEndElementAsync(TestContext.Current.CancellationToken);
        await w.WriteEndDocumentAsync(TestContext.Current.CancellationToken); Assert.Equal(1, callbacks); Assert.Equal("<r/>", Encoding.UTF8.GetString(output.Bytes));
    }
    private sealed class AsyncOnlyStream : Stream
    {
        private readonly MemoryStream inner = new();
        internal byte[] Bytes => inner.ToArray(); internal int Writes, Flushes, MaxWrite;
        internal Exception? Failure, FlushFailure; internal Action? OnWrite;
        internal TaskCompletionSource? Gate;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanWrite => true; public override bool CanRead => false; public override bool CanSeek => false;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Writes++; MaxWrite = Math.Max(MaxWrite, buffer.Length); OnWrite?.Invoke(); Entered.TrySetResult();
            await Task.Yield(); if (Gate is not null) await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); if (Failure is not null) throw Failure;
            inner.Write(buffer.Span);
        }
        public override async Task FlushAsync(CancellationToken cancellationToken)
        { Flushes++; await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); if (FlushFailure is not null) throw FlushFailure; }
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("sync write");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("sync write");
        public override void Flush() => throw new InvalidOperationException("sync flush");
        protected override void Dispose(bool disposing) => throw new InvalidOperationException("caller stream disposed");
        public override ValueTask DisposeAsync() => throw new InvalidOperationException("caller stream disposed async");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

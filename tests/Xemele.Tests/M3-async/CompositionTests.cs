using System.Text;
using Xunit;
using Xemele.Streaming;
using Reader = Xemele.Streaming.XmlReader;
using Writer = Xemele.XmlWriter;

namespace Xemele.Tests.AsyncComposition;

public sealed class CompositionTests
{
    private const string Input = "<r xmlns='urn:r' a='β&amp;😀'><c/>tail<![CDATA[界]]><!--end--><?p yes?></r>";
    private const string Body = "<r xmlns=\"urn:r\" a=\"β&amp;😀\"><c/>tail<![CDATA[界]]><!--end--><?p yes?></r>";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(65001, false, false)]
    [InlineData(65001, true, true)]
    [InlineData(1200, false, true)]
    [InlineData(1200, true, false)]
    [InlineData(1201, false, false)]
    [InlineData(1201, true, true)]
    public async Task DelayedDomOutputFeedsEveryInputSurfaceWithIndependentGolden(int page, bool bom, bool indent)
    {
        var source = new InputStream(Encoding.UTF8.GetBytes(Input));
        var document = await XmlDocument.LoadAsync(source, cancellationToken: Ct);
        var options = new XmlSaveOptions { Encoding = Encoding.GetEncoding(page), EmitByteOrderMark = bom, Indent = indent };
        var save = new OutputStream();
        var serialize = new OutputStream();
        await document.SaveAsync(save, options, Ct);
        await XmlAsyncSerializer.SerializeAsync(document, serialize, options, Ct);
        var label = page == 65001 ? "utf-8" : page == 1200 ? "utf-16le" : "utf-16be";
        var golden = $"<?xml version=\"1.0\" encoding=\"{label}\"?>{(indent ? "\n" : "")}{Body}";
        byte[] preamble = !bom ? [] : page == 65001 ? [0xef, 0xbb, 0xbf] : page == 1200 ? [0xff, 0xfe] : [0xfe, 0xff];
        byte[] expected = [.. preamble, .. options.Encoding.GetBytes(golden)];
        Assert.Equal(expected, save.Bytes);
        Assert.Equal(expected, serialize.Bytes);
        var recorder = new Recorder();
        var saxInput = new InputStream(expected);
        await XmlSaxParser.ParseAsync(saxInput, recorder, cancellationToken: Ct);
        Assert.Equal("rc", recorder.Starts.ToString());
        Assert.Equal("cr", recorder.Ends.ToString());
        Assert.Equal((indent ? "\n" : "") + "tail界", recorder.TextValue.ToString());
        Assert.Equal("β&😀", recorder.Attribute);
        Assert.Equal(1, recorder.Completed);
        Assert.Equal(1, recorder.CDataStarts);
        Assert.Equal(1, recorder.CDataEnds);
        var readerInput = new InputStream(expected);
        await using var reader = Reader.Create(readerInput, cancellationToken: Ct);
        var tokens = new List<string>();
        var text = new StringBuilder();
        while (await reader.ReadAsync(Ct))
        {
            Capture(reader, tokens, text);
        }

        Assert.Equal((indent ? "\n" : "") + "tail界", text.ToString());
        Assert.Equal("StartDocument", tokens[0]);
        Assert.Equal("EndDocument", tokens[^1]);
        Assert.False(await reader.ReadAsync(Ct));
        Assert.Equal(XmlTokenKind.None, reader.TokenKind);
        var reloadInput = new InputStream(expected);
        var reloaded = await XmlDocument.LoadAsync(reloadInput, cancellationToken: Ct);
        Assert.Equal(document.ToXmlString(new()
        {
            OmitXmlDeclaration = true
        }, Ct), reloaded.ToXmlString(new()
        {
            OmitXmlDeclaration = true
        }, Ct));
        Assert.True(source.Reads > 1);
        Assert.True(save.Writes > 0);
        Assert.InRange(save.MaxWrite, 1, 8192);
        Assert.Equal(0, save.Flushes);
        Assert.Equal(0, source.Disposals);
        Assert.Equal(0, save.Disposals);
        Assert.True(source.CanRead);
        Assert.True(save.CanWrite);
        Assert.True(saxInput.CanRead);
        Assert.True(readerInput.CanRead);
        Assert.True(reloadInput.CanRead);
    }

    private static void Capture(Reader reader, List<string> tokens, StringBuilder text)
    {
        tokens.Add(reader.TokenKind.ToString());
        if (reader.TokenKind == XmlTokenKind.Text)
        {
            text.Append(reader.ValueSpan);
        }
    }

    [Fact]
    public async Task AllForwardWriterAsyncMembersProduceTheSameConsumableDocument()
    {
        var output = new OutputStream();
        await using var writer = Writer.Create(output, cancellationToken: Ct);
        await writer.WriteStartDocumentAsync(Ct);
        await writer.WriteStartElementAsync("r", "urn:r", cancellationToken: Ct);
        await writer.WriteNamespaceAsync("", "urn:r", Ct);
        await writer.WriteAttributeAsync("a", "β&😀", cancellationToken: Ct);
        await writer.WriteStartElementAsync("c", "urn:r", cancellationToken: Ct);
        await writer.WriteEndElementAsync(Ct);
        await writer.WriteTextAsync("tail".AsMemory(), Ct);
        await writer.WriteCDataAsync("界".AsMemory(), Ct);
        await writer.WriteCommentAsync("end".AsMemory(), Ct);
        await writer.WriteProcessingInstructionAsync("p", "yes".AsMemory(), Ct);
        await writer.WriteEndElementAsync(Ct);
        await writer.WriteEndDocumentAsync(Ct);
        await writer.FlushAsync(Ct);
        Assert.Equal("<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Body, Encoding.UTF8.GetString(output.Bytes));
        var input = new InputStream(output.Bytes);
        var document = await XmlDocument.LoadAsync(input, cancellationToken: Ct);
        var saved = new OutputStream();
        await document.SaveAsync(saved, cancellationToken: Ct);
        Assert.Equal(output.Bytes, saved.Bytes);
        Assert.Equal(1, output.Flushes);
        await writer.DisposeAsync();
        await writer.DisposeAsync();
        Assert.True(output.CanWrite);
        Assert.Equal(0, output.Disposals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InputFailureCrossesAwaitUnchangedAndLeavesCallerOpen(int surface)
    {
        var failure = new IOException("controlled input failure");
        var input = new InputStream(Encoding.UTF8.GetBytes(Input)) { Failure = failure };
        Exception actual;
        if (surface == 0)
        {
            actual = await Assert.ThrowsAsync<IOException>(() => XmlSaxParser.ParseAsync(input, new Recorder(), cancellationToken: Ct));
        }
        else if (surface == 1)
        {
            actual = await Assert.ThrowsAsync<IOException>(() => XmlDocument.LoadAsync(input, cancellationToken: Ct));
        }
        else
        {
            await using var reader = Reader.Create(input, cancellationToken: Ct);
            actual = await Assert.ThrowsAsync<IOException>(async () => { while (await reader.ReadAsync(Ct)) { } });
            Assert.Equal(XmlTokenKind.None, reader.TokenKind);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.ReadAsync(Ct));
        }
        Assert.Same(failure, actual);
        Assert.Equal(0, input.Disposals);
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputFailureCrossesAwaitUnchangedAndLeavesCallerOpen(bool forward)
    {
        var failure = new IOException("controlled output failure");
        var output = new OutputStream { Failure = failure };
        Exception actual;
        if (forward)
        {
            await using var writer = Writer.Create(output, cancellationToken: Ct);
            await writer.WriteStartElementAsync("r", cancellationToken: Ct);
            actual = await Assert.ThrowsAsync<IOException>(async () => { await writer.WriteEndElementAsync(Ct); await writer.WriteEndDocumentAsync(Ct); });
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.WriteEndDocumentAsync(Ct));
        }
        else
        {
            var document = await XmlDocument.LoadAsync(new InputStream(Encoding.UTF8.GetBytes(Input)), cancellationToken: Ct);
            actual = await Assert.ThrowsAsync<IOException>(() => document.SaveAsync(output, cancellationToken: Ct));
        }
        Assert.Same(failure, actual);
        Assert.Equal(0, output.Disposals);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public async Task CancellationDuringActualInputAndOutputAwaitDoesNotCompleteOrCloseCaller()
    {
        using var inputCancellation = new CancellationTokenSource();
        var input = new InputStream(Encoding.UTF8.GetBytes(Input)) { DuringAwait = inputCancellation.Cancel };
        var recorder = new Recorder();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => XmlSaxParser.ParseAsync(input, recorder, cancellationToken: inputCancellation.Token));
        Assert.Equal(0, recorder.Completed);
        Assert.Equal(0, input.Disposals);
        var document = await XmlDocument.LoadAsync(new InputStream(Encoding.UTF8.GetBytes(Input)), cancellationToken: Ct);
        using var outputCancellation = new CancellationTokenSource();
        var output = new OutputStream { DuringAwait = outputCancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => document.SaveAsync(output, cancellationToken: outputCancellation.Token));
        Assert.Equal(0, output.Disposals);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public async Task CallbackFailureRemainsIdenticalAndHandlerNotificationsAreSynchronous()
    {
        var failure = new InvalidOperationException("callback marker");
        var callbacks = new XmlSaxCallbacks { OnStartElement = _ => throw failure };
        var input = new InputStream(Encoding.UTF8.GetBytes(Input));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => XmlSaxParser.ParseAsync(input, callbacks, cancellationToken: Ct)));
        Assert.All(typeof(IXmlSaxHandler).GetMethods(), method => Assert.Equal(typeof(void), method.ReturnType));
        Assert.All(typeof(XmlSaxCallbacks).GetProperties(), property => Assert.Equal(typeof(void), property.PropertyType.GetMethod("Invoke")!.ReturnType));
        Assert.Equal(0, input.Disposals);
    }

    private sealed class Recorder : XmlSaxHandler
    {
        internal readonly StringBuilder Starts = new(), Ends = new(), TextValue = new();
        internal string? Attribute; internal int Completed, CDataStarts, CDataEnds;
        public override void StartElement(XmlStartElement e)
        {
            Starts.Append(e.LocalName);
            if (e.Attributes.Count != 0)
            {
                Attribute = e.Attributes[0].Value.ToString();
            }
        }
        public override void EndElement(XmlEndElement e) => Ends.Append(e.LocalName);
        public override void Text(ReadOnlySpan<char> text) => TextValue.Append(text);
        public override void EndDocument() => Completed++;
        public override void StartCData() => CDataStarts++;
        public override void EndCData() => CDataEnds++;
    }
    private sealed class InputStream(byte[] bytes) : Stream
    {
        private int offset; internal int Reads, Disposals; internal Exception? Failure; internal Action? DuringAwait;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            Reads++;
            await Task.Yield();
            DuringAwait?.Invoke();
            token.ThrowIfCancellationRequested();
            if (Failure is not null)
            {
                throw Failure;
            }

            var count = Math.Min(3, Math.Min(buffer.Length, bytes.Length - offset));
            bytes.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous read forbidden");
        public override int Read(Span<byte> buffer) => throw new InvalidOperationException("Synchronous read forbidden");
        protected override void Dispose(bool disposing)
        {
            Disposals++;
            base.Dispose(disposing);
        }
        public override bool CanRead => true; public override bool CanWrite => false; public override bool CanSeek => false;
        public override long Length => bytes.Length; public override long Position
        {
            get => offset; set => throw new NotSupportedException();
        }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class OutputStream : Stream
    {
        private readonly MemoryStream storage = new(); internal int Writes, Flushes, MaxWrite, Disposals; internal Exception? Failure; internal Action? DuringAwait;
        internal byte[] Bytes => storage.ToArray();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            Writes++;
            MaxWrite = Math.Max(MaxWrite, buffer.Length);
            await Task.Yield();
            DuringAwait?.Invoke();
            token.ThrowIfCancellationRequested();
            if (Failure is not null)
            {
                throw Failure;
            }

            storage.Write(buffer.Span);
        }
        public override async Task FlushAsync(CancellationToken token)
        {
            Flushes++;
            await Task.Yield();
            token.ThrowIfCancellationRequested();
        }
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous write forbidden");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Synchronous write forbidden");
        public override void Flush() => throw new InvalidOperationException("Synchronous flush forbidden");
        protected override void Dispose(bool disposing)
        {
            Disposals++;
            base.Dispose(disposing);
        }
        public override bool CanRead => false; public override bool CanWrite => true; public override bool CanSeek => false;
        public override long Length => storage.Length; public override long Position
        {
            get => storage.Position; set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
    }
}

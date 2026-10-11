using Xemele.Parsing.Dom;
using Xemele.Parsing.Input;
using Xemele.Parsing.Recovery;
using Xemele.Parsing.Syntax;

namespace Xemele.Streaming.Transport;

// DOM keeps the syntax sink's logical text boundaries and full effective DTD
// model. Only fixed decoder scratch and cursor indices survive resource awaits.
internal sealed class AsyncDomInput : IAsyncDisposable
{
    private readonly XmlInputDecoder decoder;
    private readonly XmlParserCore core;
    private readonly DomSink sink;
    private readonly CancellationToken cancellation;
    private readonly char[] characters = new char[4096], prefix = new char[6];
    private readonly long[] mappings = new long[4096], prefixMappings = new long[6];
    private int prefixLength;
    private bool prefixDecided, completed;
    private long byteLength;
    internal XmlDocument Document => sink.Document;
    internal XmlDiagnostic? Diagnostic { get; private set; }

    internal AsyncDomInput(XmlParseOptions options, CancellationToken cancellation, RecoveryDiagnostics diagnostics)
    {
        decoder = new(options);
        core = new(options, asyncResources: true);
        if (options.Recover) core.EnableRecovery(diagnostics);
        sink = new(options, decoder, cancellation);
        this.cancellation = cancellation;
    }

    internal async ValueTask AppendAsync(ReadOnlyMemory<byte> input)
    {
        int offset = 0;
        while (offset < input.Length && Diagnostic is null)
        {
            cancellation.ThrowIfCancellationRequested();
            int length = sink.DeclarationValidated ? input.Length - offset : 1;
            int capacity = sink.DeclarationValidated ? characters.Length : 2;
            var result = decoder.Decode(input.Span.Slice(offset, length), characters.AsSpan(0, capacity), mappings.AsSpan(0, capacity), false, cancellation);
            offset += result.ConsumedBytes;
            byteLength = checked(byteLength + result.ConsumedBytes);
            await ForwardAsync(result.WrittenCharacters).ConfigureAwait(false);
            if (Diagnostic is null && result.Step == DecodeStep.InvalidInput) Diagnostic = result.Diagnostic;
        }
    }

    private async ValueTask ForwardAsync(int count)
    {
        int i = 0;
        while (!prefixDecided && i < count && Diagnostic is null)
        {
            prefix[prefixLength] = characters[i];
            prefixMappings[prefixLength++] = mappings[i++];
            bool mismatch = prefixLength <= 5 ? prefix[prefixLength - 1] != "<?xml"[prefixLength - 1]
                : prefix[5] is not (' ' or '\t' or '\r' or '\n');
            if (!mismatch && prefixLength < 6) continue;
            prefixDecided = true;
            if (mismatch) sink.ValidateAbsentDeclaration(new(null, 1, 1, prefixMappings[0]));
            Diagnostic = sink.Diagnostic;
            if (Diagnostic is null) await FeedAsync(prefix, prefixMappings, 0, prefixLength, false).ConfigureAwait(false);
        }
        if (Diagnostic is null && prefixDecided && i < count)
            await FeedAsync(characters, mappings, i, count - i, false).ConfigureAwait(false);
    }

    private async ValueTask FeedAsync(char[] text, long[] offsets, int start, int count, bool eof)
    {
        int consumed = 0;
        while (Diagnostic is null)
        {
            var result = core.Append(text.AsSpan(start + consumed, count - consumed), offsets.AsSpan(start + consumed, count - consumed), eof, sink, cancellation);
            consumed += result.Consumed;
            Diagnostic = sink.Diagnostic ?? result.Diagnostic;
            if (Diagnostic is not null) return;
            if (result.Step == ScanStep.NeedResource)
                await core.AdvanceResourceAsync(cancellation).ConfigureAwait(false);
            else if (result.Step != ScanStep.Paused) return;
        }
    }

    internal async ValueTask CompleteAsync()
    {
        if (completed) throw new InvalidOperationException("Byte source already completed.");
        completed = true;
        if (Diagnostic is not null) return;
        while (true)
        {
            int capacity = sink.DeclarationValidated ? characters.Length : 2;
            var result = decoder.Decode([], characters.AsSpan(0, capacity), mappings.AsSpan(0, capacity), true, cancellation);
            await ForwardAsync(result.WrittenCharacters).ConfigureAwait(false);
            if (Diagnostic is null && result.Step == DecodeStep.InvalidInput) Diagnostic = result.Diagnostic;
            if (Diagnostic is not null) return;
            if (result.Step == DecodeStep.Completed) break;
        }
        if (!prefixDecided)
        {
            prefixDecided = true;
            sink.ValidateAbsentDeclaration(new(null, 1, 1, prefixLength == 0 ? byteLength : prefixMappings[0]));
            Diagnostic = sink.Diagnostic;
            if (Diagnostic is not null) return;
            if (prefixLength != 0) await FeedAsync(prefix, prefixMappings, 0, prefixLength, false).ConfigureAwait(false);
            if (Diagnostic is not null) return;
        }
        core.CompleteByteSource(byteLength);
        await FeedAsync(characters, mappings, 0, 0, true).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try { await core.DisposeAsync().ConfigureAwait(false); }
        finally { decoder.Dispose(); }
    }
}

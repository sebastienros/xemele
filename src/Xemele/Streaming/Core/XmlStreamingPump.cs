using System.Buffers;
using Xemele.Parsing.Input;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Recovery;
using Xemele.Internal.Operations;

namespace Xemele.Streaming;

// The supplied owner remains alive and unmodified until NeedInput. Decoded scratch
// is fixed; retained tag/token/model state is bounded by the core's lexical quotas.
internal sealed partial class XmlStreamingPump : IAsyncXmlStreamingPump
{
    private const int ScratchSize = 4096;
    private readonly XmlParserCore core;
    private readonly XmlInputDecoder? decoder;
    private bool asyncResources;
    private readonly bool characterInput;
    private readonly Uri? sourceUri;
    private readonly RecoveryDiagnostics? recovery;
    private char[] characters = [];
    private long[] mappings = [];
    private readonly char[] prefix = new char[6];
    private readonly long[] prefixMappings = new long[6];
    private ReadOnlyMemory<byte> bytes;
    private ReadOnlyMemory<char> characterOwner;
    private int byteCursor, characterCursor, characterCount, prefixCount, prefixCursor, gate, diagnosticCursor;
    private long byteLength, asciiByteOffset;
    private bool supplied, final, needInput = true, prefixDecided, declarationValidated, decoderDone, byteEndMarked, asciiChunk;
    private bool started, publicPause, terminal, faulted, disposed, diagnosticDelivered, coreCompleted;
    private StreamingStep terminalStep;
    private XmlDiagnostic? decoderError;
    private IXmlStreamingSink? sink;
    private CancellationToken cancellation;
    public XmlDiagnostic? Diagnostic
    {
        get; private set;
    }
    internal IReadOnlyList<XmlDiagnostic> Diagnostics => recovery?.Items ?? (Diagnostic is null ? [] : new[] { Diagnostic });
    internal bool IsWellFormed => Diagnostic is null && (recovery is null || recovery.Items.Count == 0);

    internal XmlStreamingPump(XmlParseOptions options, Uri? sourceUri = null, bool asyncResources = false, bool characterInput = false, bool allowBulkText = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Limits);
        options = options with
        {
            Limits = options.Limits with
            {
            }
        };
        this.sourceUri = sourceUri;
        this.asyncResources = asyncResources;
        this.characterInput = characterInput;
        core = new(options, sourceUri, asyncResources, trustedByteMappings: !characterInput)
        {
            AllowBulkText = allowBulkText
        };
        if (!characterInput)
        {
            decoder = new(options, sourceUri);
            characters = ArrayPool<char>.Shared.Rent(ScratchSize);
            try
            {
                mappings = ArrayPool<long>.Shared.Rent(ScratchSize);
            }
            catch
            {
                ArrayPool<char>.Shared.Return(characters);
                characters = [];
                try
                {
                    core.Dispose();
                }
                finally { decoder.Dispose(); }
                throw;
            }
        }
        else
        {
            prefixDecided = declarationValidated = true;
        }
        if (options.Recover)
        {
            recovery = new(options.Limits.Diagnostics);
            core.EnableRecovery(recovery);
        }
    }

    public void Supply(ReadOnlyMemory<byte> input, bool isFinalBlock)
    {
        Enter();
        try
        {
            CheckSupply();
            if (characterInput)
            {
                throw new InvalidOperationException("This pump takes characters.");
            }

            bytes = input;
            byteCursor = 0;
            supplied = true;
            final = isFinalBlock;
            needInput = false;
        }
        finally { Exit(); }
    }
    // Additive internal character transport avoids an encoding roundtrip for SAX
    // string input and keeps original character locations without byte mappings.
    internal void SupplyCharacters(ReadOnlyMemory<char> input, bool isFinalBlock)
    {
        Enter();
        try
        {
            CheckSupply();
            if (!characterInput)
            {
                throw new InvalidOperationException("This pump takes bytes.");
            }

            characterOwner = input;
            characterCursor = 0;
            supplied = true;
            final = isFinalBlock;
            needInput = false;
        }
        finally { Exit(); }
    }
    private void CheckSupply()
    {
        CheckUsable();
        if (terminal || final || !needInput)
        {
            throw new InvalidOperationException("Supply requires NeedInput before EOF.");
        }
    }
    public StreamingStep Advance(IXmlStreamingSink sink, CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            return AdvanceCore(sink, cancellationToken);
        }
        finally { Exit(); }
    }
    private StreamingStep AdvanceCore(IXmlStreamingSink nextSink, CancellationToken token)
    {
        CheckUsable();
        ArgumentNullException.ThrowIfNull(nextSink);
        if (sink is not null && !ReferenceEquals(sink, nextSink))
        {
            throw new InvalidOperationException("Resume with the same sink.");
        }

        sink = nextSink;
        if (terminal)
        {
            return terminalStep;
        }

        cancellation = token;
        publicPause = false;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!started)
            {
                started = true;
                if (!sink.StartDocument(new(sourceUri, 1, 1, characterInput ? null : 0)))
                {
                    return StreamingStep.Paused;
                }
            }
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (!DrainModel())
                {
                    return StreamingStep.Paused;
                }

                recovery?.Reject(Diagnostic);
                if (!DrainDiagnostics())
                {
                    return StreamingStep.Paused;
                }

                if (Diagnostic is not null)
                {
                    if (!diagnosticDelivered)
                    {
                        diagnosticDelivered = true;
                        if (!sink.Diagnostic(Diagnostic))
                        {
                            return StreamingStep.Paused;
                        }
                    }
                    terminal = true;
                    return terminalStep = StreamingStep.Rejected;
                }
                if (coreCompleted)
                {
                    terminal = true;
                    return terminalStep = StreamingStep.Completed;
                }
                ScanResult result;
                if (characterInput)
                {
                    result = Feed(characterOwner.Span[characterCursor..], [], supplied && final, token);
                    characterCursor += result.Consumed;
                }
                else if (prefixDecided && prefixCursor < prefixCount)
                {
                    result = Feed(prefix.AsSpan(prefixCursor, prefixCount - prefixCursor), prefixMappings.AsSpan(prefixCursor, prefixCount - prefixCursor), false, token);
                    prefixCursor += result.Consumed;
                }
                else if (characterCursor < characterCount)
                {
                    var text = characters.AsSpan(characterCursor, characterCount - characterCursor);
                    result = asciiChunk
                        ? core.AppendAscii(text, asciiByteOffset + characterCursor, this, token)
                        : Feed(text, mappings.AsSpan(characterCursor, characterCount - characterCursor), false, token);
                    characterCursor += result.Consumed;
                }
                else if (decoderError is not null)
                {
                    Diagnostic = decoderError;
                    continue;
                }
                else if (decoderDone)
                {
                    if (!byteEndMarked)
                    {
                        core.CompleteByteSource(byteLength);
                        byteEndMarked = true;
                    }
                    result = Feed([], [], true, token);
                }
                else
                {
                    // Drain pending core notifications/resources before decoding or
                    // returning an owner to the caller. Empty input cannot refill scratch.
                    result = Feed([], [], false, token);
                    if (result.Step == ScanStep.NeedInput)
                    {
                        if (!supplied)
                        {
                            return InputExhausted();
                        }

                        DecodeMore();
                        if (Diagnostic is not null || decoderError is not null || decoderDone || characterCursor < characterCount || prefixDecided && prefixCursor < prefixCount)
                        {
                            continue;
                        }

                        if (byteCursor == bytes.Length && !final)
                        {
                            return InputExhausted();
                        }

                        continue;
                    }
                }
                if (publicPause)
                {
                    return StreamingStep.Paused;
                }

                if (Diagnostic is not null)
                {
                    continue;
                }

                switch (result.Step)
                {
                    case ScanStep.NeedResource:
                        return StreamingStep.NeedResource;
                    case ScanStep.Rejected:
                        Diagnostic = result.Diagnostic;
                        recovery?.Reject(Diagnostic);
                        continue;
                    case ScanStep.Completed:
                        coreCompleted = true;
                        if (!DrainDiagnostics())
                        {
                            return StreamingStep.Paused;
                        }

                        terminal = true;
                        return terminalStep = StreamingStep.Completed;
                    case ScanStep.NeedInput:
                        if (characterInput)
                        {
                            return InputExhausted();
                        }

                        break;
                }
            }
        }
        catch { faulted = true; throw; }
    }
    private ScanResult Feed(ReadOnlySpan<char> text, ReadOnlySpan<long> offsets, bool eof, CancellationToken token) =>
        core.Append(text, offsets, eof, this, token);
    private StreamingStep InputExhausted()
    {
        bytes = default;
        characterOwner = default;
        supplied = false;
        needInput = true;
        return StreamingStep.NeedInput;
    }
    private void DecodeMore()
    {
        var remaining = bytes.Span[byteCursor..];
        var submitted = !declarationValidated && !remaining.IsEmpty ? remaining[..1] : remaining;
        var capacity = declarationValidated ? ScratchSize : 2;
        if (prefixDecided && declarationValidated && !remaining.IsEmpty)
        {
            var ascii = decoder!.DecodeAscii(remaining, characters.AsSpan(0, ScratchSize), out asciiByteOffset, cancellation);
            if (ascii != 0)
            {
                asciiChunk = true;
                byteCursor += ascii;
                byteLength = checked(byteLength + ascii);
                characterCursor = 0;
                characterCount = ascii;
                decoderError = null;
                decoderDone = false;
                return;
            }
        }
        asciiChunk = false;
        var isFinal = final && submitted.Length == remaining.Length && remaining.IsEmpty;
        var decoded = decoder!.Decode(submitted, characters.AsSpan(0, capacity), mappings.AsSpan(0, capacity), isFinal, cancellation);
        if (decoded.Step == DecodeStep.NeedInput && final && byteCursor + decoded.ConsumedBytes == bytes.Length && !isFinal)
        {
            byteCursor += decoded.ConsumedBytes;
            byteLength = checked(byteLength + decoded.ConsumedBytes);
            var endCapacity = Math.Max(capacity - decoded.WrittenCharacters, 2);
            var end = decoder.Decode([], characters.AsSpan(decoded.WrittenCharacters, endCapacity), mappings.AsSpan(decoded.WrittenCharacters, endCapacity), true, cancellation);
            characterCursor = 0;
            characterCount = decoded.WrittenCharacters + end.WrittenCharacters;
            decoderError = end.Diagnostic;
            decoderDone = end.Step == DecodeStep.Completed;
        }
        else
        {
            byteCursor += decoded.ConsumedBytes;
            byteLength = checked(byteLength + decoded.ConsumedBytes);
            characterCursor = 0;
            characterCount = decoded.WrittenCharacters;
            decoderError = decoded.Diagnostic;
            decoderDone = decoded.Step == DecodeStep.Completed;
        }
        while (!prefixDecided && characterCursor < characterCount)
        {
            prefix[prefixCount] = characters[characterCursor];
            prefixMappings[prefixCount++] = mappings[characterCursor++];
            ReadOnlySpan<char> expected = "<?xml";
            var mismatch = prefixCount <= 5 ? prefix[prefixCount - 1] != expected[prefixCount - 1] : prefix[5] is not (' ' or '\t' or '\r' or '\n');
            if (!mismatch && prefixCount < 6)
            {
                continue;
            }

            prefixDecided = true;
            if (mismatch && decoderError is null)
            {
                ValidateDeclaration([], new(sourceUri, 1, 1, prefixMappings[0]));
            }
        }
        if ((decoderDone || decoderError is not null) && !prefixDecided)
        {
            prefixDecided = true;
            if (decoderError is null)
            {
                ValidateDeclaration([], new(sourceUri, 1, 1, prefixCount == 0 ? byteLength : prefixMappings[0]));
            }
        }
    }
    private void ValidateDeclaration(ReadOnlySpan<char> encoding, XmlLocation location)
    {
        declarationValidated = true;
        Diagnostic = decoder!.ValidateDeclaration(encoding, location);
    }
    private bool DrainDiagnostics()
    {
        if (recovery is null)
        {
            return true;
        }

        while (diagnosticCursor < recovery.Items.Count)
        {
            var item = recovery.Items[diagnosticCursor++];
            if (ReferenceEquals(item, Diagnostic))
            {
                diagnosticDelivered = true;
            }

            if (!sink!.Diagnostic(item))
            {
                return false;
            }
        }
        return true;
    }
    private bool Accept(bool value)
    {
        publicPause |= !value;
        return value;
    }
    private void Enter()
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0)
        {
            throw new InvalidOperationException("Concurrent or reentrant pump use.");
        }
    }
    private void Exit() => Volatile.Write(ref gate, 0);
    private void CheckUsable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        {
            throw new InvalidOperationException("Pump is faulted.");
        }
    }
    public void Dispose()
    {
        Enter();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                core.Dispose();
            }
            finally { decoder?.Dispose(); Release(); }
        }
        finally { Exit(); }
    }
    private void Release()
    {
        if (characters.Length != 0)
        {
            ArrayPool<char>.Shared.Return(characters);
            characters = [];
        }
        if (mappings.Length != 0)
        {
            ArrayPool<long>.Shared.Return(mappings);
            mappings = [];
        }
        bytes = default;
        characterOwner = default;
        sink = null;
        Array.Clear(attributeRows);
        Array.Clear(namespaceRows);
        attributeCharsUsed = 0;
        token.Clear();
        model = null;
        documentType = null;
    }
}

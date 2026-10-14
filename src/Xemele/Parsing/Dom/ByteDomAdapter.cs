using System.Buffers;
using Xemele.Parsing.Input;
using Xemele.Parsing.Syntax;
using Xemele.Internal.Operations;

namespace Xemele.Parsing.Dom;

// Fixed setup scratch. No submitted byte/character view survives an operation.
// The six-unit prefix distinguishes <?xml + whitespace from a PI target.
internal sealed partial class ByteDomAdapter : IDisposable
{
    private readonly XmlInputDecoder decoder;
    private readonly XmlParserCore core;
    private readonly DomSink sink;
    private readonly CancellationToken cancellation;
    private readonly Uri? sourceUri;
    private readonly char[] characters;
    private readonly long[] mappings;
    private readonly char[] prefix = new char[6];
    private readonly long[] prefixMappings = new long[6];
    private int prefixLength;
    private bool prefixDecided, completed, disposed;
    private long byteLength;
    internal XmlDocument Document => sink.Document;
    internal XmlDiagnostic? Diagnostic { get; private set; }

    internal ByteDomAdapter(XmlParseOptions options, CancellationToken cancellation,
        OperationBudget? operationBudget = null, Uri? sourceUri = null)
    {
        options = operationBudget?.Options ?? options;
        decoder = new(options, sourceUri);
        core = new(options, sourceUri, operationBudget: operationBudget, trustedByteMappings: true);
        sink = new(options, decoder, cancellation);
        this.cancellation = cancellation;
        this.sourceUri = sourceUri;
        // Scratch belongs to this operation, never to the returned DOM. Cold
        // rents and the shared pool's retained capacity are measured separately.
        characters = ArrayPool<char>.Shared.Rent(4096);
        try { mappings = ArrayPool<long>.Shared.Rent(4096); }
        catch
        {
            ArrayPool<char>.Shared.Return(characters);
            try { core.Dispose(); } finally { decoder.Dispose(); }
            throw;
        }
    }
    internal void Append(ReadOnlySpan<byte> input)
    {
        if (byteLength == 0) sink.PrepareInput(input);
        while (!input.IsEmpty && Diagnostic is null)
        {
            cancellation.ThrowIfCancellationRequested();
            if (prefixDecided && sink.DeclarationValidated)
            {
                int ascii = decoder.DecodeAscii(input, characters.AsSpan(0, 4096), out long firstByteOffset, cancellation);
                if (ascii != 0)
                {
                    byteLength = checked(byteLength + ascii);
                    input = input[ascii..];
                    var asciiResult = core.AppendAscii(characters.AsSpan(0, ascii), firstByteOffset, sink, cancellation);
                    Diagnostic = sink.Diagnostic ?? asciiResult.Diagnostic;
                    continue;
                }
            }
            var submitted = sink.DeclarationValidated ? input : input[..1];
            // Two units during framing drain detection carry before another
            // original byte; a scalar pair always fits without being split.
            int capacity = sink.DeclarationValidated ? 4096 : 2;
            var result = decoder.Decode(submitted, characters.AsSpan(0, capacity), mappings.AsSpan(0, capacity), false, cancellation);
            byteLength = checked(byteLength + result.ConsumedBytes);
            input = input[result.ConsumedBytes..];
            // Detection can return a valid prefix together with terminal invalid
            // bytes. A rejected decoder cannot validate that prefix's declaration.
            if (result.Step == DecodeStep.InvalidInput && !sink.DeclarationValidated)
            { Diagnostic = result.Diagnostic; return; }
            Forward(result.WrittenCharacters);
            if (Diagnostic is null && result.Step == DecodeStep.InvalidInput) Diagnostic = result.Diagnostic;
        }
    }
    private void Forward(int count)
    {
        int i = 0;
        while (!prefixDecided && i < count && Diagnostic is null)
        {
            prefix[prefixLength] = characters[i];
            prefixMappings[prefixLength++] = mappings[i++];
            ReadOnlySpan<char> expected = "<?xml";
            bool mismatch = prefixLength <= 5 ? prefix[prefixLength - 1] != expected[prefixLength - 1]
                : prefix[5] is not (' ' or '\t' or '\r' or '\n');
            if (!mismatch && prefixLength < 6) continue;
            prefixDecided = true;
            if (mismatch) sink.ValidateAbsentDeclaration(new(sourceUri, 1, 1, prefixMappings[0]));
            Diagnostic = sink.Diagnostic;
            if (Diagnostic is null) Feed(prefix.AsSpan(0, prefixLength), prefixMappings.AsSpan(0, prefixLength));
        }
        if (Diagnostic is null && prefixDecided && i < count) Feed(characters.AsSpan(i, count - i), mappings.AsSpan(i, count - i));
    }
    private void Feed(ReadOnlySpan<char> input, ReadOnlySpan<long> offsets)
    {
        var result = core.Append(input, offsets, false, sink, cancellation);
        Diagnostic = sink.Diagnostic ?? result.Diagnostic;
    }
    internal void Complete()
    {
        if (completed) throw new InvalidOperationException("Byte source already completed.");
        completed = true;
        if (Diagnostic is not null) return;
        while (true)
        {
            int capacity = sink.DeclarationValidated ? 4096 : 2;
            var result = decoder.Decode([], characters.AsSpan(0, capacity), mappings.AsSpan(0, capacity), true, cancellation);
            if (result.Step == DecodeStep.InvalidInput && !sink.DeclarationValidated)
            { Diagnostic = result.Diagnostic; return; }
            Forward(result.WrittenCharacters);
            if (Diagnostic is null && result.Step == DecodeStep.InvalidInput) Diagnostic = result.Diagnostic;
            if (Diagnostic is not null) return;
            if (result.Step == DecodeStep.Completed) break;
        }
        if (!prefixDecided)
        {
            prefixDecided = true;
            sink.ValidateAbsentDeclaration(new(sourceUri, 1, 1, prefixLength == 0 ? byteLength : prefixMappings[0]));
            Diagnostic = sink.Diagnostic;
            if (Diagnostic is not null) return;
            if (prefixLength != 0) Feed(prefix.AsSpan(0, prefixLength), prefixMappings.AsSpan(0, prefixLength));
            if (Diagnostic is not null) return;
        }
        // A lexical incomplete declaration rejects in the core, rather than
        // inventing an encoding label. Successful declarations validate in sink.
        core.CompleteByteSource(byteLength);
        var final = core.Append([], [], true, sink, cancellation);
        Diagnostic = sink.Diagnostic ?? final.Diagnostic;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { core.Dispose(); }
        finally
        {
            try { decoder.Dispose(); }
            finally
            {
                ArrayPool<char>.Shared.Return(characters);
                ArrayPool<long>.Shared.Return(mappings);
            }
        }
    }
}

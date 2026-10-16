using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;

namespace Xemele;

public sealed partial class XmlDocument
{
    /// <summary>Parses a complete XML string into an owned document.</summary>
    /// <param name="input">The XML source text.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token that can cancel parsing.</param>
    /// <returns>The complete, well-formed document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Recovery is enabled or the options are invalid.</exception>
    /// <exception cref="XmlParseException">The XML cannot be parsed within the configured policies and budgets.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static XmlDocument Parse(string input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        return Parse(input.AsSpan(), options, cancellationToken);
    }

    /// <summary>Parses complete UTF-16 XML text into an owned document without retaining the input span.</summary>
    /// <param name="input">The XML source characters.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token that can cancel parsing.</param>
    /// <returns>The complete, well-formed document.</returns>
    /// <exception cref="ArgumentException">Recovery is enabled or the options are invalid.</exception>
    /// <exception cref="XmlParseException">The XML cannot be parsed within the configured policies and budgets.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static XmlDocument Parse(ReadOnlySpan<char> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        if (TryParse(input, out var document, out var diagnostics, options, cancellationToken))
        {
            return document!;
        }

        throw new XmlParseException(diagnostics);
    }

    /// <summary>Decodes and parses a complete XML byte sequence into an owned document.</summary>
    /// <param name="input">The encoded XML bytes; the input span is not retained.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token that can cancel decoding and parsing.</param>
    /// <returns>The complete, well-formed document.</returns>
    /// <exception cref="ArgumentException">Recovery is enabled or the options are invalid.</exception>
    /// <exception cref="XmlParseException">Decoding or parsing fails within the configured policies and budgets.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static XmlDocument Parse(ReadOnlySpan<byte> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        if (TryParse(input, out var document, out var diagnostics, options, cancellationToken))
        {
            return document!;
        }

        throw new XmlParseException(diagnostics);
    }

    /// <summary>Reads and parses XML bytes from the stream's current position, leaving the stream open.</summary>
    /// <param name="input">The readable XML input stream.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token checked between reads and during parsing.</param>
    /// <returns>The complete, well-formed document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Recovery is enabled or the options are invalid.</exception>
    /// <exception cref="XmlParseException">Decoding or parsing fails within the configured policies and budgets.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static XmlDocument Load(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        => LoadCore(input, options, null, cancellationToken);

    /// <summary>Loads XML from a stream with an explicit source URI for diagnostics and relative resource resolution.</summary>
    /// <param name="input">The readable XML input stream, which remains open.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults.</param>
    /// <param name="sourceUri">The absolute URI identifying the input source.</param>
    /// <param name="cancellationToken">A token checked between reads and during parsing.</param>
    /// <returns>The complete, well-formed document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="sourceUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The source URI is relative, recovery is enabled, or the options are invalid.</exception>
    /// <exception cref="XmlParseException">Decoding or parsing fails within the configured policies and budgets.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static XmlDocument Load(Stream input, XmlParseOptions? options, Uri sourceUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        if (!sourceUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Source URI must be absolute.", nameof(sourceUri));
        }

        return LoadCore(input, options, sourceUri, cancellationToken);
    }

    private static XmlDocument LoadCore(Stream input, XmlParseOptions? options, Uri? sourceUri, CancellationToken cancellationToken)
    {
        RejectStrictRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        if (sourceUri is null && TryLoadOptimistic(input, snapshot, cancellationToken) is { } fast)
        {
            return fast;
        }

        using var adapter = new ByteDomAdapter(snapshot, cancellationToken, sourceUri: sourceUri);
        var buffer = new byte[4096];
        while (adapter.Diagnostic is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = input.Read(buffer.AsSpan());
            if (read == 0)
            {
                adapter.Complete();
                break;
            }
            adapter.Append(buffer.AsSpan(0, read));
        }
        if (adapter.Diagnostic is { } diagnostic)
        {
            throw new XmlParseException([diagnostic]);
        }

        return adapter.Document;
    }

    private const long OptimisticStreamLimit = 64L << 20;

    // Seekable streams of bounded size are read whole so the optimistic parser can run; on any
    // bail-out the stream is rewound and the streaming parser re-reads it, so behavior is unchanged.
    private static XmlDocument? TryLoadOptimistic(Stream input, XmlParseOptions snapshot, CancellationToken cancellationToken)
    {
        if (!input.CanSeek || !input.CanRead)
        {
            return null;
        }

        long start = input.Position, remaining = input.Length - start;
        if (remaining <= 0 || remaining > OptimisticStreamLimit)
        {
            return null;
        }

        byte[]? rented = null;
        try
        {
            ReadOnlySpan<byte> bytes;
            if (input is MemoryStream memory && memory.TryGetBuffer(out var segment) && start <= int.MaxValue)
            {
                bytes = segment.AsSpan((int)start, (int)remaining);
            }
            else
            {
                rented = System.Buffers.ArrayPool<byte>.Shared.Rent((int)remaining);
                var total = 0;
                while (total < remaining)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = input.Read(rented.AsSpan(total, (int)remaining - total));
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (total != remaining || input.ReadByte() >= 0)
                {
                    input.Position = start;
                    return null;
                }
                cancellationToken.ThrowIfCancellationRequested();
                bytes = rented.AsSpan(0, total);
            }
            if (!TryParseUtf8Optimistic(bytes, snapshot, cancellationToken, out var fast))
            {
                input.Position = start;
                return null;
            }
            input.Position = start + remaining;
            return fast;
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Attempts strict parsing of a complete XML string, returning XML failures as diagnostics.</summary>
    /// <param name="input">The XML source text.</param>
    /// <param name="document">Receives the complete document on success, or <see langword="null"/> on failure.</param>
    /// <param name="diagnostics">Receives parsing diagnostics, or an empty list on success.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults; recovery must be disabled.</param>
    /// <param name="cancellationToken">A token that can cancel parsing.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Recovery is enabled or the options are invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static bool TryParse(string input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        return TryParse(input.AsSpan(), out document, out diagnostics, options, cancellationToken);
    }

    /// <summary>Attempts strict parsing of complete UTF-16 XML text without retaining the input span.</summary>
    /// <param name="input">The XML source characters.</param>
    /// <param name="document">Receives the complete document on success, or <see langword="null"/> on failure.</param>
    /// <param name="diagnostics">Receives parsing diagnostics, or an empty list on success.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults; recovery must be disabled.</param>
    /// <param name="cancellationToken">A token that can cancel parsing.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Recovery is enabled or the options are invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static bool TryParse(ReadOnlySpan<char> input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        if (TryParseUtf16Optimistic(input, snapshot, cancellationToken, out var fast))
        {
            document = fast;
            diagnostics = [];
            return true;
        }
        using var core = new XmlParserCore(snapshot);
        var sink = new DomSink(snapshot, cancellationToken: cancellationToken);
        var result = core.Append(input, [], true, sink, cancellationToken);
        return Finish(sink.Document, sink.Diagnostic ?? result.Diagnostic, out document, out diagnostics);
    }

    /// <summary>Attempts decoding and strict parsing of complete XML bytes without retaining the input span.</summary>
    /// <param name="input">The encoded XML bytes.</param>
    /// <param name="document">Receives the complete document on success, or <see langword="null"/> on failure.</param>
    /// <param name="diagnostics">Receives decoding or parsing diagnostics, or an empty list on success.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults; recovery must be disabled.</param>
    /// <param name="cancellationToken">A token that can cancel decoding and parsing.</param>
    /// <returns><see langword="true"/> when decoding and parsing succeed; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Recovery is enabled or the options are invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static bool TryParse(ReadOnlySpan<byte> input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        if (TryParseUtf8Optimistic(input, snapshot, cancellationToken, out var fast))
        {
            document = fast;
            diagnostics = [];
            return true;
        }
        using var adapter = new ByteDomAdapter(snapshot, cancellationToken);
        adapter.Append(input);
        if (adapter.Diagnostic is null)
        {
            adapter.Complete();
        }

        return Finish(adapter.Document, adapter.Diagnostic, out document, out diagnostics);
    }

    private static bool Finish(XmlDocument candidate, XmlDiagnostic? diagnostic, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics)
    {
        document = diagnostic is null ? candidate : null;
        diagnostics = diagnostic is null ? Array.Empty<XmlDiagnostic>() : Array.AsReadOnly(new[] { diagnostic });
        return diagnostic is null;
    }
}

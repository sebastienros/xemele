using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;

namespace Xemele;

public sealed partial class XmlDocument
{
    public static XmlDocument Parse(string input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        return Parse(input.AsSpan(), options, cancellationToken);
    }

    public static XmlDocument Parse(ReadOnlySpan<char> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        if (TryParse(input, out var document, out var diagnostics, options, cancellationToken)) return document!;
        throw new XmlParseException(diagnostics);
    }

    public static XmlDocument Parse(ReadOnlySpan<byte> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        if (TryParse(input, out var document, out var diagnostics, options, cancellationToken)) return document!;
        throw new XmlParseException(diagnostics);
    }

    public static XmlDocument Load(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        => LoadCore(input, options, null, cancellationToken);

    public static XmlDocument Load(Stream input, XmlParseOptions? options, Uri sourceUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        if (!sourceUri.IsAbsoluteUri) throw new ArgumentException("Source URI must be absolute.", nameof(sourceUri));
        return LoadCore(input, options, sourceUri, cancellationToken);
    }

    private static XmlDocument LoadCore(Stream input, XmlParseOptions? options, Uri? sourceUri, CancellationToken cancellationToken)
    {
        RejectStrictRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        if (sourceUri is null && TryLoadOptimistic(input, snapshot, cancellationToken) is { } fast) return fast;
        using var adapter = new ByteDomAdapter(snapshot, cancellationToken, sourceUri: sourceUri);
        byte[] buffer = new byte[4096];
        while (adapter.Diagnostic is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = input.Read(buffer.AsSpan());
            if (read == 0) { adapter.Complete(); break; }
            adapter.Append(buffer.AsSpan(0, read));
        }
        if (adapter.Diagnostic is { } diagnostic) throw new XmlParseException([diagnostic]);
        return adapter.Document;
    }

    private const long OptimisticStreamLimit = 64L << 20;

    // Seekable streams of bounded size are read whole so the optimistic parser can run; on any
    // bail-out the stream is rewound and the streaming parser re-reads it, so behavior is unchanged.
    private static XmlDocument? TryLoadOptimistic(Stream input, XmlParseOptions snapshot, CancellationToken cancellationToken)
    {
        if (!input.CanSeek || !input.CanRead) return null;
        long start = input.Position, remaining = input.Length - start;
        if (remaining <= 0 || remaining > OptimisticStreamLimit) return null;
        byte[]? rented = null;
        try
        {
            ReadOnlySpan<byte> bytes;
            if (input is MemoryStream memory && memory.TryGetBuffer(out var segment) && start <= int.MaxValue)
                bytes = segment.AsSpan((int)start, (int)remaining);
            else
            {
                rented = System.Buffers.ArrayPool<byte>.Shared.Rent((int)remaining);
                int total = 0;
                while (total < remaining)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = input.Read(rented.AsSpan(total, (int)remaining - total));
                    if (read == 0) break;
                    total += read;
                }
                if (total != remaining || input.ReadByte() >= 0) { input.Position = start; return null; }
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
            if (rented is not null) System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static bool TryParse(string input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        return TryParse(input.AsSpan(), out document, out diagnostics, options, cancellationToken);
    }

    public static bool TryParse(ReadOnlySpan<char> input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        using var core = new XmlParserCore(snapshot);
        var sink = new DomSink(snapshot, cancellationToken: cancellationToken);
        var result = core.Append(input, [], true, sink, cancellationToken);
        return Finish(sink.Document, sink.Diagnostic ?? result.Diagnostic, out document, out diagnostics);
    }

    public static bool TryParse(ReadOnlySpan<byte> input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        if (TryParseUtf8Optimistic(input, snapshot, cancellationToken, out var fast))
        {
            document = fast;
            diagnostics = Array.Empty<XmlDiagnostic>();
            return true;
        }
        using var adapter = new ByteDomAdapter(snapshot, cancellationToken);
        adapter.Append(input);
        if (adapter.Diagnostic is null) adapter.Complete();
        return Finish(adapter.Document, adapter.Diagnostic, out document, out diagnostics);
    }

    private static bool Finish(XmlDocument candidate, XmlDiagnostic? diagnostic, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics)
    {
        document = diagnostic is null ? candidate : null;
        diagnostics = diagnostic is null ? Array.Empty<XmlDiagnostic>() : Array.AsReadOnly(new[] { diagnostic });
        return diagnostic is null;
    }
}

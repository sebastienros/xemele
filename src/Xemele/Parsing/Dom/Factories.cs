using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;

namespace Xemele;

public sealed partial class XmlDocument
{
    public static XmlDocument Parse(string input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Parse(input.AsSpan(), options, cancellationToken);
    }

    public static XmlDocument Parse(ReadOnlySpan<char> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (TryParse(input, out var document, out var diagnostics, options, cancellationToken)) return document!;
        throw new XmlParseException(diagnostics);
    }

    public static XmlDocument Parse(ReadOnlySpan<byte> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (TryParse(input, out var document, out var diagnostics, options, cancellationToken)) return document!;
        throw new XmlParseException(diagnostics);
    }

    public static XmlDocument Load(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        using var adapter = new ByteDomAdapter(snapshot, cancellationToken);
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

    public static bool TryParse(string input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return TryParse(input.AsSpan(), out document, out diagnostics, options, cancellationToken);
    }

    public static bool TryParse(ReadOnlySpan<char> input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        using var core = new XmlParserCore(snapshot);
        var sink = new DomSink(snapshot);
        var result = core.Append(input, [], true, sink, cancellationToken);
        return Finish(sink.Document, sink.Diagnostic ?? result.Diagnostic, out document, out diagnostics);
    }

    public static bool TryParse(ReadOnlySpan<byte> input, out XmlDocument? document, out IReadOnlyList<XmlDiagnostic> diagnostics, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = new OperationBudget(options, cancellationToken).Options;
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

using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Recovery;
using Xemele.Parsing.Syntax;

namespace Xemele
{
    public sealed partial class XmlDocument
    {
        private static void RejectStrictRecovery(XmlParseOptions? options)
        {
            if (options?.Recover == true) throw new ArgumentException("Strict factories do not admit recovery.", nameof(options));
        }
        public static XmlParseResult<XmlDocument> ParseWithDiagnostics(string input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            return ParseWithDiagnostics(input.AsSpan(), options, cancellationToken);
        }
        public static XmlParseResult<XmlDocument> ParseWithDiagnostics(ReadOnlySpan<char> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            var snapshot = new OperationBudget(options, cancellationToken).Options;
            var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
            using var core = new XmlParserCore(snapshot);
            if (snapshot.Recover) core.EnableRecovery(diagnostics);
            var sink = new DomSink(snapshot, cancellationToken: cancellationToken);
            var result = core.Append(input, [], true, sink, cancellationToken);
            var fatal = sink.Diagnostic ?? result.Diagnostic;
            return RecoveryResult(sink.Document, snapshot.Recover, result.Step == ScanStep.Completed && fatal is null, diagnostics, fatal);
        }
        public static XmlParseResult<XmlDocument> ParseWithDiagnostics(ReadOnlySpan<byte> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            var snapshot = new OperationBudget(options, cancellationToken).Options;
            var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
            using var adapter = new ByteDomAdapter(snapshot, cancellationToken, diagnostics);
            adapter.Append(input);
            if (adapter.Diagnostic is null) adapter.Complete();
            return RecoveryResult(adapter.Document, snapshot.Recover, adapter.Diagnostic is null, diagnostics, adapter.Diagnostic);
        }
        public static XmlParseResult<XmlDocument> LoadWithDiagnostics(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            var snapshot = new OperationBudget(options, cancellationToken).Options;
            var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
            using var adapter = new ByteDomAdapter(snapshot, cancellationToken, diagnostics);
            byte[] buffer = new byte[4096];
            while (adapter.Diagnostic is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = input.Read(buffer.AsSpan());
                if (read == 0) { adapter.Complete(); break; }
                adapter.Append(buffer.AsSpan(0, read));
            }
            return RecoveryResult(adapter.Document, snapshot.Recover, adapter.Diagnostic is null, diagnostics, adapter.Diagnostic);
        }
        private static XmlParseResult<XmlDocument> RecoveryResult(XmlDocument candidate, bool recover, bool complete, RecoveryDiagnostics diagnostics, XmlDiagnostic? fatal)
        {
            diagnostics.Reject(fatal);
            XmlDocument? value = (complete || recover) && candidate.RootElement != default ? candidate : null;
            return new(value, complete, complete && value is not null && diagnostics.Items.Count == 0, diagnostics.Items);
        }
    }
}

namespace Xemele.Parsing.Dom
{
    internal sealed partial class ByteDomAdapter
    {
        internal ByteDomAdapter(XmlParseOptions options, CancellationToken cancellation, RecoveryDiagnostics diagnostics) : this(options, cancellation)
        {
            if (options.Recover) core.EnableRecovery(diagnostics);
        }
    }
}

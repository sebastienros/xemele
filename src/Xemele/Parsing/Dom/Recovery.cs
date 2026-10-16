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
            if (options?.Recover == true)
            {
                throw new ArgumentException("Strict factories do not admit recovery.", nameof(options));
            }
        }
        /// <summary>Parses XML text and returns diagnostics, optionally retaining a recovered document.</summary>
        /// <param name="input">The XML source text.</param>
        /// <param name="options">The parse options; recovery is disabled by default.</param>
        /// <param name="cancellationToken">A token that can cancel parsing.</param>
        /// <returns>The parsing status and diagnostics, with a value when a root element exists and parsing completed or recovery is enabled.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The options are invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public static XmlParseResult<XmlDocument> ParseWithDiagnostics(string input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            return ParseWithDiagnostics(input.AsSpan(), options, cancellationToken);
        }
        /// <summary>Parses complete UTF-16 XML text and returns diagnostics without retaining the input span.</summary>
        /// <param name="input">The XML source characters.</param>
        /// <param name="options">The parse options; recovery is disabled by default.</param>
        /// <param name="cancellationToken">A token that can cancel parsing.</param>
        /// <returns>The parsing status and diagnostics, optionally with a recovered document when recovery is enabled.</returns>
        /// <remarks>A result has a value only if a root element exists and parsing completed or recovery is enabled. Inspect completion and well-formedness before using recovered content.</remarks>
        /// <exception cref="ArgumentException">The options are invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public static XmlParseResult<XmlDocument> ParseWithDiagnostics(ReadOnlySpan<char> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            var snapshot = new OperationBudget(options, cancellationToken).Options;
            var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
            using var core = new XmlParserCore(snapshot);
            if (snapshot.Recover)
            {
                core.EnableRecovery(diagnostics);
            }

            var sink = new DomSink(snapshot, cancellationToken: cancellationToken);
            var result = core.Append(input, [], true, sink, cancellationToken);
            var fatal = sink.Diagnostic ?? result.Diagnostic;
            return RecoveryResult(sink.Document, snapshot.Recover, result.Step == ScanStep.Completed && fatal is null, diagnostics, fatal);
        }
        /// <summary>Decodes complete XML bytes and returns parsing diagnostics without retaining the input span.</summary>
        /// <param name="input">The encoded XML bytes.</param>
        /// <param name="options">The parse options; recovery is disabled by default.</param>
        /// <param name="cancellationToken">A token that can cancel decoding and parsing.</param>
        /// <returns>The parsing status and diagnostics, optionally with a recovered document when recovery is enabled.</returns>
        /// <remarks>A result has a value only if a root element exists and parsing completed or recovery is enabled. Inspect completion and well-formedness before using recovered content.</remarks>
        /// <exception cref="ArgumentException">The options are invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public static XmlParseResult<XmlDocument> ParseWithDiagnostics(ReadOnlySpan<byte> input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            var snapshot = new OperationBudget(options, cancellationToken).Options;
            var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
            using var adapter = new ByteDomAdapter(snapshot, cancellationToken, diagnostics);
            adapter.Append(input);
            if (adapter.Diagnostic is null)
            {
                adapter.Complete();
            }

            return RecoveryResult(adapter.Document, snapshot.Recover, adapter.Diagnostic is null, diagnostics, adapter.Diagnostic);
        }
        /// <summary>Reads XML from the stream's current position and returns diagnostics, leaving the stream open.</summary>
        /// <param name="input">The readable XML input stream.</param>
        /// <param name="options">The parse options; recovery is disabled by default.</param>
        /// <param name="cancellationToken">A token checked between reads and during parsing.</param>
        /// <returns>The parsing status and diagnostics, optionally with a recovered document when recovery is enabled.</returns>
        /// <remarks>A result has a value only if a root element exists and parsing completed or recovery is enabled. Stream I/O errors and cancellation propagate rather than becoming XML diagnostics.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The options are invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public static XmlParseResult<XmlDocument> LoadWithDiagnostics(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            var snapshot = new OperationBudget(options, cancellationToken).Options;
            var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
            using var adapter = new ByteDomAdapter(snapshot, cancellationToken, diagnostics);
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
            return RecoveryResult(adapter.Document, snapshot.Recover, adapter.Diagnostic is null, diagnostics, adapter.Diagnostic);
        }
        private static XmlParseResult<XmlDocument> RecoveryResult(XmlDocument candidate, bool recover, bool complete, RecoveryDiagnostics diagnostics, XmlDiagnostic? fatal)
        {
            if (!complete)
            {
                candidate.MarkSourceIncomplete();
            }

            diagnostics.Reject(fatal);
            var value = (complete || recover) && candidate.RootElement != default ? candidate : null;
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
            if (options.Recover)
            {
                core.EnableRecovery(diagnostics);
            }
        }
    }
}

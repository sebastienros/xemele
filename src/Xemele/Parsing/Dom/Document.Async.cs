using Xemele.Internal.Operations;
using Xemele.Parsing.Recovery;
using Xemele.Streaming.Transport;

namespace Xemele;

public sealed partial class XmlDocument
{
    /// <summary>Asynchronously reads and strictly parses XML from a stream, leaving the stream open.</summary>
    /// <param name="input">The readable XML stream, read from its current position.</param>
    /// <param name="options">The parse options, or <see langword="null"/> for defaults; recovery must be disabled.</param>
    /// <param name="cancellationToken">A token that can cancel asynchronous reads and parsing.</param>
    /// <returns>A task producing the complete, well-formed document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The stream is unreadable, recovery is enabled, or the options are invalid.</exception>
    /// <exception cref="XmlParseException">The XML cannot be decoded or parsed within the configured policies and budgets.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static async Task<XmlDocument> LoadAsync(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        var result = await LoadWithDiagnosticsAsync(input, options, cancellationToken).ConfigureAwait(false);
        if (!result.IsWellFormed)
        {
            throw new XmlParseException(result.Diagnostics);
        }

        return result.Value!;
    }

    /// <summary>Asynchronously reads XML and returns parsing diagnostics, optionally retaining a recovered document.</summary>
    /// <param name="input">The readable XML stream, read from its current position and left open.</param>
    /// <param name="options">The parse options; recovery is disabled by default.</param>
    /// <param name="cancellationToken">A token that can cancel asynchronous reads and parsing.</param>
    /// <returns>A task producing the parsing status, diagnostics, and any usable document.</returns>
    /// <remarks>A result has a value only if a root element exists and parsing completed or recovery is enabled. Stream I/O errors and cancellation propagate rather than becoming XML diagnostics.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The stream is unreadable or the options are invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static async Task<XmlParseResult<XmlDocument>> LoadWithDiagnosticsAsync(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(input));
        }

        var snapshot = new OperationBudget(options, cancellationToken).Options;
        var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
        var adapter = new AsyncDomInput(snapshot, cancellationToken, diagnostics);
        var failed = false;
        try
        {
            var buffer = new byte[4096];
            while (adapter.Diagnostic is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    await adapter.CompleteAsync().ConfigureAwait(false);
                    break;
                }
                await adapter.AppendAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
            }
            return RecoveryResult(adapter.Document, snapshot.Recover, adapter.Diagnostic is null, diagnostics, adapter.Diagnostic);
        }
        catch { failed = true; throw; }
        finally
        {
            if (failed || adapter.Diagnostic is not null)
            {
                try
                {
                    await adapter.DisposeAsync().ConfigureAwait(false);
                }
                catch { }
            }
            else
            {
                await adapter.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

using Xemele.Internal.Operations;
using Xemele.Parsing.Recovery;
using Xemele.Streaming.Transport;

namespace Xemele;

public sealed partial class XmlDocument
{
    public static async Task<XmlDocument> LoadAsync(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectStrictRecovery(options);
        var result = await LoadWithDiagnosticsAsync(input, options, cancellationToken).ConfigureAwait(false);
        if (!result.IsWellFormed) throw new XmlParseException(result.Diagnostics);
        return result.Value!;
    }

    public static async Task<XmlParseResult<XmlDocument>> LoadWithDiagnosticsAsync(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead) throw new ArgumentException("Stream must be readable.", nameof(input));
        var snapshot = new OperationBudget(options, cancellationToken).Options;
        var diagnostics = new RecoveryDiagnostics(snapshot.Limits.Diagnostics);
        var adapter = new AsyncDomInput(snapshot, cancellationToken, diagnostics);
        bool failed = false;
        try
        {
            byte[] buffer = new byte[4096];
            while (adapter.Diagnostic is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (count == 0) { await adapter.CompleteAsync().ConfigureAwait(false); break; }
                await adapter.AppendAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
            }
            return RecoveryResult(adapter.Document, snapshot.Recover, adapter.Diagnostic is null, diagnostics, adapter.Diagnostic);
        }
        catch { failed = true; throw; }
        finally
        {
            if (failed || adapter.Diagnostic is not null) { try { await adapter.DisposeAsync().ConfigureAwait(false); } catch { } }
            else await adapter.DisposeAsync().ConfigureAwait(false);
        }
    }
}

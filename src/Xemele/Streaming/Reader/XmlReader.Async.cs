namespace Xemele.Streaming;

public sealed partial class XmlReader : IAsyncDisposable
{
    public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
    {
        Enter();
        CancellationTokenSource? linked = null;
        try
        {
            current.Clear();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted) throw new InvalidOperationException("Reader is faulted.");
            try
            {
                var token = this.cancellationToken;
                if (cancellationToken.CanBeCanceled)
                {
                    if (token.CanBeCanceled && token != cancellationToken)
                    { linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken); token = linked.Token; }
                    else token = cancellationToken;
                }
                token.ThrowIfCancellationRequested();
                if (completed) return false;
                pump.SetAsyncResources(true);
                while (true)
                {
                    switch (await pump.AdvanceAsync(current, token).ConfigureAwait(false))
                    {
                        case StreamingStep.Paused:
                            if (current.Kind == XmlTokenKind.Diagnostic && pump.Diagnostic is { } terminal) throw new XmlParseException([terminal]);
                            return true;
                        case StreamingStep.Completed: completed = true; return false;
                        case StreamingStep.Rejected: throw new XmlParseException([pump.Diagnostic!]);
                        case StreamingStep.NeedInput:
                            token.ThrowIfCancellationRequested();
                            int count = await input.ReadAsync(inputBuffer.AsMemory(), token).ConfigureAwait(false);
                            pump.Supply(inputBuffer.AsMemory(0, count), count == 0);
                            break;
                        default: throw new InvalidOperationException("Unexpected resource suspension.");
                    }
                }
            }
            catch
            {
                faulted = true; current.Clear();
                try { await pump.DisposeAsync().ConfigureAwait(false); } catch { }
                throw;
            }
        }
        finally { linked?.Dispose(); Exit(); }
    }

    public async ValueTask DisposeAsync()
    {
        Enter();
        try
        {
            if (disposed) return;
            disposed = true; current.Clear();
            try { await pump.DisposeAsync().ConfigureAwait(false); }
            finally { current.Release(); }
        }
        finally { Exit(); }
    }
}

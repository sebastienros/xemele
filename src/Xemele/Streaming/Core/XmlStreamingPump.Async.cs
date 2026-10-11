namespace Xemele.Streaming;

internal sealed partial class XmlStreamingPump
{
    public async ValueTask AdvanceResourceAsync(CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            CheckUsable();
            if (!asyncResources || terminal) throw new InvalidOperationException("No asynchronous resource operation.");
            try { await core.AdvanceResourceAsync(cancellationToken).ConfigureAwait(false); }
            catch { faulted = true; await CleanupFaultAsync().ConfigureAwait(false); throw; }
        }
        finally { Exit(); }
    }
    public async ValueTask<StreamingStep> AdvanceAsync(IXmlStreamingSink sink, CancellationToken cancellationToken)
    {
        Enter();
        try
        {
            try
            {
                while (true)
                {
                    var step = AdvanceCore(sink, cancellationToken);
                    if (step != StreamingStep.NeedResource) return step;
                    await core.AdvanceResourceAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch { faulted = true; await CleanupFaultAsync().ConfigureAwait(false); throw; }
        }
        finally { Exit(); }
    }
    private async ValueTask CleanupFaultAsync()
    {
        // The primary callback/cancellation/I/O exception wins over cleanup.
        try { if (asyncResources) await core.DisposeAsync().ConfigureAwait(false); else core.Dispose(); }
        catch { }
        finally { decoder?.Dispose(); Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        Enter();
        try
        {
            if (disposed) return;
            disposed = true;
            try { await core.DisposeAsync().ConfigureAwait(false); }
            finally { decoder?.Dispose(); Release(); }
        }
        finally { Exit(); }
    }
}

using System.Runtime.ExceptionServices;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    // The adapter selects the next operation's I/O policy at an idle boundary.
    // Live general sources keep their owners, decoder cursors and quota charges.
    internal void SetAsyncResources(bool enabled)
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0)
            throw new InvalidOperationException("Concurrent or reentrant core use.");
        try
        {
            CheckUsable();
            if (resourceWork != ResourceWork.None || asyncDtdParser is not null ||
                asyncDtdPending is not null || asyncDtdDisposal is not null ||
                asyncDtdSources is { Count: > 0 } || asyncFinishDoctype)
                throw new InvalidOperationException("Resource mode cannot change during a resource continuation.");
            asyncResources = enabled;
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    // Dispose is an explicit synchronous abort, even after async advancement.
    // Detach before attempting each owner so throwing cleanup is never retried.
    private void DisposeResourcesSynchronously()
    {
        ExceptionDispatchInfo? first = null;
        if (asyncDtdDisposal is { } pending)
        {
            asyncDtdDisposal = null;
            try { pending.Dispose(); }
            catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
        }
        if (asyncDtdSources is not null)
        {
            while (asyncDtdSources.Count != 0)
            {
                var source = asyncDtdSources[^1];
                asyncDtdSources.RemoveAt(asyncDtdSources.Count - 1);
                try { source.Dispose(); }
                catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
            }
            asyncDtdSources = null;
        }
        try { if (!ReferenceEquals(asyncDtdParser, dtdParser)) asyncDtdParser?.Dispose(); }
        catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
        finally
        {
            asyncDtdParser = null; asyncDtdPending = null;
            asyncFinishDoctype = false; resourceWork = ResourceWork.None;
        }
        try { DisposeEntities(); }
        catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
        if (!faulted && diagnostic is null) first?.Throw();
    }
}

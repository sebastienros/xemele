namespace Xemele;

public sealed partial class XmlDocument
{
    // Holds an immutable XPath snapshot for one content revision. It is weakly
    // referenced so a document that is queried rarely does not retain a copy.
    private WeakReference<object>? xpathSnapshot;

    internal object? XPathSnapshot => xpathSnapshot is { } weak && weak.TryGetTarget(out var target) ? target : null;

    internal void StoreXPathSnapshot(object snapshot)
    {
        // Publish the fully built snapshot to concurrent evaluations of this revision.
        Interlocked.MemoryBarrier();
        if (xpathSnapshot is { } weak)
        {
            weak.SetTarget(snapshot);
        }
        else
        {
            xpathSnapshot = new(snapshot);
        }
    }
}

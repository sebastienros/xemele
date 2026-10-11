namespace Xemele;

public sealed partial class XmlDocument
{
    // Unlike MutationVersion (structural enumerator invalidation), this revision
    // covers every committed notification, including values and prefix changes.
    // -1 permanently disables revision-based acceptance after exhaustion. It is
    // never a usable captured revision: consumers must reject negative values.
    // Notification occurs after commit, so exhaustion cannot throw here or claim
    // that an already committed mutation failed atomically.
    internal long ContentRevision { get; private set; }
    internal bool SourceInputComplete { get; private set; } = true;

    private void AdvanceContentRevision()
    {
        if (ContentRevision < 0) return;
        ContentRevision = ContentRevision == long.MaxValue ? -1 : ContentRevision + 1;
    }

    internal void MarkSourceIncomplete() => SourceInputComplete = false;
}

namespace Xemele;

public sealed partial class XmlDocument
{
    private long? validationSourceContentRevision;

    internal long? ValidationSourceContentRevision => validationSourceContentRevision;

    // A scalar completion stamp, not a lexical snapshot. Consumers require a
    // complete source and a nonnegative stamp equal to the current revision.
    internal void MarkValidationSourceComplete() => validationSourceContentRevision = ContentRevision;
}

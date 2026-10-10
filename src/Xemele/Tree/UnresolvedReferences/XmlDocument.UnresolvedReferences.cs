namespace Xemele;

public sealed partial class XmlDocument
{
    private List<XmlUnresolvedReference>? unresolvedReferences;
    private IReadOnlyList<XmlUnresolvedReference>? unresolvedReferenceView;
    private static readonly IReadOnlyList<XmlUnresolvedReference> NoUnresolvedReferences =
        Array.AsReadOnly(Array.Empty<XmlUnresolvedReference>());

    /// <summary>Owned occurrences in original parse order; mutation does not rewrite provenance.</summary>
    public IReadOnlyList<XmlUnresolvedReference> UnresolvedReferences =>
        unresolvedReferenceView ?? NoUnresolvedReferences;

    internal bool CanAddUnresolvedReference => (unresolvedReferences?.Count ?? 0) < Array.MaxLength;

    internal void AddUnresolvedReference(ReadOnlySpan<char> name, ReadOnlySpan<char> lexical,
        XmlLocation location, XmlNode parent)
    {
        if (!CanAddUnresolvedReference) throw new InvalidOperationException("Unresolved reference capacity exceeded.");
        // Name ownership and its quota are shared with the document's existing intern table.
        string ownedName = FindInternedName(name) ?? new string(name);
        PreflightNames(ownedName);
        ownedName = Intern(ownedName);
        unresolvedReferences ??= [];
        unresolvedReferenceView ??= unresolvedReferences.AsReadOnly();
        ref var data = ref Nodes[parent.Slot];
        unresolvedReferences.Add(new(ownedName, new string(lexical), location, parent,
            Handle(data.Last), data.Count, unresolvedReferences.Count));
    }

    internal void RefuseUnresolvedReferences()
    {
        if (unresolvedReferences is { Count: > 0 } || HasIncompleteAttributes)
            throw new InvalidOperationException("Document contains unresolved entity references.");
    }
}

namespace Xemele;

public sealed partial class XmlDocument
{
    // One owned value per parsed arena entry. Stable slots are never reused;
    // clones/imports and later programmatic entries deliberately have no entry.
    // No lexical buffer, parser, or borrowed callback view survives here.
    private Dictionary<long, XmlLocation>? sourceLocations;
    internal Uri? ParsedSourceUri { get; private set; }

    internal void RecordSourceOrigin(XmlLocation location)
    {
        ParsedSourceUri = location.SourceUri;
        RecordSourceLocation(DocumentNode, location);
    }

    internal void RecordSourceLocation(XmlNode node, XmlLocation location)
    {
        if (!ReferenceEquals(node.Document, this)) throw new ArgumentException("Foreign source node.", nameof(node));
        RecordLocation(0, node.Slot, location);
    }
    internal void RecordSourceLocation(XmlAttribute attribute, XmlLocation location)
    {
        if (!ReferenceEquals(attribute.Document, this)) throw new ArgumentException("Foreign source attribute.", nameof(attribute));
        RecordLocation(1, attribute.Slot, location);
    }
    internal void RecordSourceLocation(XmlNamespaceDeclaration declaration, XmlLocation location)
    {
        if (!ReferenceEquals(declaration.Document, this)) throw new ArgumentException("Foreign source namespace.", nameof(declaration));
        RecordLocation(2, declaration.Slot, location);
    }
    private void RecordLocation(int kind, int slot, XmlLocation location)
        => (sourceLocations ??= new()).Add(((long)kind << 32) | (uint)slot, location);

    internal XmlLocation OriginalLocation(XmlNode node)
    {
        if (!ReferenceEquals(node.Document, this)) throw new ArgumentException("Foreign source node.", nameof(node));
        return OriginalLocation(0, node.Slot);
    }
    internal XmlLocation OriginalLocation(XmlAttribute attribute)
    {
        if (!ReferenceEquals(attribute.Document, this)) throw new ArgumentException("Foreign source attribute.", nameof(attribute));
        return OriginalLocation(1, attribute.Slot);
    }
    internal XmlLocation OriginalLocation(XmlNamespaceDeclaration declaration)
    {
        if (!ReferenceEquals(declaration.Document, this)) throw new ArgumentException("Foreign source namespace.", nameof(declaration));
        return OriginalLocation(2, declaration.Slot);
    }
    private XmlLocation OriginalLocation(int kind, int slot)
    {
        // Coordinates describe the original completed input, not a mutated tree.
        // The existing revision stamp also excludes recovery and partial parses.
        if (SourceInputComplete && ContentRevision >= 0 && ValidationSourceContentRevision == ContentRevision &&
            sourceLocations is not null && sourceLocations.TryGetValue(((long)kind << 32) | (uint)slot, out var location))
            return location;
        return new(ParsedSourceUri, 0, 0, null);
    }
}

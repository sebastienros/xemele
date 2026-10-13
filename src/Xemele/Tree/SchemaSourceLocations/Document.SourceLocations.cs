namespace Xemele;

public sealed partial class XmlDocument
{
    // Dense document-owned sidecars use the same stable slots as the tree.
    // Flags preserve missing entries and nullable offsets without sentinel values.
    // No parser, lexical buffer, or borrowed callback view survives here.
    private struct SourceLocationData
    {
        internal long Offset;
        internal int Line, Position, UriIndex;
        internal byte Flags;
    }
    private SlotArena<SourceLocationData>? nodeLocations, attributeLocations, namespaceLocations;
    private bool locationUriInitialized;
    private Uri? locationUri;
    private Dictionary<Uri, int>? locationUriIndices;
    private List<Uri>? locationUris;
    internal Uri? ParsedSourceUri { get; private set; }

    internal void RecordSourceOrigin(XmlLocation location)
    {
        ParsedSourceUri = location.SourceUri;
        RecordSourceLocation(DocumentNode, location);
    }

    internal void RecordSourceLocation(XmlNode node, XmlLocation location)
    {
        if (!ReferenceEquals(node.Document, this)) throw new ArgumentException("Foreign source node.", nameof(node));
        RecordLocation(ref nodeLocations, node.Slot, location);
    }
    internal void RecordSourceLocation(XmlAttribute attribute, XmlLocation location)
    {
        if (!ReferenceEquals(attribute.Document, this)) throw new ArgumentException("Foreign source attribute.", nameof(attribute));
        RecordLocation(ref attributeLocations, attribute.Slot, location);
    }
    internal void RecordSourceLocation(XmlNamespaceDeclaration declaration, XmlLocation location)
    {
        if (!ReferenceEquals(declaration.Document, this)) throw new ArgumentException("Foreign source namespace.", nameof(declaration));
        RecordLocation(ref namespaceLocations, declaration.Slot, location);
    }
    private int LocationUriIndex(Uri? uri)
    {
        if (!locationUriInitialized) { locationUri = uri; locationUriInitialized = true; }
        if (ReferenceEquals(uri, locationUri)) return 0;
        if (uri is null) return 1;
        if (locationUriIndices is not null && locationUriIndices.TryGetValue(uri, out int index)) return index;
        locationUris ??= new();
        index = checked(locationUris.Count + 2);
        (locationUriIndices ??= new(ReferenceEqualityComparer.Instance)).Add(uri, index);
        locationUris.Add(uri);
        return index;
    }
    private void RecordLocation(ref SlotArena<SourceLocationData>? locations, int slot, XmlLocation location)
    {
        locations ??= new();
        if (slot < locations.Count && locations[slot].Flags != 0) throw new ArgumentException("Duplicate source location.");
        int uriIndex = LocationUriIndex(location.SourceUri);
        locations.EnsureCapacity(checked(slot + 1));
        while (locations.Count <= slot) locations.Add(default);
        locations[slot] = new()
        {
            Offset = location.ByteOffset.GetValueOrDefault(), Line = location.LineNumber,
            Position = location.LinePosition, UriIndex = uriIndex,
            Flags = location.ByteOffset.HasValue ? (byte)3 : (byte)1
        };
    }

    internal XmlLocation OriginalLocation(XmlNode node)
    {
        if (!ReferenceEquals(node.Document, this)) throw new ArgumentException("Foreign source node.", nameof(node));
        return OriginalLocation(nodeLocations, node.Slot);
    }
    internal XmlLocation OriginalLocation(XmlAttribute attribute)
    {
        if (!ReferenceEquals(attribute.Document, this)) throw new ArgumentException("Foreign source attribute.", nameof(attribute));
        return OriginalLocation(attributeLocations, attribute.Slot);
    }
    internal XmlLocation OriginalLocation(XmlNamespaceDeclaration declaration)
    {
        if (!ReferenceEquals(declaration.Document, this)) throw new ArgumentException("Foreign source namespace.", nameof(declaration));
        return OriginalLocation(namespaceLocations, declaration.Slot);
    }
    private XmlLocation OriginalLocation(SlotArena<SourceLocationData>? locations, int slot)
    {
        // Coordinates describe the original completed input, not a mutated tree.
        // The existing revision stamp also excludes recovery and partial parses.
        if (SourceInputComplete && ContentRevision >= 0 && ValidationSourceContentRevision == ContentRevision &&
            locations is not null && (uint)slot < (uint)locations.Count)
        {
            var location = locations[slot];
            if (location.Flags != 0)
                return new(location.UriIndex == 0 ? locationUri : location.UriIndex == 1 ? null : locationUris![location.UriIndex - 2],
                    location.Line, location.Position, (location.Flags & 2) != 0 ? location.Offset : null);
        }
        return new(ParsedSourceUri, 0, 0, null);
    }
}

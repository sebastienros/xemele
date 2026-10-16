namespace Xemele;

public sealed partial class XmlDocument
{
    // Common coordinates use 12-byte slots; unusual URIs, signed coordinates
    // and full-width offsets live in sparse, reference-preserving exceptions.
    // Presence is independent of line zero and nullable byte offsets.
    private const uint LocationPresent = 0x80000000;
    private struct SourceLocationData
    {
        internal uint Line, Position, Offset;
    }
    private SlotArena<SourceLocationData>? nodeLocations, attributeLocations, namespaceLocations;
    private Dictionary<int, XmlLocation>? nodeLocationExceptions, attributeLocationExceptions, namespaceLocationExceptions;
    private bool locationUriInitialized;
    private Uri? locationUri;
    internal Uri? ParsedSourceUri
    {
        get; private set;
    }

    internal void ReserveSourceLocationCapacity(int nodes, int attributes)
    {
        (nodeLocations ??= new()).EnsureCapacity(nodes);
        if (attributes != 0)
        {
            (attributeLocations ??= new()).EnsureCapacity(attributes);
        }
    }
    internal void RecordSourceOrigin(XmlLocation location)
    {
        ParsedSourceUri = location.SourceUri;
        RecordSourceLocation(DocumentNode, location);
    }
    internal void RecordSourceLocation(XmlNode node, XmlLocation location)
    {
        if (!ReferenceEquals(node.Document, this))
        {
            throw new ArgumentException("Foreign source node.", nameof(node));
        }

        RecordLocation(ref nodeLocations, ref nodeLocationExceptions, node.Slot, location);
    }
    internal void RecordSourceLocation(XmlAttribute attribute, XmlLocation location)
    {
        if (!ReferenceEquals(attribute.Document, this))
        {
            throw new ArgumentException("Foreign source attribute.", nameof(attribute));
        }

        RecordLocation(ref attributeLocations, ref attributeLocationExceptions, attribute.Slot, location);
    }
    internal void RecordSourceLocation(XmlNamespaceDeclaration declaration, XmlLocation location)
    {
        if (!ReferenceEquals(declaration.Document, this))
        {
            throw new ArgumentException("Foreign source namespace.", nameof(declaration));
        }

        RecordLocation(ref namespaceLocations, ref namespaceLocationExceptions, declaration.Slot, location);
    }
    private void RecordLocation(ref SlotArena<SourceLocationData>? locations, ref Dictionary<int, XmlLocation>? exceptions,
        int slot, XmlLocation location)
    {
        locations ??= new();
        if (slot < locations.Count && locations[slot].Line != 0)
        {
            throw new ArgumentException("Duplicate source location.");
        }

        if (!locationUriInitialized)
        {
            locationUri = location.SourceUri;
            locationUriInitialized = true;
        }
        var uncommon = !ReferenceEquals(location.SourceUri, locationUri) || location.LineNumber < 0 || location.LinePosition < 0 ||
            location.ByteOffset is long offset && (offset < 0 || offset >= uint.MaxValue);
        locations.EnsureCapacity(checked(slot + 1));
        while (locations.Count <= slot)
        {
            locations.Add(default);
        }

        if (uncommon)
        {
            (exceptions ??= []).Add(slot, location);
            locations[slot] = new()
            {
                Line = LocationPresent
            };
        }
        else
        {
            locations[slot] = new()
            {
                Line = (uint)location.LineNumber | LocationPresent,
                Position = (uint)location.LinePosition,
                Offset = location.ByteOffset.HasValue ? (uint)location.ByteOffset.Value : uint.MaxValue
            };
        }
    }
    internal XmlLocation OriginalLocation(XmlNode node)
    {
        if (!ReferenceEquals(node.Document, this))
        {
            throw new ArgumentException("Foreign source node.", nameof(node));
        }

        return OriginalLocation(nodeLocations, nodeLocationExceptions, node.Slot);
    }
    internal XmlLocation OriginalLocation(XmlAttribute attribute)
    {
        if (!ReferenceEquals(attribute.Document, this))
        {
            throw new ArgumentException("Foreign source attribute.", nameof(attribute));
        }

        return OriginalLocation(attributeLocations, attributeLocationExceptions, attribute.Slot);
    }
    internal XmlLocation OriginalLocation(XmlNamespaceDeclaration declaration)
    {
        if (!ReferenceEquals(declaration.Document, this))
        {
            throw new ArgumentException("Foreign source namespace.", nameof(declaration));
        }

        return OriginalLocation(namespaceLocations, namespaceLocationExceptions, declaration.Slot);
    }
    private XmlLocation OriginalLocation(SlotArena<SourceLocationData>? locations, Dictionary<int, XmlLocation>? exceptions, int slot)
    {
        if (SourceInputComplete && ContentRevision >= 0 && ValidationSourceContentRevision == ContentRevision &&
            locations is not null && (uint)slot < (uint)locations.Count)
        {
            var location = locations[slot];
            if (location.Line != 0)
            {
                if (exceptions is not null && exceptions.TryGetValue(slot, out var exceptional))
                {
                    return exceptional;
                }

                return new(locationUri, (int)(location.Line & ~LocationPresent), (int)location.Position,
                    location.Offset == uint.MaxValue ? null : location.Offset);
            }
        }
        return new(ParsedSourceUri, 0, 0, null);
    }
}

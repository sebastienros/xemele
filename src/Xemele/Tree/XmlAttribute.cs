namespace Xemele;

/// <summary>A stable handle to an attribute owned by an XML document.</summary>
/// <remarks>Equality compares identity, not name or value. A removed attribute retains its identity and original owner element. A default handle supports only identity operations and <see cref="IsDefault"/>.</remarks>
public readonly struct XmlAttribute : IEquatable<XmlAttribute>
{
    internal readonly XmlDocument? Document;
    internal readonly int Slot;
    internal XmlAttribute(XmlDocument document, int slot)
    {
        Document = document;
        Slot = slot;
    }
    private XmlDocument.MemberData Data
    {
        get
        {
            if (Document is null)
            {
                throw new InvalidOperationException("Default handle.");
            }

            return Document.AttributeSlots[Slot];
        }
    }
    /// <summary>Gets whether this handle identifies no attribute.</summary>
    public bool IsDefault => Document is null;
    /// <summary>Gets the document retaining this attribute's storage.</summary>
    public XmlDocument OwnerDocument
    {
        get
        {
            _ = Data;
            return Document!;
        }
    }
    /// <summary>Gets the element that owns this attribute, including after removal.</summary>
    public XmlNode OwnerElement => OwnerDocument.Handle(Data.Element);
    /// <summary>Gets whether this attribute is still attached to its owner element.</summary>
    public bool IsAttached => Data.Attached;
    /// <summary>Gets the lexical prefix, or an empty string for an unprefixed attribute.</summary>
    public string Prefix => Data.Prefix;
    /// <summary>Gets or sets the attribute's character value.</summary>
    /// <exception cref="InvalidOperationException">The handle is default or the value being read contains unresolved entity references.</exception>
    /// <exception cref="ArgumentException">The assigned value contains illegal XML characters.</exception>
    public string Value
    {
        get
        {
            _ = Data;
            return Document!.ReadAttributeValue(Slot);
        }
        set => OwnerDocument.MemberValue(Slot, value, false);
    }
    /// <summary>Detaches this attribute without invalidating its handle; repeated removal has no effect.</summary>
    public void Remove() => OwnerDocument.RemoveMember(Slot, false);
    /// <summary>Gets the attribute's local name.</summary>
    public string LocalName => Data.Local;
    /// <summary>Gets the namespace URI, or an empty string for no namespace.</summary>
    public string NamespaceUri => Data.Uri;
    /// <summary>Determines whether another handle identifies the same attribute.</summary>
    /// <param name="other">The attribute handle to compare.</param>
    /// <returns>Whether the handles identify the same attribute or are both default.</returns>
    public bool Equals(XmlAttribute other) => ReferenceEquals(Document, other.Document) && (Document is null || Slot == other.Slot);
    /// <inheritdoc/>
    public override bool Equals(object? other) => other is XmlAttribute handle && Equals(handle);
    /// <inheritdoc/>
    public override int GetHashCode() => Document is null ? 0 : HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Document), Slot);
    /// <summary>Compares attribute handles for identity equality.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns>Whether the handles identify the same attribute or are both default.</returns>
    public static bool operator ==(XmlAttribute left, XmlAttribute right) => left.Equals(right);
    /// <summary>Compares attribute handles for different identities.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns>Whether the handles have different identities.</returns>
    public static bool operator !=(XmlAttribute left, XmlAttribute right) => !left.Equals(right);
}

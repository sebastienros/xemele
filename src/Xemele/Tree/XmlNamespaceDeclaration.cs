namespace Xemele;

/// <summary>A stable handle to a namespace declaration owned by an XML document.</summary>
/// <remarks>Equality compares identity, not prefix or URI. Removed declarations retain their storage and original owner element. A default handle supports only identity operations and <see cref="IsDefault"/>.</remarks>
public readonly struct XmlNamespaceDeclaration : IEquatable<XmlNamespaceDeclaration>
{
    internal readonly XmlDocument? Document;
    internal readonly int Slot;
    internal XmlNamespaceDeclaration(XmlDocument document, int slot)
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

            return Document.NamespaceSlots[Slot];
        }
    }
    /// <summary>Gets whether this handle identifies no declaration.</summary>
    public bool IsDefault => Document is null;
    /// <summary>Gets the document retaining this declaration's storage.</summary>
    public XmlDocument OwnerDocument
    {
        get
        {
            _ = Data;
            return Document!;
        }
    }
    /// <summary>Gets the element that owns this declaration, including after removal.</summary>
    public XmlNode OwnerElement => OwnerDocument.Handle(Data.Element);
    /// <summary>Gets whether this declaration is still attached to its owner element.</summary>
    public bool IsAttached => Data.Attached;
    /// <summary>Gets the declared prefix, or an empty string for the default namespace.</summary>
    public string Prefix => Data.Prefix;
    /// <summary>Gets or sets the namespace URI bound to the declared prefix.</summary>
    /// <exception cref="ArgumentException">The assigned namespace binding is invalid or reserved.</exception>
    /// <exception cref="InvalidOperationException">The handle is default or the interned-name budget is exceeded.</exception>
    public string Value
    {
        get => Data.Value; set => OwnerDocument.MemberValue(Slot, value, true);
    }
    /// <summary>Detaches this declaration without invalidating its handle; repeated removal has no effect.</summary>
    public void Remove() => OwnerDocument.RemoveMember(Slot, true);
    /// <summary>Determines whether another handle identifies the same declaration.</summary>
    /// <param name="other">The declaration handle to compare.</param>
    /// <returns>Whether the handles identify the same declaration or are both default.</returns>
    public bool Equals(XmlNamespaceDeclaration other) => ReferenceEquals(Document, other.Document) && (Document is null || Slot == other.Slot);
    /// <inheritdoc/>
    public override bool Equals(object? other) => other is XmlNamespaceDeclaration handle && Equals(handle);
    /// <inheritdoc/>
    public override int GetHashCode() => Document is null ? 0 : HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Document), Slot);
    /// <summary>Compares declaration handles for identity equality.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns>Whether the handles identify the same declaration or are both default.</returns>
    public static bool operator ==(XmlNamespaceDeclaration left, XmlNamespaceDeclaration right) => left.Equals(right);
    /// <summary>Compares declaration handles for different identities.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns>Whether the handles have different identities.</returns>
    public static bool operator !=(XmlNamespaceDeclaration left, XmlNamespaceDeclaration right) => !left.Equals(right);
}

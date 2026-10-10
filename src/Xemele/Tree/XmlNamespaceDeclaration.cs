namespace Xemele;
public readonly struct XmlNamespaceDeclaration : IEquatable<XmlNamespaceDeclaration>
{
    internal readonly XmlDocument? Document;
    internal readonly int Slot;
    internal XmlNamespaceDeclaration(XmlDocument document, int slot) { Document = document; Slot = slot; }
    private XmlDocument.MemberData Data { get { if (Document is null) throw new InvalidOperationException("Default handle."); return Document.NamespaceSlots[Slot]; } }
    public bool IsDefault => Document is null;
    public XmlDocument OwnerDocument { get { _ = Data; return Document!; } }
    public XmlNode OwnerElement => OwnerDocument.Handle(Data.Element);
    public bool IsAttached => Data.Attached;
    public string Prefix => Data.Prefix;
    public string Value { get => Data.Value; set => OwnerDocument.MemberValue(Slot, value, true); }
    public void Remove() => OwnerDocument.RemoveMember(Slot, true);
    public bool Equals(XmlNamespaceDeclaration other) => ReferenceEquals(Document, other.Document) && (Document is null || Slot == other.Slot);
    public override bool Equals(object? other) => other is XmlNamespaceDeclaration handle && Equals(handle);
    public override int GetHashCode() => Document is null ? 0 : HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Document), Slot);
    public static bool operator ==(XmlNamespaceDeclaration left, XmlNamespaceDeclaration right) => left.Equals(right);
    public static bool operator !=(XmlNamespaceDeclaration left, XmlNamespaceDeclaration right) => !left.Equals(right);
}

namespace Xemele;
public readonly struct XmlAttribute : IEquatable<XmlAttribute>
{
    internal readonly XmlDocument? Document;
    internal readonly int Slot;
    internal XmlAttribute(XmlDocument document, int slot) { Document = document; Slot = slot; }
    private XmlDocument.MemberData Data { get { if (Document is null) throw new InvalidOperationException("Default handle."); return Document.AttributeSlots[Slot]; } }
    public bool IsDefault => Document is null;
    public XmlDocument OwnerDocument { get { _ = Data; return Document!; } }
    public XmlNode OwnerElement => OwnerDocument.Handle(Data.Element);
    public bool IsAttached => Data.Attached;
    public string Prefix => Data.Prefix;
    public string Value { get { _ = Data; return Document!.ReadAttributeValue(Slot); } set => OwnerDocument.MemberValue(Slot, value, false); }
    public void Remove() => OwnerDocument.RemoveMember(Slot, false);
    public string LocalName => Data.Local;
    public string NamespaceUri => Data.Uri;
    public bool Equals(XmlAttribute other) => ReferenceEquals(Document, other.Document) && (Document is null || Slot == other.Slot);
    public override bool Equals(object? other) => other is XmlAttribute handle && Equals(handle);
    public override int GetHashCode() => Document is null ? 0 : HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Document), Slot);
    public static bool operator ==(XmlAttribute left, XmlAttribute right) => left.Equals(right);
    public static bool operator !=(XmlAttribute left, XmlAttribute right) => !left.Equals(right);
}

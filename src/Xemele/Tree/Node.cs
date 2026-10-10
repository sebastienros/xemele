namespace Xemele;
public readonly struct XmlNode : IEquatable<XmlNode>
{
    internal readonly XmlDocument? Document;
    internal readonly int Slot;
    internal XmlNode(XmlDocument document, int slot) { Document = document; Slot = slot; }
    internal void Require() { if (Document is null) throw new InvalidOperationException("Default node."); }
    private XmlDocument.NodeData Data { get { Require(); return Document!.Nodes[Slot]; } }
    public bool IsDefault => Document is null;
    public XmlDocument OwnerDocument { get { Require(); return Document!; } }
    public XmlNodeKind Kind => Data.Kind;
    public string LocalName => Data.Local;
    public string NamespaceUri => Data.Uri;
    public string Prefix => Data.Prefix;
    public string Value { get => Data.Value; set { Require(); Document!.SetValue(Slot, value); } }
    public XmlNode Parent => OwnerDocument.Handle(Data.Parent);
    public XmlNode FirstChild => OwnerDocument.Handle(Data.First);
    public XmlNode LastChild => OwnerDocument.Handle(Data.Last);
    public XmlNode PreviousSibling => OwnerDocument.Handle(Data.Previous);
    public XmlNode NextSibling => OwnerDocument.Handle(Data.Next);
    public XmlNodeChildren Children { get { Require(); return new(this); } }
    public XmlAttributes Attributes { get { Require(); return new(this); } }
    public XmlNamespaceDeclarations NamespaceDeclarations { get { Require(); return new(this); } }
    public string GetTextContent() => OwnerDocument.TextContent(Slot);
    public void SetTextContent(string value) => OwnerDocument.SetText(Slot, value);
    public string? GetAttribute(string localName, string? namespaceUri = null)
    {
        Require(); ArgumentNullException.ThrowIfNull(localName);
        int i = Document!.Find(Slot, localName, namespaceUri ?? "", false);
        return i < 0 ? null : Document.AttributeSlots[i].Value;
    }
    public XmlAttribute SetAttribute(string localName, string value, string? namespaceUri = null, string? prefix = null) =>
        new(OwnerDocument, OwnerDocument.SetMember(Slot, localName, value, namespaceUri ?? "", prefix ?? "", false));
    public bool RemoveAttribute(string localName, string? namespaceUri = null)
    {
        OwnerDocument.Element(Slot); ArgumentNullException.ThrowIfNull(localName);
        int i = Document!.Find(Slot, localName, namespaceUri ?? "", false);
        if (i < 0) return false; Document.RemoveMember(i, false); return true;
    }
    public XmlNamespaceDeclaration SetNamespaceDeclaration(string prefix, string namespaceUri) =>
        new(OwnerDocument, OwnerDocument.SetMember(Slot, "", namespaceUri, "", prefix, true));
    public bool RemoveNamespaceDeclaration(string prefix)
    {
        OwnerDocument.Element(Slot); ArgumentNullException.ThrowIfNull(prefix);
        int i = Document!.Find(Slot, prefix, "", true);
        if (i < 0) return false; Document.RemoveMember(i, true); return true;
    }
    public void AppendChild(XmlNode child) => OwnerDocument.Insert(Slot, child, default, false);
    public void InsertBefore(XmlNode child, XmlNode referenceChild) => OwnerDocument.Insert(Slot, child, referenceChild, true);
    public void Remove() => OwnerDocument.Remove(Slot);
    public XmlNode Clone(bool deep = true) => OwnerDocument.Clone(this, deep);
    public bool Equals(XmlNode other) => ReferenceEquals(Document, other.Document) && (Document is null || Slot == other.Slot);
    public override bool Equals(object? other) => other is XmlNode handle && Equals(handle);
    public override int GetHashCode() => Document is null ? 0 : HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Document), Slot);
    public static bool operator ==(XmlNode left, XmlNode right) => left.Equals(right);
    public static bool operator !=(XmlNode left, XmlNode right) => !left.Equals(right);
}

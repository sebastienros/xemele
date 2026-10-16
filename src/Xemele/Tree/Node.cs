namespace Xemele;

/// <summary>A stable, document-owned handle to an XML tree node.</summary>
/// <remarks>Equality compares identity, not contents. Except for identity operations and <see cref="IsDefault"/>, a default handle cannot be used.</remarks>
public readonly struct XmlNode : IEquatable<XmlNode>
{
    internal readonly XmlDocument? Document;
    internal readonly int Slot;
    internal XmlNode(XmlDocument document, int slot)
    {
        Document = document;
        Slot = slot;
    }
    internal void Require()
    {
        if (Document is null)
        {
            throw new InvalidOperationException("Default node.");
        }
    }
    private XmlDocument.NodeData Data
    {
        get
        {
            Require();
            return Document!.Nodes[Slot];
        }
    }
    /// <summary>Gets whether this handle has no owning document or node.</summary>
    public bool IsDefault => Document is null;
    /// <summary>Gets the document that owns this node, even when the node is detached.</summary>
    public XmlDocument OwnerDocument
    {
        get
        {
            Require();
            return Document!;
        }
    }
    /// <summary>Gets the node kind.</summary>
    public XmlNodeKind Kind => Data.Kind;
    /// <summary>Gets the local name, the processing-instruction target, or an empty string for unnamed nodes.</summary>
    public string LocalName => Data.Local;
    /// <summary>Gets the namespace URI, or an empty string for nodes without a namespace.</summary>
    public string NamespaceUri => Data.Uri;
    /// <summary>Gets the lexical namespace prefix, or an empty string when unprefixed.</summary>
    public string Prefix => Data.Prefix;
    /// <summary>Gets or sets the node's direct value, without concatenating descendant text.</summary>
    /// <remarks>Container nodes have an empty direct value; use <see cref="GetTextContent"/> to read descendant text.</remarks>
    /// <exception cref="InvalidOperationException">The handle is default, or a container's value is assigned.</exception>
    /// <exception cref="ArgumentException">The assigned value contains illegal XML characters or delimiters for this node kind.</exception>
    public string Value
    {
        get => Data.Value; set
        {
            Require();
            Document!.SetValue(Slot, value);
        }
    }
    /// <summary>Gets the parent, or a default handle if this node is detached or is the document node.</summary>
    public XmlNode Parent => OwnerDocument.Handle(Data.Parent);
    /// <summary>Gets the first child, or a default handle when there are no children.</summary>
    public XmlNode FirstChild => OwnerDocument.Handle(Data.First);
    /// <summary>Gets the last child, or a default handle when there are no children.</summary>
    public XmlNode LastChild => OwnerDocument.Handle(Data.Last);
    /// <summary>Gets the previous sibling, or a default handle when there is none.</summary>
    public XmlNode PreviousSibling => OwnerDocument.Handle(Data.Previous);
    /// <summary>Gets the next sibling, or a default handle when there is none.</summary>
    public XmlNode NextSibling => OwnerDocument.Handle(Data.Next);
    /// <summary>Gets a live, ordered view of this node's children.</summary>
    public XmlNodeChildren Children
    {
        get
        {
            Require();
            return new(this);
        }
    }
    /// <summary>Gets a live, ordered view of this node's attributes, excluding namespace declarations.</summary>
    public XmlAttributes Attributes
    {
        get
        {
            Require();
            return new(this);
        }
    }
    /// <summary>Gets a live, ordered view of namespace declarations directly on this node.</summary>
    public XmlNamespaceDeclarations NamespaceDeclarations
    {
        get
        {
            Require();
            return new(this);
        }
    }
    /// <summary>Reads this node's value or concatenates descendant text and CDATA for a container.</summary>
    /// <returns>The direct value for a non-container; otherwise descendant character data in tree order, excluding comments and processing instructions.</returns>
    /// <exception cref="InvalidOperationException">The handle is default or the owning document contains unresolved content.</exception>
    public string GetTextContent() => OwnerDocument.TextContent(Slot);
    /// <summary>Replaces an element or fragment's children with text, or sets a non-container's value.</summary>
    /// <param name="value">The replacement text; an empty string removes all children from a container.</param>
    /// <exception cref="InvalidOperationException">The handle is default or is a document node.</exception>
    /// <exception cref="ArgumentException">The text is invalid for the node kind.</exception>
    public void SetTextContent(string value) => OwnerDocument.SetText(Slot, value);
    /// <summary>Finds an attribute value by expanded name.</summary>
    /// <param name="localName">The attribute local name.</param>
    /// <param name="namespaceUri">The namespace URI, or <see langword="null"/> for no namespace.</param>
    /// <returns>The attribute value, or <see langword="null"/> if no matching attribute is attached.</returns>
    /// <exception cref="InvalidOperationException">The handle is default or the attribute value contains unresolved references.</exception>
    public string? GetAttribute(string localName, string? namespaceUri = null)
    {
        Require();
        ArgumentNullException.ThrowIfNull(localName);
        var i = Document!.Find(Slot, localName, namespaceUri ?? "", false);
        return i < 0 ? null : Document.ReadAttributeValue(i);
    }
    /// <summary>Adds an attribute or updates the attached attribute with the same expanded name.</summary>
    /// <param name="localName">The attribute local name.</param>
    /// <param name="value">The attribute value containing legal XML characters.</param>
    /// <param name="namespaceUri">The namespace URI, or <see langword="null"/> for no namespace.</param>
    /// <param name="prefix">The lexical prefix, or <see langword="null"/> for no prefix.</param>
    /// <returns>The new or updated attribute handle.</returns>
    /// <exception cref="InvalidOperationException">This is not an element or a resource budget is exceeded.</exception>
    /// <exception cref="ArgumentException">The name, namespace binding, or value is invalid.</exception>
    public XmlAttribute SetAttribute(string localName, string value, string? namespaceUri = null, string? prefix = null) =>
        new(OwnerDocument, OwnerDocument.SetMember(Slot, localName, value, namespaceUri ?? "", prefix ?? "", false));
    /// <summary>Detaches an attribute with the specified expanded name.</summary>
    /// <param name="localName">The attribute local name.</param>
    /// <param name="namespaceUri">The namespace URI, or <see langword="null"/> for no namespace.</param>
    /// <returns><see langword="true"/> if an attribute was removed; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">This handle does not identify an element.</exception>
    public bool RemoveAttribute(string localName, string? namespaceUri = null)
    {
        OwnerDocument.Element(Slot);
        ArgumentNullException.ThrowIfNull(localName);
        var i = Document!.Find(Slot, localName, namespaceUri ?? "", false);
        if (i < 0)
        {
            return false;
        }

        Document.RemoveMember(i, false);
        return true;
    }
    /// <summary>Adds or updates a namespace declaration directly on this element.</summary>
    /// <param name="prefix">The declared prefix, or an empty string for the default namespace.</param>
    /// <param name="namespaceUri">The namespace URI to bind.</param>
    /// <returns>The new or updated declaration.</returns>
    /// <exception cref="InvalidOperationException">This is not an element or a resource budget is exceeded.</exception>
    /// <exception cref="ArgumentException">The namespace binding is invalid or reserved.</exception>
    public XmlNamespaceDeclaration SetNamespaceDeclaration(string prefix, string namespaceUri) =>
        new(OwnerDocument, OwnerDocument.SetMember(Slot, "", namespaceUri, "", prefix, true));
    /// <summary>Detaches a namespace declaration directly on this element.</summary>
    /// <param name="prefix">The declared prefix, or an empty string for the default namespace.</param>
    /// <returns><see langword="true"/> if a declaration was removed; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">This handle does not identify an element.</exception>
    public bool RemoveNamespaceDeclaration(string prefix)
    {
        OwnerDocument.Element(Slot);
        ArgumentNullException.ThrowIfNull(prefix);
        var i = Document!.Find(Slot, prefix, "", true);
        if (i < 0)
        {
            return false;
        }

        Document.RemoveMember(i, true);
        return true;
    }
    /// <summary>Moves a node to the end of this container, or transfers a fragment's children there.</summary>
    /// <param name="child">A node owned by the same document; an inserted fragment is emptied rather than attached.</param>
    /// <exception cref="ArgumentException">The insertion uses a foreign node, creates a cycle, or violates document child constraints.</exception>
    /// <exception cref="InvalidOperationException">A handle is default, this node is not a container, or a storage limit is reached.</exception>
    public void AppendChild(XmlNode child) => OwnerDocument.Insert(Slot, child, default, false);
    /// <summary>Moves a node, or transfers a fragment's children, before a direct child of this container.</summary>
    /// <param name="child">The node or fragment owned by the same document.</param>
    /// <param name="referenceChild">The direct child before which insertion occurs.</param>
    /// <exception cref="ArgumentException">The reference is not a direct child, a node is foreign, or insertion violates tree constraints.</exception>
    /// <exception cref="InvalidOperationException">A handle is default, this node is not a container, or a storage limit is reached.</exception>
    public void InsertBefore(XmlNode child, XmlNode referenceChild) => OwnerDocument.Insert(Slot, child, referenceChild, true);
    /// <summary>Detaches this node from its parent, leaving the node and its descendants usable.</summary>
    /// <remarks>Removing an already detached node has no effect.</remarks>
    /// <exception cref="InvalidOperationException">The handle is default or identifies the document node.</exception>
    public void Remove() => OwnerDocument.Remove(Slot);
    /// <summary>Copies this node, including its attributes and namespace declarations.</summary>
    /// <param name="deep">Whether to copy descendants; the default is <see langword="true"/>.</param>
    /// <returns>A detached copy in the same document, or a new document node when cloning a document.</returns>
    /// <exception cref="InvalidOperationException">The handle is default, its document has unresolved content, or a storage budget is exceeded.</exception>
    public XmlNode Clone(bool deep = true) => OwnerDocument.Clone(this, deep);
    /// <summary>Determines whether two handles identify the same node in the same document.</summary>
    /// <param name="other">The handle to compare.</param>
    /// <returns><see langword="true"/> for identical node identities or two default handles.</returns>
    public bool Equals(XmlNode other) => ReferenceEquals(Document, other.Document) && (Document is null || Slot == other.Slot);
    /// <inheritdoc/>
    public override bool Equals(object? other) => other is XmlNode handle && Equals(handle);
    /// <inheritdoc/>
    public override int GetHashCode() => Document is null ? 0 : HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Document), Slot);
    /// <summary>Compares two node handles for identity equality.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns>Whether both handles identify the same node, or are both default.</returns>
    public static bool operator ==(XmlNode left, XmlNode right) => left.Equals(right);
    /// <summary>Compares two node handles for different identities.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns>Whether the handles have different node identities.</returns>
    public static bool operator !=(XmlNode left, XmlNode right) => !left.Equals(right);
}

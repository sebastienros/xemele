// All spans and collection views expire when their delivering callback returns.
using Xemele;
namespace Xemele.Streaming;

/// <summary>Identifies a declaration in a document type definition.</summary>
public enum XmlDtdDeclarationKind
{
    /// <summary>An element content-model declaration.</summary>
    Element = 0,
    /// <summary>An attribute declaration from an attribute list.</summary>
    Attribute = 1,
    /// <summary>A general or parameter entity declaration.</summary>
    Entity = 2,
    /// <summary>A notation declaration.</summary>
    Notation = 3
}
/// <summary>Identifies the defaulting rule of a DTD attribute declaration.</summary>
public enum XmlDtdDefaultKind
{
    /// <summary>No attribute defaulting rule applies.</summary>
    None = 0,
    /// <summary>The attribute may be omitted and has no default value.</summary>
    Implied = 1,
    /// <summary>The attribute must be explicitly supplied.</summary>
    Required = 2,
    /// <summary>The attribute has a fixed declared value.</summary>
    Fixed = 3,
    /// <summary>The attribute has an ordinary default value.</summary>
    Value = 4
}
/// <summary>A borrowed attribute name, value, and source location.</summary>
/// <remarks>Valid only during its delivering callback or until its reader next reads or is disposed.</remarks>
public readonly ref struct XmlAttributeItem
{
    internal XmlAttributeItem(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, ReadOnlySpan<char> p2, ReadOnlySpan<char> p3, bool p4, bool p5, ReadOnlySpan<XmlAttributeValuePart> p6, XmlLocation p7)
    {
        LocalName = p0;
        Prefix = p1;
        NamespaceUri = p2;
        Value = p3;
        IsDefaulted = p4;
        IsValueComplete = p5;
        ValueParts = p6;
        Location = p7;
    }
    /// <summary>Gets the attribute's local name.</summary>
    public ReadOnlySpan<char> LocalName
    {
        get;
    }
    /// <summary>Gets the lexical prefix, or an empty span for an unprefixed attribute.</summary>
    public ReadOnlySpan<char> Prefix
    {
        get;
    }
    /// <summary>Gets the resolved namespace URI, or an empty span for no namespace.</summary>
    public ReadOnlySpan<char> NamespaceUri
    {
        get;
    }
    /// <summary>Gets the normalized value when complete, or an empty span when expansion is incomplete.</summary>
    public ReadOnlySpan<char> Value
    {
        get;
    }
    /// <summary>Gets whether the attribute was supplied by a DTD default.</summary>
    public bool IsDefaulted
    {
        get;
    }
    /// <summary>Gets whether all references in the attribute value were expanded.</summary>
    public bool IsValueComplete
    {
        get;
    }
    /// <summary>Gets the value parts for incomplete expansion, or an empty span for a complete value.</summary>
    public ReadOnlySpan<XmlAttributeValuePart> ValueParts
    {
        get;
    }
    /// <summary>Gets the attribute's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>A borrowed namespace declaration.</summary>
/// <remarks>Valid only during its delivering callback or until its reader next reads or is disposed.</remarks>
public readonly ref struct XmlNamespaceItem
{
    internal XmlNamespaceItem(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, XmlLocation p2)
    {
        Prefix = p0;
        NamespaceUri = p1;
        Location = p2;
    }
    /// <summary>Gets the declared prefix, or an empty span for the default namespace.</summary>
    public ReadOnlySpan<char> Prefix
    {
        get;
    }
    /// <summary>Gets the namespace URI bound by this declaration.</summary>
    public ReadOnlySpan<char> NamespaceUri
    {
        get;
    }
    /// <summary>Gets the declaration's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>A borrowed view of an element start tag and its attributes and namespace declarations.</summary>
/// <remarks>Valid only during its delivering callback or until its reader next reads or is disposed.</remarks>
public readonly ref struct XmlStartElement
{
    internal readonly string? OwnedLocalName, OwnedPrefix, OwnedNamespaceUri;
    internal XmlStartElement(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, ReadOnlySpan<char> p2, XmlAttributeView p3, XmlNamespaceView p4, bool p5, XmlLocation p6, string? ownedLocal = null, string? ownedPrefix = null, string? ownedUri = null)
    {
        LocalName = p0;
        Prefix = p1;
        NamespaceUri = p2;
        Attributes = p3;
        NamespaceDeclarations = p4;
        IsEmptyElement = p5;
        Location = p6;
        OwnedLocalName = ownedLocal;
        OwnedPrefix = ownedPrefix;
        OwnedNamespaceUri = ownedUri;
    }
    /// <summary>Gets the element's local name.</summary>
    public ReadOnlySpan<char> LocalName
    {
        get;
    }
    /// <summary>Gets the lexical prefix, or an empty span for an unprefixed element.</summary>
    public ReadOnlySpan<char> Prefix
    {
        get;
    }
    /// <summary>Gets the resolved namespace URI.</summary>
    public ReadOnlySpan<char> NamespaceUri
    {
        get;
    }
    /// <summary>Gets the borrowed attributes, excluding namespace declarations.</summary>
    public XmlAttributeView Attributes
    {
        get;
    }
    /// <summary>Gets the borrowed namespace declarations on this start tag.</summary>
    public XmlNamespaceView NamespaceDeclarations
    {
        get;
    }
    /// <summary>Gets whether the source used empty-element syntax.</summary>
    public bool IsEmptyElement
    {
        get;
    }
    /// <summary>Gets the start tag's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>A borrowed view of an element end notification.</summary>
/// <remarks>Valid only during its delivering callback or until its reader next reads or is disposed.</remarks>
public readonly ref struct XmlEndElement
{
    internal readonly string? OwnedLocalName, OwnedPrefix, OwnedNamespaceUri;
    internal XmlEndElement(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, ReadOnlySpan<char> p2, XmlLocation p3, string? ownedLocal = null, string? ownedPrefix = null, string? ownedUri = null)
    {
        LocalName = p0;
        Prefix = p1;
        NamespaceUri = p2;
        Location = p3;
        OwnedLocalName = ownedLocal;
        OwnedPrefix = ownedPrefix;
        OwnedNamespaceUri = ownedUri;
    }
    /// <summary>Gets the element's local name.</summary>
    public ReadOnlySpan<char> LocalName
    {
        get;
    }
    /// <summary>Gets the element's lexical prefix.</summary>
    public ReadOnlySpan<char> Prefix
    {
        get;
    }
    /// <summary>Gets the resolved namespace URI.</summary>
    public ReadOnlySpan<char> NamespaceUri
    {
        get;
    }
    /// <summary>Gets the end notification's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>A borrowed view of an XML declaration.</summary>
/// <remarks>Valid only during its delivering callback or until its reader next reads or is disposed.</remarks>
public readonly ref struct XmlDeclaration
{
    internal XmlDeclaration(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, bool? p2, XmlLocation p3)
    {
        Version = p0;
        EncodingName = p1;
        Standalone = p2;
        Location = p3;
    }
    /// <summary>Gets the declared XML version.</summary>
    public ReadOnlySpan<char> Version
    {
        get;
    }
    /// <summary>Gets the declared encoding name, or an empty span when absent.</summary>
    public ReadOnlySpan<char> EncodingName
    {
        get;
    }
    /// <summary>Gets the standalone declaration, or null when absent.</summary>
    public bool? Standalone
    {
        get;
    }
    /// <summary>Gets the declaration's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>A borrowed view of a document type declaration's header.</summary>
/// <remarks>Valid only during its delivering callback or until its reader next reads or is disposed.</remarks>
public readonly ref struct XmlDocumentTypeView
{
    internal XmlDocumentTypeView(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, ReadOnlySpan<char> p2, bool p3, bool p4, XmlLocation p5)
    {
        Name = p0;
        PublicId = p1;
        SystemId = p2;
        HasPublicId = p3;
        HasSystemId = p4;
        Location = p5;
    }
    /// <summary>Gets the document type name.</summary>
    public ReadOnlySpan<char> Name
    {
        get;
    }
    /// <summary>Gets the public identifier, when present.</summary>
    public ReadOnlySpan<char> PublicId
    {
        get;
    }
    /// <summary>Gets the system identifier, when present.</summary>
    public ReadOnlySpan<char> SystemId
    {
        get;
    }
    /// <summary>Gets whether a public identifier was specified.</summary>
    public bool HasPublicId
    {
        get;
    }
    /// <summary>Gets whether a system identifier was specified.</summary>
    public bool HasSystemId
    {
        get;
    }
    /// <summary>Gets the declaration's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>A borrowed view of an element, attribute, entity, or notation declaration.</summary>
/// <remarks>Fields not applicable to the declaration kind have their default values. The view expires after its callback or the reader's next read or disposal.</remarks>
public readonly ref struct XmlDtdDeclaration
{
    internal XmlDtdDeclaration(XmlDtdDeclarationKind p0, ReadOnlySpan<char> p1, ReadOnlySpan<char> p2, ReadOnlySpan<char> p3, ReadOnlySpan<char> p4, XmlDtdDefaultKind p5, ReadOnlySpan<char> p6, bool p7, ReadOnlySpan<char> p8, bool p9, ReadOnlySpan<char> p10, bool p11, ReadOnlySpan<char> p12, bool p13, bool? p14, XmlLocation p15)
    {
        Kind = p0;
        Name = p1;
        ElementName = p2;
        ContentModel = p3;
        AttributeType = p4;
        DefaultKind = p5;
        Value = p6;
        HasValue = p7;
        SystemId = p8;
        HasSystemId = p9;
        PublicId = p10;
        HasPublicId = p11;
        NotationName = p12;
        IsParameter = p13;
        IsExternal = p14;
        Location = p15;
    }
    /// <summary>Gets the kind of DTD declaration.</summary>
    public XmlDtdDeclarationKind Kind
    {
        get;
    }
    /// <summary>Gets the declared element, attribute, entity, or notation name.</summary>
    public ReadOnlySpan<char> Name
    {
        get;
    }
    /// <summary>Gets the owning element name for an attribute declaration.</summary>
    public ReadOnlySpan<char> ElementName
    {
        get;
    }
    /// <summary>Gets the declared element content model.</summary>
    public ReadOnlySpan<char> ContentModel
    {
        get;
    }
    /// <summary>Gets the declared attribute type.</summary>
    public ReadOnlySpan<char> AttributeType
    {
        get;
    }
    /// <summary>Gets the attribute defaulting rule.</summary>
    public XmlDtdDefaultKind DefaultKind
    {
        get;
    }
    /// <summary>Gets the declared attribute default or internal entity value, when present.</summary>
    public ReadOnlySpan<char> Value
    {
        get;
    }
    /// <summary>Gets whether the declaration supplies a value.</summary>
    public bool HasValue
    {
        get;
    }
    /// <summary>Gets the external system identifier, when present.</summary>
    public ReadOnlySpan<char> SystemId
    {
        get;
    }
    /// <summary>Gets whether a system identifier is present.</summary>
    public bool HasSystemId
    {
        get;
    }
    /// <summary>Gets the external public identifier, when present.</summary>
    public ReadOnlySpan<char> PublicId
    {
        get;
    }
    /// <summary>Gets whether a public identifier is present.</summary>
    public bool HasPublicId
    {
        get;
    }
    /// <summary>Gets the notation name of an unparsed entity.</summary>
    public ReadOnlySpan<char> NotationName
    {
        get;
    }
    /// <summary>Gets whether the declared entity is a parameter entity.</summary>
    public bool IsParameter
    {
        get;
    }
    /// <summary>Gets whether the declaration originated in external DTD content, or null when provenance is not supplied.</summary>
    public bool? IsExternal
    {
        get;
    }
    /// <summary>Gets the declaration's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>A borrowed view of an entity reference that was not expanded.</summary>
/// <remarks>Valid only during its delivering callback or until its reader next reads or is disposed.</remarks>
public readonly ref struct XmlSkippedEntity
{
    internal XmlSkippedEntity(ReadOnlySpan<char> p0, ReadOnlySpan<char> p1, bool p2, XmlLocation p3)
    {
        Name = p0;
        LexicalReference = p1;
        IsParameter = p2;
        Location = p3;
    }
    /// <summary>Gets the referenced entity's name.</summary>
    public ReadOnlySpan<char> Name
    {
        get;
    }
    /// <summary>Gets the lexical entity reference, including its delimiters.</summary>
    public ReadOnlySpan<char> LexicalReference
    {
        get;
    }
    /// <summary>Gets whether the reference names a parameter entity.</summary>
    public bool IsParameter
    {
        get;
    }
    /// <summary>Gets the reference's source location.</summary>
    public XmlLocation Location
    {
        get;
    }
}
// Ordinary owners are retained by the pump; views borrow their storage only during delivery.
internal readonly record struct AttributeRow(string LocalName, string Prefix, string NamespaceUri,
    string? Value, int ValueStart, int ValueLength, bool IsDefaulted, bool IsValueComplete,
    ReadOnlyMemory<XmlAttributeValuePart> ValueParts, XmlLocation Location)
{
    internal AttributeRow(string LocalName, string Prefix, string NamespaceUri, string Value, bool IsDefaulted, bool IsValueComplete, ReadOnlyMemory<XmlAttributeValuePart> ValueParts, XmlLocation Location)
        : this(LocalName, Prefix, NamespaceUri, Value, 0, 0, IsDefaulted, IsValueComplete, ValueParts, Location)
    {
    }
}
internal readonly record struct NamespaceRow(string Prefix, string NamespaceUri, XmlLocation Location);
/// <summary>A borrowed, indexed collection of attributes on a start tag.</summary>
/// <remarks>The collection and its items expire after their callback or the reader's next read or disposal.</remarks>
public readonly ref struct XmlAttributeView
{
    private readonly ReadOnlySpan<AttributeRow> rows;
    private readonly ReadOnlySpan<FastAttribute> fast;
    private readonly ReadOnlySpan<char> chars;
    internal ReadOnlySpan<AttributeRow> Rows => rows;
    internal ReadOnlySpan<char> Characters => chars;
    internal XmlAttributeView(ReadOnlySpan<AttributeRow> rows, ReadOnlySpan<char> chars = default)
    {
        this.rows = rows;
        this.chars = chars;
    }
    internal XmlAttributeView(ReadOnlySpan<FastAttribute> fast, ReadOnlySpan<char> chars)
    {
        this.fast = fast;
        this.chars = chars;
    }
    /// <summary>Gets the number of attributes.</summary>
    public int Count => rows.Length + fast.Length;
    /// <summary>Gets the borrowed attribute at a zero-based index.</summary>
    /// <param name="index">The zero-based attribute index.</param>
    /// <returns>The attribute view.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside this collection.</exception>
    public XmlAttributeItem this[int index] { get { if (!fast.IsEmpty) { if ((uint)index >= (uint)fast.Length) { throw new ArgumentOutOfRangeException(nameof(index)); } ref readonly var a = ref fast[index]; return new(a.Name.Local, a.Name.Prefix, a.NamespaceUri, chars.Slice(a.ValueStart, a.ValueLength), false, true, [], a.Location); } if ((uint)index >= (uint)rows.Length) { throw new ArgumentOutOfRangeException(nameof(index)); } ref readonly var row = ref rows[index]; var value = !row.IsValueComplete ? ReadOnlySpan<char>.Empty : row.Value is not null ? row.Value.AsSpan() : chars.Slice(row.ValueStart, row.ValueLength); return new(row.LocalName, row.Prefix, row.NamespaceUri, value, row.IsDefaulted, row.IsValueComplete, row.IsValueComplete ? [] : row.ValueParts.Span, row.Location); } }
    /// <summary>Creates an enumerator over the borrowed attributes.</summary>
    /// <returns>An enumerator positioned before the first attribute.</returns>
    public Enumerator GetEnumerator() => new(this);
    /// <summary>Enumerates attributes without extending the borrowed collection's lifetime.</summary>
    public ref struct Enumerator
    {
        private readonly XmlAttributeView view;
        private int index;
        internal Enumerator(XmlAttributeView view)
        {
            this.view = view;
            index = -1;
        }
        /// <summary>Gets the attribute at the current position.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The enumerator is not positioned on an attribute.</exception>
        public readonly XmlAttributeItem Current => view[index];
        /// <summary>Advances to the next attribute.</summary>
        /// <returns>True if positioned on an attribute; false after the last attribute.</returns>
        public bool MoveNext()
        {
            if (index < view.Count)
            {
                index++;
            }

            return index < view.Count;
        }
    }
}
/// <summary>A borrowed, indexed collection of namespace declarations on a start tag.</summary>
/// <remarks>The collection and its items expire after their callback or the reader's next read or disposal.</remarks>
public readonly ref struct XmlNamespaceView
{
    private readonly ReadOnlySpan<NamespaceRow> rows;
    internal ReadOnlySpan<NamespaceRow> Rows => rows;
    internal XmlNamespaceView(ReadOnlySpan<NamespaceRow> rows)
    {
        this.rows = rows;
    }
    /// <summary>Gets the number of namespace declarations.</summary>
    public int Count => rows.Length;
    /// <summary>Gets the borrowed namespace declaration at a zero-based index.</summary>
    /// <param name="index">The zero-based declaration index.</param>
    /// <returns>The namespace declaration view.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside this collection.</exception>
    public XmlNamespaceItem this[int index] { get { if ((uint)index >= (uint)rows.Length) { throw new ArgumentOutOfRangeException(nameof(index)); } var row = rows[index]; return new(row.Prefix, row.NamespaceUri, row.Location); } }
    /// <summary>Creates an enumerator over the borrowed declarations.</summary>
    /// <returns>An enumerator positioned before the first declaration.</returns>
    public Enumerator GetEnumerator() => new(this);
    /// <summary>Enumerates namespace declarations without extending the borrowed collection's lifetime.</summary>
    public ref struct Enumerator
    {
        private readonly XmlNamespaceView view;
        private int index;
        internal Enumerator(XmlNamespaceView view)
        {
            this.view = view;
            index = -1;
        }
        /// <summary>Gets the namespace declaration at the current position.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The enumerator is not positioned on a declaration.</exception>
        public readonly XmlNamespaceItem Current => view[index];
        /// <summary>Advances to the next namespace declaration.</summary>
        /// <returns>True if positioned on a declaration; false after the last declaration.</returns>
        public bool MoveNext()
        {
            if (index < view.Count)
            {
                index++;
            }

            return index < view.Count;
        }
    }
}

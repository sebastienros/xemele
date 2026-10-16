namespace Xemele;

/// <summary>A known text run or unresolved entity reference in an incompletely expanded attribute value.</summary>
/// <param name="Location">The source location of the value part.</param>
public abstract record XmlAttributeValuePart(XmlLocation Location);
/// <summary>A successfully expanded text run in an incomplete attribute value.</summary>
/// <param name="Text">The known character data.</param>
/// <param name="Location">The source location at the start of the run.</param>
public sealed record XmlKnownAttributeText(string Text, XmlLocation Location) : XmlAttributeValuePart(Location);
/// <summary>An entity reference that could not be expanded in an attribute value.</summary>
/// <param name="Name">The referenced entity name.</param>
/// <param name="LexicalReference">The original reference spelling, including its delimiters.</param>
/// <param name="Location">The source location of the reference.</param>
public sealed record XmlUnknownAttributeReference(string Name, string LexicalReference, XmlLocation Location) : XmlAttributeValuePart(Location);
/// <summary>Preserves a DTD attribute default containing unresolved entity references.</summary>
/// <param name="ElementName">The lexical element name in the declaration.</param>
/// <param name="AttributeName">The lexical attribute name in the declaration.</param>
/// <param name="LexicalValue">The default value text before complete entity expansion.</param>
/// <param name="IsTokenized">Whether the declared attribute type requires tokenized-value normalization rather than CDATA normalization.</param>
/// <param name="IsExternal">Whether the declaration originates in external DTD content.</param>
/// <param name="DeclarationLocation">The source location of the declaration.</param>
/// <param name="ValueLocation">The source location of the default value.</param>
/// <param name="Parts">The ordered known text runs and unresolved references.</param>
/// <param name="Sequence">The zero-based position among retained incomplete default declarations.</param>
public sealed record XmlIncompleteAttributeDefault(string ElementName, string AttributeName,
    string LexicalValue, bool IsTokenized, bool IsExternal, XmlLocation DeclarationLocation,
    XmlLocation ValueLocation, IReadOnlyList<XmlAttributeValuePart> Parts, int Sequence);
/// <summary>Preserves an attribute occurrence whose value could not be completely expanded.</summary>
/// <param name="Attribute">The stable handle for the affected attribute.</param>
/// <param name="Parent">The element that owned the attribute when parsed.</param>
/// <param name="LexicalValue">The value text before complete entity expansion.</param>
/// <param name="IsTokenized">Whether the value uses tokenized rather than CDATA attribute normalization.</param>
/// <param name="ValueLocation">The source location of the value.</param>
/// <param name="Default">The incomplete default declaration supplying this value, or <see langword="null"/> for an explicit value.</param>
/// <param name="Parts">The ordered known text runs and unresolved references.</param>
/// <param name="Sequence">The zero-based position among retained incomplete attribute occurrences.</param>
/// <remarks>This is original parse provenance, not a fully expanded string value.</remarks>
public sealed record XmlIncompleteAttributeValue(XmlAttribute Attribute, XmlNode Parent,
    string LexicalValue, bool IsTokenized, XmlLocation ValueLocation,
    XmlIncompleteAttributeDefault? Default, IReadOnlyList<XmlAttributeValuePart> Parts, int Sequence);

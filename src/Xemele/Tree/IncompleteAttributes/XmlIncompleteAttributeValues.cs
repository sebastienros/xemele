namespace Xemele;
public abstract record XmlAttributeValuePart(XmlLocation Location);
public sealed record XmlKnownAttributeText(string Text, XmlLocation Location) : XmlAttributeValuePart(Location);
public sealed record XmlUnknownAttributeReference(string Name, string LexicalReference, XmlLocation Location) : XmlAttributeValuePart(Location);
public sealed record XmlIncompleteAttributeDefault(string ElementName, string AttributeName,
    string LexicalValue, bool IsTokenized, bool IsExternal, XmlLocation DeclarationLocation,
    XmlLocation ValueLocation, IReadOnlyList<XmlAttributeValuePart> Parts, int Sequence);
public sealed record XmlIncompleteAttributeValue(XmlAttribute Attribute, XmlNode Parent,
    string LexicalValue, bool IsTokenized, XmlLocation ValueLocation,
    XmlIncompleteAttributeDefault? Default, IReadOnlyList<XmlAttributeValuePart> Parts, int Sequence);

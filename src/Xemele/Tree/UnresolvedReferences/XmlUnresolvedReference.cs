namespace Xemele;

/// <summary>An unresolved content reference and immutable original parse provenance.</summary>
public sealed record XmlUnresolvedReference(string Name, string LexicalReference,
    XmlLocation Location, XmlNode Parent, XmlNode PreviousSibling, int ChildBoundary, int Sequence);

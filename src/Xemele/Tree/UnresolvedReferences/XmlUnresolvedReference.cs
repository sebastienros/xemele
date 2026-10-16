namespace Xemele;

/// <summary>An unresolved content reference and immutable original parse provenance.</summary>
/// <param name="Name">The referenced entity name.</param>
/// <param name="LexicalReference">The original reference spelling, including its delimiters.</param>
/// <param name="Location">The source location of the reference.</param>
/// <param name="Parent">The containing node at parse time.</param>
/// <param name="PreviousSibling">The preceding persisted sibling at parse time, or a default handle if none existed.</param>
/// <param name="ChildBoundary">The zero-based child boundary at which the reference occurred.</param>
/// <param name="Sequence">The zero-based occurrence position among unresolved content references.</param>
public sealed record XmlUnresolvedReference(string Name, string LexicalReference,
    XmlLocation Location, XmlNode Parent, XmlNode PreviousSibling, int ChildBoundary, int Sequence);

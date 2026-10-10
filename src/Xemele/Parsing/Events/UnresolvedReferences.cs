namespace Xemele.Parsing.Syntax;

internal readonly ref struct XmlUnresolvedReferenceView(ReadOnlySpan<char> name,
    ReadOnlySpan<char> lexicalReference, XmlLocation location)
{
    public ReadOnlySpan<char> Name { get; } = name;
    public ReadOnlySpan<char> LexicalReference { get; } = lexicalReference;
    public XmlLocation Location { get; } = location;
}

internal interface IXmlUnresolvedReferenceSink
{
    bool UnresolvedReference(XmlUnresolvedReferenceView reference);
}

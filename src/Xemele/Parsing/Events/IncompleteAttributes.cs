namespace Xemele.Parsing.Syntax
{
    internal readonly ref struct XmlIncompleteAttributeDefaultView(ReadOnlySpan<char> elementName,
        ReadOnlySpan<char> attributeName, ReadOnlySpan<char> lexicalValue, bool isTokenized,
        bool isExternal, Xemele.XmlLocation declarationLocation, Xemele.XmlLocation valueLocation,
        ReadOnlySpan<Xemele.XmlAttributeValuePart> parts, int declarationSequence)
    {
        public ReadOnlySpan<char> ElementName { get; } = elementName;
        public ReadOnlySpan<char> AttributeName { get; } = attributeName;
        public ReadOnlySpan<char> LexicalValue { get; } = lexicalValue;
        public bool IsTokenized { get; } = isTokenized;
        public bool IsExternal { get; } = isExternal;
        public Xemele.XmlLocation DeclarationLocation { get; } = declarationLocation;
        public Xemele.XmlLocation ValueLocation { get; } = valueLocation;
        public ReadOnlySpan<Xemele.XmlAttributeValuePart> Parts { get; } = parts;
        public int DeclarationSequence { get; } = declarationSequence;
    }
    internal readonly ref struct XmlIncompleteAttributeView(XmlNameView name, ReadOnlySpan<char> lexicalValue,
        bool isTokenized, Xemele.XmlLocation valueLocation, ReadOnlySpan<Xemele.XmlAttributeValuePart> parts,
        int? defaultDeclarationSequence)
    {
        public XmlNameView Name { get; } = name;
        public ReadOnlySpan<char> LexicalValue { get; } = lexicalValue;
        public bool IsTokenized { get; } = isTokenized;
        public Xemele.XmlLocation ValueLocation { get; } = valueLocation;
        public ReadOnlySpan<Xemele.XmlAttributeValuePart> Parts { get; } = parts;
        public int? DefaultDeclarationSequence { get; } = defaultDeclarationSequence;
    }
    internal interface IXmlIncompleteAttributeSink
    {
        bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView declaration);
        bool IncompleteAttribute(XmlIncompleteAttributeView attribute);
    }
}

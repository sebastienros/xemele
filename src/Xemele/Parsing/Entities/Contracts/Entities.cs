// Nonpackaged integration hooks; no implementation peer dependency.
namespace Xemele.Parsing.Entities
{
    internal enum EntityUse { Content, Attribute, Dtd }
    internal readonly record struct EntityExpansionRequest(string Name, EntityUse Use, Xemele.XmlLocation ReferenceLocation);
    internal interface IXmlDtdSink
    {
        bool DocumentType(Xemele.XmlDocumentType declaration, Xemele.Parsing.Dtd.DtdModel model, Xemele.XmlLocation location);
    }
    internal interface IXmlEntityContext
    {
        Xemele.Parsing.Dtd.DtdModel Model { get; }
        bool TryGetGeneralEntity(ReadOnlySpan<char> name, out Xemele.Parsing.Dtd.DtdEntityDeclaration? declaration);
        IReadOnlyList<Xemele.Parsing.Dtd.DtdAttributeDeclaration> GetAttributes(ReadOnlySpan<char> elementName);
    }
}

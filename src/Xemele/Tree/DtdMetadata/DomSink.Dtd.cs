using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;

namespace Xemele.Parsing.Dom;

internal sealed partial class DomSink : IXmlDtdSink
{
    public bool DocumentType(XmlDocumentType declaration, DtdModel model, XmlLocation location)
    {
        Document.SetDtdMetadata(declaration, model);
        return true;
    }
}

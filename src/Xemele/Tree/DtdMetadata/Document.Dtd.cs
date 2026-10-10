using Xemele.Parsing.Dtd;

namespace Xemele;

public sealed partial class XmlDocument
{
    private XmlDocumentType? documentType;
    private DtdModel? dtd;
    public XmlDocumentType? DocumentType => documentType;
    internal DtdModel? Dtd => dtd;

    internal void SetDtdMetadata(XmlDocumentType declaration, DtdModel model)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(model);
        documentType = declaration;
        dtd = model;
    }
}

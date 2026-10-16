using Xemele.Parsing.Dtd;

namespace Xemele;

public sealed partial class XmlDocument
{
    private XmlDocumentType? documentType;
    private DtdModel? dtd;
    /// <summary>Gets the parsed document type declaration, or <see langword="null"/> when none was parsed.</summary>
    /// <remarks>This immutable metadata describes the original declaration, not a node in the mutable tree.</remarks>
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

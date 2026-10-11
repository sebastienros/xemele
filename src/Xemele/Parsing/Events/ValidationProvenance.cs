using Xemele.Parsing.Dtd;

namespace Xemele.Parsing.Syntax;

// Optional internal completion metadata. No borrowed input or new public callback.
internal interface IXmlValidationProvenanceSink
{
    void ValidationProvenance(XmlDiagnostic diagnostic, DtdElementDeclaration declaration);
}

using Xemele.Parsing.Dtd;

namespace Xemele;

public sealed partial class XmlDocument
{
    internal XmlDiagnostic? ValidationInstanceDiagnostic { get; private set; }
    internal DtdElementDeclaration? ValidationInstanceDeclaration { get; private set; }

    // One owned diagnostic and a reference into the existing immutable DTD model.
    // Applicable only with a complete matching original-source revision.
    internal void SetValidationInstanceProvenance(XmlDiagnostic diagnostic, DtdElementDeclaration declaration)
    {
        ValidationInstanceDiagnostic ??= diagnostic;
        ValidationInstanceDeclaration ??= declaration;
    }
}

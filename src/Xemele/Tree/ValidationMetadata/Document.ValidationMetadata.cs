namespace Xemele;

public sealed partial class XmlDocument
{
    private bool? validationStandalone;
    private XmlLocation validationDeclarationLocation;

    internal bool? ValidationStandalone => validationStandalone;
    internal XmlLocation ValidationDeclarationLocation => validationDeclarationLocation;

    // Copy scalar declaration provenance; never retain borrowed lexical views.
    internal void SetValidationDeclaration(bool? standalone, XmlLocation location)
    {
        validationStandalone = standalone;
        validationDeclarationLocation = location;
    }
}

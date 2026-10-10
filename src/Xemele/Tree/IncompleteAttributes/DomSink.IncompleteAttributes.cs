using Xemele.Parsing.Syntax;
namespace Xemele.Parsing.Dom;

internal sealed partial class DomSink : IXmlIncompleteAttributeSink
{
    public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView declaration)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Document.CanAddIncompleteDefault)
        { Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error, "DOM incomplete default capacity exceeded.", declaration.ValueLocation); return false; }
        if (!Names(declaration.ElementName, declaration.AttributeName, [], declaration.DeclarationLocation)) return false;
        Document.AddIncompleteDefault(new(OwnName(declaration.ElementName), OwnName(declaration.AttributeName),
            declaration.LexicalValue.ToString(), declaration.IsTokenized, declaration.IsExternal,
            declaration.DeclarationLocation, declaration.ValueLocation, Array.AsReadOnly(declaration.Parts.ToArray()), declaration.DeclarationSequence));
        return true;
    }
    public bool IncompleteAttribute(XmlIncompleteAttributeView attribute)
    {
        cancellation.ThrowIfCancellationRequested(); var name = attribute.Name;
        if (!Document.CanAddIncompleteValue)
        { Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error, "DOM incomplete attribute capacity exceeded.", attribute.ValueLocation); return false; }
        if (!Names(name.LocalName, name.NamespaceUri, name.Prefix, attribute.ValueLocation)) return false;
        var handle = builder.AppendSyntaxValidatedAttribute(parent, OwnName(name.LocalName), "", OwnName(name.NamespaceUri), OwnName(name.Prefix), cancellation);
        string lexical = attribute.DefaultDeclarationSequence is int index
            ? Document.IncompleteAttributeDefaults[index].LexicalValue : attribute.LexicalValue.ToString();
        Document.AddIncompleteValue(handle, parent, lexical, attribute.IsTokenized,
            attribute.ValueLocation, attribute.DefaultDeclarationSequence,
            attribute.DefaultDeclarationSequence is null ? attribute.Parts.ToArray() : null);
        return true;
    }
}

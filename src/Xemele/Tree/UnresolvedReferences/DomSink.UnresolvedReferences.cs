using Xemele.Parsing.Syntax;

namespace Xemele.Parsing.Dom;

internal sealed partial class DomSink : IXmlUnresolvedReferenceSink
{
    public bool UnresolvedReference(XmlUnresolvedReferenceView reference)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Document.CanAddUnresolvedReference)
        {
            Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error,
                "DOM unresolved reference capacity exceeded.", reference.Location);
            return false;
        }
        if (!Names(reference.Name, [], [], reference.Location)) return false;
        Document.AddUnresolvedReference(reference.Name, reference.LexicalReference, reference.Location, parent);
        return true;
    }
}

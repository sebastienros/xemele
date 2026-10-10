using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Input;
using Xemele.Parsing.Syntax;

namespace Xemele.Parsing.Dom;

// Callback views never escape. Text scratch owns one logical token until its
// final fragment; the final string and names then belong to the document arena.
internal sealed class DomSink : IXmlEventSink
{
    private readonly OperationBudget budget;
    private readonly XmlInputDecoder? decoder;
    private readonly XmlDocument.DomBuilder builder;
    private XmlNode parent;
    private XmlTextKind textKind;
    private string target = "";
    private readonly StringBuilder text = new();
    internal XmlDocument Document { get; }
    internal XmlDiagnostic? Diagnostic { get; private set; }
    internal bool DeclarationValidated { get; private set; }

    internal DomSink(XmlParseOptions options, XmlInputDecoder? decoder = null)
    {
        budget = new(options, default);
        Document = new(options.Limits);
        builder = Document.BeginDomConstruction();
        parent = Document.DocumentNode;
        this.decoder = decoder;
    }
    internal void ValidateAbsentDeclaration(XmlLocation location)
    {
        DeclarationValidated = true;
        Diagnostic = decoder!.ValidateDeclaration([], location);
    }
    public bool StartDocument(XmlLocation location) => true;
    public bool Declaration(XmlDeclarationView declaration)
    {
        if (decoder is null) return true;
        DeclarationValidated = true;
        Diagnostic = decoder.ValidateDeclaration(declaration.EncodingName, declaration.Location);
        return Diagnostic is null;
    }
    private bool Names(ReadOnlySpan<char> a, ReadOnlySpan<char> b, ReadOnlySpan<char> c, XmlLocation location)
    {
        int missing = 0;
        if (!a.IsEmpty && Document.FindInternedName(a) is null) missing++;
        if (!b.IsEmpty && !b.SequenceEqual(a) && Document.FindInternedName(b) is null) missing++;
        if (!c.IsEmpty && !c.SequenceEqual(a) && !c.SequenceEqual(b) && Document.FindInternedName(c) is null) missing++;
        if (budget.TryCharge(Quota.InternedNames, missing)) return true;
        Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error, "DOM interned-name limit exceeded.", location);
        return false;
    }
    private string OwnName(ReadOnlySpan<char> value) => Document.FindInternedName(value) ?? value.ToString();
    public bool StartElement(XmlNameView name, bool isEmptyElement, XmlLocation location)
    {
        if (!Names(name.LocalName, name.NamespaceUri, name.Prefix, location)) return false;
        var element = Document.CreateElement(OwnName(name.LocalName), OwnName(name.NamespaceUri), OwnName(name.Prefix));
        builder.AppendChild(parent, element);
        parent = element;
        return true;
    }
    public bool NamespaceDeclaration(XmlNamespaceView declaration)
    {
        if (!Names(declaration.Prefix, declaration.NamespaceUri, [], declaration.Location)) return false;
        builder.AppendNamespaceDeclaration(parent, OwnName(declaration.Prefix), OwnName(declaration.NamespaceUri));
        return true;
    }
    public bool Attribute(XmlAttributeView attribute)
    {
        var name = attribute.Name;
        if (!Names(name.LocalName, name.NamespaceUri, name.Prefix, attribute.Location)) return false;
        builder.AppendAttribute(parent, OwnName(name.LocalName), attribute.Value.ToString(), OwnName(name.NamespaceUri), OwnName(name.Prefix));
        return true;
    }
    public bool StartElementContent() => true;
    public bool EndElement(XmlNameView name, XmlLocation location) { parent = parent.Parent; return true; }
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> piTarget, XmlLocation location)
    {
        textKind = kind;
        text.Clear();
        if (!Names(piTarget, [], [], location)) return false;
        target = OwnName(piTarget);
        return true;
    }
    public bool TextFragment(ReadOnlySpan<char> fragment, bool tokenEnded, XmlLocation location)
    {
        // Outer whitespace is delivered by syntax but cannot be a document child.
        if (textKind == XmlTextKind.Text && parent.Kind == XmlNodeKind.Document) return true;
        if (fragment.Length > Array.MaxLength - text.Length)
        {
            Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error, "DOM text capacity exceeded.", location);
            return false;
        }
        text.Append(fragment);
        if (!tokenEnded) return true;
        string value = text.ToString();
        var node = textKind switch
        {
            XmlTextKind.Text => Document.CreateText(value),
            XmlTextKind.CData => Document.CreateCData(value),
            XmlTextKind.Comment => Document.CreateComment(value),
            _ => Document.CreateProcessingInstruction(target, value)
        };
        builder.AppendChild(parent, node);
        text.Clear();
        return true;
    }
    public bool EndDocument(XmlLocation location) => true;
}

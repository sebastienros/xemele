using System.Text;
using System.Text.Json;
using Xemele.Streaming;
using Reader = Xemele.Streaming.XmlReader;
namespace Xemele.StreamingAcceptance;

// Copies callback views deliberately. This is a semantic oracle, never a memory sink.
public sealed class NormalizedEvents : XmlSaxHandler
{
    public readonly List<string> Content = [];
    public readonly List<string> Metadata = [];
    private readonly StringBuilder text = new();
    private int depth;
    public static string Row(params object?[] values) => JsonSerializer.Serialize(values);
    public void Flush() { if (text.Length != 0) { Content.Add(Row("text", text.ToString())); text.Clear(); } }
    public void Event(params object?[] values) { Flush(); Content.Add(Row(values)); }
    public override void Text(ReadOnlySpan<char> value) { if (depth > 0) text.Append(value); }
    public override void StartDocument() => Metadata.Add(Row("document"));
    public override void EndDocument() { Flush(); Metadata.Add(Row("done")); }
    public void Start(string local, string prefix, string uri, IEnumerable<string> attrs, IEnumerable<string> ns) { Event("start", local, prefix, uri, attrs.ToArray(), ns.ToArray()); depth++; }
    public override void StartElement(XmlStartElement e) => Start(e.LocalName.ToString(), e.Prefix.ToString(), e.NamespaceUri.ToString(), Attributes(e.Attributes), Namespaces(e.NamespaceDeclarations));
    public List<string> Attributes(XmlAttributeView view)
    {
        List<string> result = [];
        foreach (var a in view)
        {
            string value = a.IsValueComplete ? Row("value", a.Value.ToString()) : Parts(a.ValueParts.ToArray());
            result.Add(Row(a.LocalName.ToString(), a.Prefix.ToString(), a.NamespaceUri.ToString(), value));
            Metadata.Add(Row("attribute", a.LocalName.ToString(), a.IsDefaulted, a.IsValueComplete));
        }
        return result;
    }
    public static string Parts(IEnumerable<XmlAttributeValuePart> parts) => Row("parts", parts.Select(p => p switch
    {
        XmlKnownAttributeText k => Row("known", k.Text),
        XmlUnknownAttributeReference u => Row("unknown", u.Name, u.LexicalReference),
        _ => throw new InvalidOperationException("Unknown value part.")
    }).ToArray());
    public static List<string> Namespaces(XmlNamespaceView view) { List<string> result = []; foreach (var n in view) result.Add(Row(n.Prefix.ToString(), n.NamespaceUri.ToString())); return result; }
    public void End(string local, string prefix, string uri) { Event("end", local, prefix, uri); depth--; }
    public override void EndElement(XmlEndElement e) => End(e.LocalName.ToString(), e.Prefix.ToString(), e.NamespaceUri.ToString());
    public override void Comment(ReadOnlySpan<char> value) => Event("comment", value.ToString());
    public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> value) => Event("pi", target.ToString(), value.ToString());
    public override void StartCData() => Event("cdata");
    public override void EndCData() => Event("/cdata");
    public override void Declaration(XmlDeclaration d) => Metadata.Add(Row("declaration", d.Version.ToString(), d.EncodingName.ToString(), d.Standalone));
    public override void StartDtd(XmlDocumentTypeView d) => Metadata.Add(Row("dtd", d.Name.ToString(), d.HasPublicId, d.PublicId.ToString(), d.HasSystemId, d.SystemId.ToString()));
    public override void EndDtd() => Metadata.Add(Row("/dtd"));
    public override void DtdDeclaration(XmlDtdDeclaration d) => Metadata.Add(Row("dtd-declaration", d.Kind, d.Name.ToString(), d.ElementName.ToString(), d.ContentModel.ToString(), d.AttributeType.ToString(), d.DefaultKind, d.HasValue, d.Value.ToString(), d.HasSystemId, d.SystemId.ToString(), d.HasPublicId, d.PublicId.ToString(), d.NotationName.ToString(), d.IsParameter, d.IsExternal));
    public override void SkippedEntity(XmlSkippedEntity e) => Event("unknown", e.Name.ToString(), e.LexicalReference.ToString());
    public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault d) => Metadata.Add(Row("incomplete-default", d.ElementName, d.AttributeName, d.LexicalValue, d.IsTokenized, d.IsExternal, d.Sequence, Parts(d.Parts)));
    public void Read(Reader r)
    {
        switch (r.TokenKind)
        {
            case XmlTokenKind.StartDocument: StartDocument(); break;
            case XmlTokenKind.EndDocument: EndDocument(); break;
            case XmlTokenKind.StartElement: Start(r.LocalName.ToString(), r.Prefix.ToString(), r.NamespaceUri.ToString(), Attributes(r.Attributes), Namespaces(r.NamespaceDeclarations)); break;
            case XmlTokenKind.EndElement: End(r.LocalName.ToString(), r.Prefix.ToString(), r.NamespaceUri.ToString()); break;
            case XmlTokenKind.Text: Text(r.ValueSpan); break;
            case XmlTokenKind.Comment: Comment(r.ValueSpan); break;
            case XmlTokenKind.ProcessingInstruction: ProcessingInstruction(r.Target, r.ValueSpan); break;
            case XmlTokenKind.StartCData: StartCData(); break;
            case XmlTokenKind.EndCData: EndCData(); break;
            case XmlTokenKind.Declaration: Declaration(r.Declaration); break;
            case XmlTokenKind.StartDtd: StartDtd(r.DocumentType); break;
            case XmlTokenKind.DtdDeclaration: DtdDeclaration(r.DtdDeclaration); break;
            case XmlTokenKind.EndDtd: EndDtd(); break;
            case XmlTokenKind.SkippedEntity: SkippedEntity(r.SkippedEntity); break;
            case XmlTokenKind.IncompleteAttributeDefault: IncompleteAttributeDefault(r.IncompleteAttributeDefault!); break;
            default: throw new InvalidOperationException("Unexpected strict token " + r.TokenKind);
        }
    }
    public static NormalizedEvents Dom(XmlDocument document)
    {
        var result = new NormalizedEvents();
        void Visit(XmlNode node)
        {
            switch (node.Kind)
            {
                case XmlNodeKind.Element:
                    List<string> attributes = [], namespaces = [];
                    foreach (var a in node.Attributes) attributes.Add(Row(a.LocalName, a.Prefix, a.NamespaceUri,
                        document.IncompleteAttributeValues.FirstOrDefault(v => v.Attribute == a) is { } incomplete ? Parts(incomplete.Parts) : Row("value", a.Value)));
                    foreach (var n in node.NamespaceDeclarations) namespaces.Add(Row(n.Prefix, n.Value));
                    result.Start(node.LocalName, node.Prefix, node.NamespaceUri, attributes, namespaces);
                    Children(node); result.End(node.LocalName, node.Prefix, node.NamespaceUri); break;
                case XmlNodeKind.Text: result.Text(node.Value); break;
                case XmlNodeKind.CData: result.StartCData(); result.Text(node.Value); result.EndCData(); break;
                case XmlNodeKind.Comment: result.Comment(node.Value); break;
                case XmlNodeKind.ProcessingInstruction: result.ProcessingInstruction(node.LocalName, node.Value); break;
                default: throw new InvalidOperationException("Unexpected DOM node.");
            }
        }
        void Children(XmlNode parent)
        {
            int boundary = 0;
            foreach (var child in parent.Children)
            {
                foreach (var u in document.UnresolvedReferences.Where(u => u.Parent == parent && u.ChildBoundary == boundary)) result.Event("unknown", u.Name, u.LexicalReference);
                Visit(child); boundary++;
            }
            foreach (var u in document.UnresolvedReferences.Where(u => u.Parent == parent && u.ChildBoundary == boundary)) result.Event("unknown", u.Name, u.LexicalReference);
        }
        Children(document.DocumentNode); result.Flush(); return result;
    }
}

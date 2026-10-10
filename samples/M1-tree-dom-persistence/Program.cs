using System.Text;
using Xemele;
using Xemele.Parsing.Syntax;

if (args.Contains("--allocation")) { AllocationProbe.Run(); return; }
string name = "根" + new string('n', 8192), value = new string('α', 16384) + "😀";
string source = $"<r xmlns:p='urn:p' a='{value}'><p:{name}>{value}<![CDATA[{value}]]><!--{value}--><?{name} {value}?></p:{name}><p:{name}/></r>";
var model = new ModelSink();
using (var parser = new XmlParserCore(new() { Limits = new() { TokenCharacters = null } }))
{
    var result = parser.Append(source, [], true, model, default);
    if (result.Step != ScanStep.Completed) throw new Exception("Independent model syntax rejected: " + result.Diagnostic?.Message);
}
source = ""; // Model retains only exact immutable owned strings, no source view.
var document = Replay(model.Items);
var root = document.RootElement; var first = root.FirstChild;
if (root.Children.Count != 2 || root.GetAttribute("a") != value || first.Children.Count != 4 || first.LastChild.LocalName != name || first.LastChild.Value != value || first.GetTextContent() != value + value)
    throw new Exception("Independent validated model replay mismatch.");
if (!ReferenceEquals(first.LocalName, root.LastChild.LocalName)) throw new Exception("Repeated name identity lost.");
Console.WriteLine("Independent validated model replay passed: long names, attribute, namespace, text/CDATA/comment/PI, borrowed repeated name hits and owned lifetime.");

static XmlDocument Replay(List<Item> items)
{
    var d = new XmlDocument(); var b = d.BeginDomConstruction(); var parents = new Stack<XmlNode>(); parents.Push(d.DocumentNode);
    foreach (var item in items)
    {
        switch (item.Kind)
        {
            case "start":
                var element = b.CreateSyntaxValidatedNode(XmlNodeKind.Element, item.Local, item.Uri, item.Prefix, "", default);
                b.AppendSyntaxValidatedChild(parents.Peek(), element, default); parents.Push(element); break;
            case "end": parents.Pop(); break;
            case "attribute": b.AppendSyntaxValidatedAttribute(parents.Peek(), item.Local, item.Value, item.Uri, item.Prefix, default); break;
            case "namespace": b.AppendSyntaxValidatedNamespaceDeclaration(parents.Peek(), item.Prefix, item.Value, default); break;
            case "text":
                var node = b.CreateSyntaxValidatedNode(item.NodeKind, item.Local, "", "", item.Value, default);
                b.AppendSyntaxValidatedChild(parents.Peek(), node, default); break;
        }
    }
    if (parents.Count != 1) throw new Exception("Unbalanced validated model."); return d;
}
sealed record Item(string Kind, string Local = "", string Uri = "", string Prefix = "", string Value = "", XmlNodeKind NodeKind = XmlNodeKind.Text);
sealed class ModelSink : IXmlEventSink
{
    internal readonly List<Item> Items = [];
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private readonly StringBuilder text = new();
    private XmlNodeKind kind; private string target = "";
    private string OwnName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty) return "";
        if (names.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var owned)) return owned;
        owned = name.ToString(); names.Add(owned, owned); return owned;
    }
    public bool StartDocument(XmlLocation l) => true;
    public bool Declaration(XmlDeclarationView d) => true;
    public bool StartElement(XmlNameView n, bool empty, XmlLocation l) { Items.Add(new("start", OwnName(n.LocalName), OwnName(n.NamespaceUri), OwnName(n.Prefix))); return true; }
    public bool NamespaceDeclaration(XmlNamespaceView n) { Items.Add(new("namespace", Prefix: OwnName(n.Prefix), Value: OwnName(n.NamespaceUri))); return true; }
    public bool Attribute(XmlAttributeView a) { Items.Add(new("attribute", OwnName(a.Name.LocalName), OwnName(a.Name.NamespaceUri), OwnName(a.Name.Prefix), a.Value.ToString())); return true; }
    public bool StartElementContent() => true;
    public bool EndElement(XmlNameView n, XmlLocation l) { Items.Add(new("end")); return true; }
    public bool StartText(XmlTextKind k, ReadOnlySpan<char> piTarget, XmlLocation l)
    {
        kind = k switch { XmlTextKind.Text => XmlNodeKind.Text, XmlTextKind.CData => XmlNodeKind.CData, XmlTextKind.Comment => XmlNodeKind.Comment, XmlTextKind.ProcessingInstruction => XmlNodeKind.ProcessingInstruction, _ => throw new Exception("Unknown kind") };
        target = OwnName(piTarget); text.Clear(); return true;
    }
    public bool TextFragment(ReadOnlySpan<char> fragment, bool ended, XmlLocation l)
    {
        text.Append(fragment); if (ended) Items.Add(new("text", Local: target, Value: text.ToString(), NodeKind: kind)); return true;
    }
    public bool EndDocument(XmlLocation l) => true;
}

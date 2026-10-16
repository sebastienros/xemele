using Xemele;
using Xemele.Parsing.Syntax;

if (args.Contains("--allocation")) { AllocationProbe.Run(); return; }

// Independent owned event model first, then replay into Tree. This is a contract
// probe, not DOM factories or a streaming DOM adapter. Model allocations are explicit.
var model = new ModelSink();
using (var parser = new XmlParserCore(new()))
{
    var result = parser.Append("<r xmlns:p='urn:p' a='owned'><p:c>α&amp;😀</p:c><empty/></r>", [], true, model, default);
    if (result.Step != ScanStep.Completed)
        throw new Exception("Model syntax rejected.");
}
var document = new XmlDocument();
var builder = document.BeginDomConstruction();
var parents = new Stack<XmlNode>();
parents.Push(document.DocumentNode);
foreach (var item in model.Items)
{
    switch (item.Kind)
    {
        case "start":
            var element = document.CreateElement(item.Local, item.Uri, item.Prefix);
            builder.AppendChild(parents.Peek(), element);
            parents.Push(element);
            break;
        case "end":
            parents.Pop();
            break;
        case "attribute":
            builder.AppendAttribute(parents.Peek(), item.Local, item.Value, item.Uri, item.Prefix);
            break;
        case "namespace":
            builder.AppendNamespaceDeclaration(parents.Peek(), item.Prefix, item.Value);
            break;
        case "text":
            builder.AppendChild(parents.Peek(), document.CreateText(item.Value));
            break;
    }
}
if (parents.Count != 1 || document.RootElement.Children.Count != 2 || document.RootElement.GetAttribute("a") != "owned" || document.RootElement.GetTextContent() != "α&😀")
    throw new Exception("Independent model replay differs.");
Console.WriteLine($"Independent validated model replay passed: {builder.ChildChecks} child checks, {builder.MemberChecks} member checks; owned strings survive parser disposal.");

sealed record Item(string Kind, string Local = "", string Uri = "", string Prefix = "", string Value = "");
sealed class ModelSink : IXmlEventSink
{
    internal readonly List<Item> Items = [];
    public bool StartDocument(XmlLocation l) => true;
    public bool Declaration(XmlDeclarationView d) => true;
    public bool StartElement(XmlNameView n, bool empty, XmlLocation l)
    {
        Items.Add(new("start", n.LocalName.ToString(), n.NamespaceUri.ToString(), n.Prefix.ToString()));
        return true;
    }
    public bool NamespaceDeclaration(XmlNamespaceView n)
    {
        Items.Add(new("namespace", Prefix: n.Prefix.ToString(), Value: n.NamespaceUri.ToString()));
        return true;
    }
    public bool Attribute(XmlAttributeView a)
    {
        Items.Add(new("attribute", a.Name.LocalName.ToString(), a.Name.NamespaceUri.ToString(), a.Name.Prefix.ToString(), a.Value.ToString()));
        return true;
    }
    public bool StartElementContent() => true;
    public bool EndElement(XmlNameView n, XmlLocation l)
    {
        Items.Add(new("end"));
        return true;
    }
    public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l)
    {
        if (k != XmlTextKind.Text)
            throw new Exception("Probe only uses text.");
        return true;
    }
    public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation l)
    {
        Items.Add(new("text", Value: text.ToString()));
        return true;
    }
    public bool EndDocument(XmlLocation l) => true;
}

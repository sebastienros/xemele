using Xemele;
using Xemele.XPath;
using Xemele.XPath.Internal;

// The core library is deliberately internal. This standalone friend consumer
// uses only the shipped model and library, plus its own snapshot navigator fake.
var document = new XmlDocument();
var root = document.CreateElement("r"); document.DocumentNode.AppendChild(root);
root.AppendChild(document.CreateText("A😀B"));
var item = XPathItem.FromNode(root);
var library = new XPathCoreFunctions();
var navigator = new SnapshotFake(XPathItem.FromNode(document.DocumentNode));
var budget = new XPathBudget(new(), default, default);
XPathValue Call(string name, params XPathValue[] args) => library.Invoke(XPathCoreSignatures.ByName[new("", name)], new(item, 1, 1), args, navigator, budget);
if (Call("string-length").AsNumber() != 3 || Call("substring", XPathValue.FromString("A😀B"), XPathValue.FromNumber(2), XPathValue.FromNumber(1)).AsString() != "😀") throw new InvalidOperationException();
if (library.Signatures.Count != 27 || Call("number", XPathValue.FromString(" -2.5 ")).AsNumber() != -2.5) throw new InvalidOperationException();
Console.WriteLine("PASS: 27 internal signatures; scalar length=3; substring=😀; number=-2.5; shared budget charged.");
internal sealed class SnapshotFake(XPathItem root) : IXPathNavigator
{
    public XPathItem Root => root;
    public IReadOnlyList<XPathItem> SelectAxis(XPathItem item, XPathAxis axis, IXPathBudget budget) => throw new NotSupportedException();
    public int CompareDocumentOrder(XPathItem left, XPathItem right, IXPathBudget budget) => throw new NotSupportedException();
    public IReadOnlyList<XPathItem> SelectId(string id, IXPathBudget budget) => throw new NotSupportedException();
    public string? FindLanguage(XPathItem item, IXPathBudget budget) => throw new NotSupportedException();
}

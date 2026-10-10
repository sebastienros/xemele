using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
namespace Xemele.DomPersistenceMeasurements;

// Both paths replay the same prevalidated owned model and use the fresh builder
// for attachment. Only redundant validation at persistence differs.
[MemoryDiagnoser, ShortRunJob]
public class DomPersistenceBenchmarks
{
    [Params(16, 16384)] public int Length { get; set; }
    [Params("values", "names")] public string Shape { get; set; } = "values";
    private string name = "", prefix = "", uri = "", value = "";
    [GlobalSetup]
    public void Setup()
    {
        name = "n" + new string('n', Shape == "names" ? Length : 1);
        prefix = "p"; uri = "urn:" + new string('u', Shape == "names" ? Length : 1);
        value = new string('α', Shape == "values" ? Length : 1);
        TreeValidation.Expanded(name, uri, prefix); TreeValidation.Name(name, true);
        foreach (var kind in new[] { XmlNodeKind.Text, XmlNodeKind.CData, XmlNodeKind.Comment, XmlNodeKind.ProcessingInstruction }) TreeValidation.Value(kind, value);
        var a = Build(false); var b = Build(true);
        if (a.RootElement.Children.Count != b.RootElement.Children.Count || a.RootElement.GetTextContent() != b.RootElement.GetTextContent() || a.RootElement.LastChild.LastChild.LocalName != b.RootElement.LastChild.LastChild.LocalName)
            throw new Exception("Equivalent validated replay oracle differs.");
    }
    [Benchmark(Baseline = true)] public XmlDocument CheckedPersistence() => Build(false);
    [Benchmark] public XmlDocument SyntaxValidatedPersistence() => Build(true);
    private XmlDocument Build(bool trusted)
    {
        var d = new XmlDocument(new() { InternedNames = null }); var b = d.BeginDomConstruction();
        XmlNode Create(XmlNodeKind kind, string local, string ns, string p, string v) => trusted
            ? b.CreateSyntaxValidatedNode(kind, local, ns, p, v, default)
            : kind switch { XmlNodeKind.Element => d.CreateElement(local, ns, p), XmlNodeKind.Text => d.CreateText(v), XmlNodeKind.CData => d.CreateCData(v), XmlNodeKind.Comment => d.CreateComment(v), _ => d.CreateProcessingInstruction(local, v) };
        var root = Create(XmlNodeKind.Element, "r", "", "", ""); b.AppendSyntaxValidatedChild(d.DocumentNode, root, default);
        for (int i = 0; i < 64; i++)
        {
            var e = Create(XmlNodeKind.Element, name, uri, prefix, ""); b.AppendSyntaxValidatedChild(root, e, default);
            if (trusted) { b.AppendSyntaxValidatedAttribute(e, name, value, uri, prefix, default); b.AppendSyntaxValidatedNamespaceDeclaration(e, prefix, uri, default); }
            else { b.AppendAttribute(e, name, value, uri, prefix); b.AppendNamespaceDeclaration(e, prefix, uri); }
            for (int k = 2; k <= 5; k++)
            {
                var kind = (XmlNodeKind)k; var node = Create(kind, kind == XmlNodeKind.ProcessingInstruction ? name : "", "", "", value);
                b.AppendSyntaxValidatedChild(e, node, default);
            }
        }
        return d;
    }
}

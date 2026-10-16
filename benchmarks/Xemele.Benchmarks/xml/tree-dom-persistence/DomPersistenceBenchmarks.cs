using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
namespace Xemele.DomPersistenceMeasurements;

// Both paths replay the same prevalidated owned model and use the fresh builder
// for attachment. Only redundant validation at persistence differs.
[MemoryDiagnoser, ShortRunJob]
public class DomPersistenceBenchmarks
{
    [Params(16, 16384)]
    public int Length
    {
        get; set;
    }
    [Params("values", "names")] public string Shape { get; set; } = "values";
    private string name = "", prefix = "", uri = "", value = "";
    [GlobalSetup]
    public void Setup()
    {
        name = "n" + new string('n', Shape == "names" ? Length : 1);
        prefix = "p";
        uri = "urn:" + new string('u', Shape == "names" ? Length : 1);
        value = new string('α', Shape == "values" ? Length : 1);
        TreeValidation.Expanded(name, uri, prefix);
        TreeValidation.Name(name, true);
        foreach (var kind in new[] { XmlNodeKind.Text, XmlNodeKind.CData, XmlNodeKind.Comment, XmlNodeKind.ProcessingInstruction })
        {
            TreeValidation.Value(kind, value);
        }

        var a = Build(false);
        var b = Build(true);
        AssertEquivalent(a.DocumentNode, b.DocumentNode);
    }
    private static void AssertEquivalent(XmlNode a, XmlNode b)
    {
        var pending = new Stack<(XmlNode, XmlNode)>();
        pending.Push((a, b));
        var visited = 0;
        while (pending.TryPop(out var pair))
        {
            (a, b) = pair;
            visited++;
            if ((a.Kind, a.LocalName, a.NamespaceUri, a.Prefix, a.Value) != (b.Kind, b.LocalName, b.NamespaceUri, b.Prefix, b.Value) ||
                a.Children.Count != b.Children.Count || a.Attributes.Count != b.Attributes.Count || a.NamespaceDeclarations.Count != b.NamespaceDeclarations.Count)
            {
                throw new Exception("Equivalent node fields/counts differ.");
            }

            var ac = a.Children.GetEnumerator();
            var bc = b.Children.GetEnumerator();
            while (ac.MoveNext())
            {
                if (!bc.MoveNext())
                {
                    throw new Exception("Child order differs.");
                }

                pending.Push((ac.Current, bc.Current));
            }
            if (bc.MoveNext())
            {
                throw new Exception("Extra candidate child.");
            }

            var aa = a.Attributes.GetEnumerator();
            var ba = b.Attributes.GetEnumerator();
            while (aa.MoveNext())
            {
                if (!ba.MoveNext() || (aa.Current.LocalName, aa.Current.NamespaceUri, aa.Current.Prefix, aa.Current.Value) !=
                    (ba.Current.LocalName, ba.Current.NamespaceUri, ba.Current.Prefix, ba.Current.Value))
                {
                    throw new Exception("Attribute fields/order differ.");
                }
            }
            if (ba.MoveNext())
            {
                throw new Exception("Extra candidate attribute.");
            }

            var an = a.NamespaceDeclarations.GetEnumerator();
            var bn = b.NamespaceDeclarations.GetEnumerator();
            while (an.MoveNext())
            {
                if (!bn.MoveNext() || (an.Current.Prefix, an.Current.Value) != (bn.Current.Prefix, bn.Current.Value))
                {
                    throw new Exception("Namespace fields/order differ.");
                }
            }

            if (bn.MoveNext())
            {
                throw new Exception("Extra candidate namespace.");
            }
        }
        if (visited != 322)
        {
            throw new Exception("Expected document/root/64 five-node groups.");
        }
    }
    [Benchmark(Baseline = true)] public XmlDocument CheckedPersistence() => Build(false);
    [Benchmark] public XmlDocument SyntaxValidatedPersistence() => Build(true);
    private XmlDocument Build(bool trusted)
    {
        var d = new XmlDocument(new()
        {
            InternedNames = null
        });
        var b = d.BeginDomConstruction();
        XmlNode Create(XmlNodeKind kind, string local, string ns, string p, string v) => trusted
            ? b.CreateSyntaxValidatedNode(kind, local, ns, p, v, default)
            : kind switch
            {
                XmlNodeKind.Element => d.CreateElement(local, ns, p),
                XmlNodeKind.Text => d.CreateText(v),
                XmlNodeKind.CData => d.CreateCData(v),
                XmlNodeKind.Comment => d.CreateComment(v),
                _ => d.CreateProcessingInstruction(local, v)
            };
        var root = Create(XmlNodeKind.Element, "r", "", "", "");
        b.AppendSyntaxValidatedChild(d.DocumentNode, root, default);
        for (var i = 0; i < 64; i++)
        {
            var e = Create(XmlNodeKind.Element, name, uri, prefix, "");
            b.AppendSyntaxValidatedChild(root, e, default);
            if (trusted)
            {
                b.AppendSyntaxValidatedAttribute(e, name, value, uri, prefix, default);
                b.AppendSyntaxValidatedNamespaceDeclaration(e, prefix, uri, default);
            }
            else
            {
                b.AppendAttribute(e, name, value, uri, prefix);
                b.AppendNamespaceDeclaration(e, prefix, uri);
            }
            for (var k = 2; k <= 5; k++)
            {
                var kind = (XmlNodeKind)k;
                var node = Create(kind, kind == XmlNodeKind.ProcessingInstruction ? name : "", "", "", value);
                b.AppendSyntaxValidatedChild(e, node, default);
            }
        }
        return d;
    }
}

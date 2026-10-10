using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Xemele;

namespace Xemele.DomBuilderMeasurements;

[MemoryDiagnoser, ShortRunJob]
public class DomBuilderBenchmarks
{
    [Params(256, 2048)] public int Count { get; set; }
    [Params("deep", "members", "document")] public string Shape { get; set; } = "deep";
    private string[] names = null!;
    [GlobalSetup]
    public void Setup()
    {
        names = new string[Count]; for (int i = 0; i < Count; i++) names[i] = "n" + i;
        var baseline = PublicConstruction(); var candidate = ValidatedConstruction();
        if (baseline.DocumentNode.Children.Count != candidate.DocumentNode.Children.Count || baseline.RootElement.Attributes.Count != candidate.RootElement.Attributes.Count || baseline.RootElement.GetTextContent() != candidate.RootElement.GetTextContent())
            throw new Exception("Construction oracle mismatch.");
    }
    [Benchmark(Baseline = true)] public XmlDocument PublicConstruction() => Build(false);
    [Benchmark] public XmlDocument ValidatedConstruction() => Build(true);
    private XmlDocument Build(bool validated)
    {
        var d = new XmlDocument(new() { InternedNames = null });
        var b = validated ? d.BeginDomConstruction() : null;
        var e = d.CreateElement("e"); if (b is null) d.DocumentNode.AppendChild(e); else b.AppendChild(d.DocumentNode, e);
        for (int i = 0; i < Count; i++)
        {
            if (Shape == "members")
            {
                if (b is null) { e.SetAttribute(names[i], "v"); e.SetNamespaceDeclaration(names[i], "urn:shared"); }
                else { b.AppendAttribute(e, names[i], "v"); b.AppendNamespaceDeclaration(e, names[i], "urn:shared"); }
            }
            else
            {
                var child = Shape == "deep" ? d.CreateElement("e") : d.CreateComment("comment");
                var parent = Shape == "deep" ? e : d.DocumentNode;
                if (b is null) parent.AppendChild(child); else b.AppendChild(parent, child);
                if (Shape == "deep") e = child;
            }
        }
        return d;
    }
}

using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.XPath;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class XPathOverlapBenchmarks
{
    [Params(128, 1024)]
    public int Count
    {
        get; set;
    }
    private XmlDocument document = null!;
    private XPathExpression expression = null!;
    private readonly XPathContext options = new() { Limits = new() { MaxOperations = null } };
    [GlobalSetup]
    public void Setup()
    {
        var xml = new StringBuilder("<r>");
        for (var i = 0; i < Count; i++)
        {
            xml.Append("<n a='v'><x/></n>");
        }

        document = XmlDocument.Parse(xml.Append("</r>").ToString());
        expression = XPathExpression.Compile("count(/r/n[last()]/preceding-sibling::n | /r/n)");
    }
    [Benchmark]
    public double ReverseAndOverlappingMerge() => expression.Evaluate(document.RootElement, options).AsNumber();
}

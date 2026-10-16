using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.XPath;
using Xemele.XPath.Internal;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class XPathOrderBenchmarks
{
    [Params(128, 1024)]
    public int Count
    {
        get; set;
    }
    [Params("sorted", "reverse", "children", "evaluate")] public string Operation { get; set; } = "sorted";
    private XmlDocument document = null!;
    private XPathNavigator navigator = null!;
    private XPathExpression expression = null!;
    private IReadOnlyList<XPathItem> items = null!;
    private readonly XPathContext options = new() { Limits = new() { MaxOperations = null } };
    private static XPathBudget Budget() => new(new() { MaxOperations = null }, default, CancellationToken.None);

    [GlobalSetup]
    public void Setup()
    {
        var xml = new StringBuilder("<r>");
        for (var i = 0; i < Count; i++)
        {
            xml.Append("<n a='v'><x/></n>");
        }

        document = XmlDocument.Parse(xml.Append("</r>").ToString());
        navigator = new(document, Budget());
        expression = XPathExpression.Compile("count(//n)");
        items = navigator.SelectAxis(XPathItem.FromNode(document.RootElement), XPathAxis.Child, Budget());
        if (Operation == "reverse")
        {
            items = [.. items.Reverse()];
        }
    }
    [Benchmark]
    public double Query()
    {
        if (Operation == "evaluate")
        {
            return expression.Evaluate(document.RootElement, options).AsNumber();
        }

        if (Operation == "children")
        {
            return navigator.SelectAxis(XPathItem.FromNode(document.RootElement), XPathAxis.Child, Budget()).Count;
        }

        return navigator.Normalize(items, Budget()).Count;
    }
}

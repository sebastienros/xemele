using Xemele.XPath;
using Xunit;
namespace Xemele.Tests.M4SnapshotCache;

public sealed class SnapshotCacheTests
{
    private static XmlDocument Parse(string xml) => XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
    private static XPathExpression Compile(string expression) => XPathExpression.Compile(expression, null, TestContext.Current.CancellationToken);
    private static string Describe(XPathValue value) => value.Kind == XPathValueKind.NodeSet
        ? string.Join(",", value.AsNodeSet().Select(i => i.Kind + ":" + i.Name + "=" + i.StringValue))
        : value.Kind == XPathValueKind.Number ? value.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture) : value.AsString();
    private static readonly string[] Sources =
    [
        "count(//*)", "string(/r)", "//a/@id", "count(//namespace::*)", "//a[@id='2']", "sum(//a/@id)", "//a/following-sibling::*", "//text()"
    ];

    [Fact]
    public void RepeatedEvaluationsMatchFreshDocumentsAfterEveryMutation()
    {
        var document = Parse("<r xmlns:p='urn:p'><a id='1'>x</a><a id='2'>y</a><p:b/></r>");
        var expressions = Sources.Select(Compile).ToArray();
        var mutations = new Action<XmlDocument>[]
        {
            d => d.RootElement.FirstChild.SetAttribute("id", "9"),
            d => d.RootElement.FirstChild.FirstChild.SetTextContent("changed"),
            d =>
            {
                var added = d.CreateElement("a");
                added.SetAttribute("id", "3");
                d.RootElement.AppendChild(added);
            },
            d => d.RootElement.SetNamespaceDeclaration("q", "urn:q"),
            d => d.RootElement.FirstChild.RemoveAttribute("id"),
            d => d.RootElement.LastChild.Remove()
        };
        foreach (var mutate in new Action<XmlDocument>?[] { null }.Concat(mutations))
        {
            mutate?.Invoke(document);
            var fresh = Parse(document.ToXmlString(new() { OmitXmlDeclaration = true }));
            foreach (var expression in expressions)
            {
                var first = Describe(expression.Evaluate(document.RootElement));
                var second = Describe(expression.Evaluate(document.RootElement));
                Assert.Equal(Describe(expression.Evaluate(fresh.RootElement)), first);
                Assert.Equal(first, second);
            }
        }
    }

    [Fact]
    public void WarmEvaluationChargesTheSameOperationsAndEnforcesLimits()
    {
        var document = Parse("<r><a id='1'>x</a><a id='2'>y</a><b/></r>");
        foreach (var source in Sources)
        {
            var expression = Compile(source);
            var coldDocument = Parse("<r><a id='1'>x</a><a id='2'>y</a><b/></r>");
            expression.EvaluateWithAccounting(coldDocument.RootElement, null, out var cold);
            expression.EvaluateWithAccounting(document.RootElement, null, out _);
            expression.EvaluateWithAccounting(document.RootElement, null, out var warm);
            Assert.Equal(cold, warm);
            expression.Evaluate(document.RootElement, new() { Limits = new() { MaxOperations = warm } });
            var limited = Assert.Throws<XPathEvaluationException>(() => expression.Evaluate(document.RootElement, new() { Limits = new() { MaxOperations = warm - 1 } }));
            Assert.Equal("XPATH-EVALUATION-LIMIT", limited.Diagnostic.Code);
        }
    }

    [Fact]
    public void WarmEvaluationStillAppliesInputStringAndCancellationLimits()
    {
        var document = Parse("<r><a id='1'>text</a><a id='2'/></r>");
        var expression = Compile("count(//a)");
        Assert.Equal(2, expression.Evaluate(document.RootElement).AsNumber());
        foreach (var limits in new XPathEvaluationLimits[] { new() { MaxInputItems = 3 }, new() { MaxInputCharacters = 5 }, new() { MaxStringCharacters = 3 } })
        {
            var error = Assert.Throws<XPathEvaluationException>(() => expression.Evaluate(document.RootElement, new() { Limits = limits }));
            Assert.Equal("XPATH-EVALUATION-LIMIT", error.Diagnostic.Code);
        }
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => expression.Evaluate(document.RootElement, new() { CancellationToken = source.Token }));
        Assert.Equal(2, expression.Evaluate(document.RootElement).AsNumber());
    }

    [Fact]
    public void ReturnedNodeSetsAreOwnedAndIndependentOfLaterMutation()
    {
        var document = Parse("<r><a id='1'/><a id='2'/></r>");
        var expression = Compile("//a");
        var result = expression.Evaluate(document.RootElement);
        var items = result.AsNodeSet();
        document.RootElement.FirstChild.SetAttribute("id", "changed");
        document.RootElement.LastChild.Remove();
        Assert.Equal(2, items.Count);
        Assert.Single(expression.Evaluate(document.RootElement).AsNodeSet());
        Assert.Equal(2, result.AsNodeSet().Count);
    }
}

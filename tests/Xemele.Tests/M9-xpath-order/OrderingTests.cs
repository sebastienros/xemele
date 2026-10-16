using System.Text;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;

namespace Xemele.Tests.M9XPathOrder;

public sealed class OrderingTests
{
    private static XmlDocument Wide(int count)
    {
        var text = new StringBuilder("<r>");
        for (var i = 0; i < count; i++)
        {
            text.Append("<n a='").Append(i).Append("'><x/></n>");
        }

        return XmlDocument.Parse(text.Append("</r>").ToString());
    }
    private static XPathBudget Budget(long? work = null) => new(new() { MaxOperations = work, MaxNodeSetItems = 100000, MaxInputItems = 100000 }, default, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeNormalizationIsBoundedOrderedAndUnique(bool reverse)
    {
        var document = Wide(4096);
        var nav = new XPathNavigator(document, Budget());
        var source = nav.SelectAxis(XPathItem.FromNode(document.RootElement), XPathAxis.Child, Budget());
        var input = new List<XPathItem>();
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[reverse ? source.Count - 1 - i : i];
            input.Add(item);
            input.Add(item);
        }
        var budget = Budget(500000);
        var result = nav.Normalize(input, budget);
        Assert.Equal(source, result);
        Assert.True(budget.Operations < 500000);
    }

    [Fact]
    public void DescendantCountAndOverlappingUnionsPreserveOrderWithinFiniteWork()
    {
        var document = Wide(4096);
        var options = new XPathContext
        {
            Limits = new() { MaxOperations = 5000000, MaxNodeSetItems = 100000, MaxInputItems = 100000 },
            CancellationToken = TestContext.Current.CancellationToken
        };
        Assert.Equal(4096d, XPathExpression.Compile("count(//n)", cancellationToken: TestContext.Current.CancellationToken).Evaluate(document.RootElement, options).AsNumber());
        var result = XPathExpression.Compile("/r/n[3] | /r/n[1] | /r/n[3] | /r/n[2]", cancellationToken: TestContext.Current.CancellationToken).Evaluate(document.RootElement, options).AsNodeSet();
        Assert.Equal(["0", "1", "2"], result.Select(n => n.Node!.Value.Attributes[0].Value));
        Assert.Equal("4094", XPathExpression.Compile("string(/r/n[last()]/preceding-sibling::n[1]/@a)", cancellationToken: TestContext.Current.CancellationToken).Evaluate(document.RootElement, options).AsString());
    }

    [Fact]
    public void MergeSortPollsAndPreservesUnwrappedLimitAndCancellationFailures()
    {
        var document = Wide(512);
        var nav = new XPathNavigator(document, Budget());
        var input = nav.SelectAxis(XPathItem.FromNode(document.RootElement), XPathAxis.Child, Budget()).Reverse().ToArray();
        // Mapping passes; the finite cap is exhausted inside the merge loop.
        var limited = Budget(input.Length * 3L);
        var error = Assert.Throws<XPathEvaluationException>(() => nav.Normalize(input, limited));
        Assert.Equal("XPATH-EVALUATION-LIMIT", error.Diagnostic.Code);
        using var token = new CancellationTokenSource();
        var polling = new CancelAfterBudget(token, input.Length * 3L);
        var canceled = Assert.Throws<OperationCanceledException>(() => nav.Normalize(input, polling));
        Assert.Equal(token.Token, canceled.CancellationToken);
    }

    [Fact]
    public void ForeignIdentitiesAndDistinctNodeSetLimitsStillRefuse()
    {
        var document = Wide(8);
        var nav = new XPathNavigator(document, Budget());
        var foreign = Wide(1);
        Assert.Throws<XPathEvaluationException>(() => nav.Normalize([XPathItem.FromNode(foreign.RootElement)], Budget()));
        Assert.Throws<ArgumentException>(() => nav.Normalize([default], Budget()));
        var quotaDocument = Wide(3);
        var context = new XPathContext { Limits = new() { MaxInputItems = 100000, MaxNodeSetItems = 3, MaxOperations = 1000000 }, CancellationToken = TestContext.Current.CancellationToken };
        // Existing member repetitions do not consume new unique slots.
        Assert.Equal(3d, XPathExpression.Compile("count(/r/n[1] | /r/n[2] | /r/n[3] | /r/n[1])", cancellationToken: TestContext.Current.CancellationToken).Evaluate(quotaDocument.RootElement, context).AsNumber());
        Assert.Throws<XPathEvaluationException>(() => XPathExpression.Compile("/r/n[1] | /r/n[2] | /r/n[3] | /r/n[1]/x", cancellationToken: TestContext.Current.CancellationToken).Evaluate(quotaDocument.RootElement, context));
    }

    private sealed class CancelAfterBudget(CancellationTokenSource source, long at) : IXPathBudget
    {
        private long used;
        public CancellationToken CancellationToken => source.Token;
        public void ChargeOperations(long amount)
        {
            used += amount;
            if (used > at)
            {
                source.Cancel();
            }

            CancellationToken.ThrowIfCancellationRequested();
        }
        public void CheckNodeSetSize(int count) => CancellationToken.ThrowIfCancellationRequested();
        public void CheckStringSize(int count) => CancellationToken.ThrowIfCancellationRequested();
    }
}

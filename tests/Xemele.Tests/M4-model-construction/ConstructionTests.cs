using System.Collections;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;
namespace Xemele.Tests.M4ModelConstruction;
public sealed class ConstructionTests
{
    private static readonly XPathSourceSpan Span = new(0, 1);
    private static XPathLiteral Literal() => new(XPathValue.FromNumber(1), Span);
    [Theory]
    [InlineData("name")]
    [InlineData("根😀")]
    public void CancellationAwareNamesPreserveValidNames(string local)
    {
        var name = new XPathExpandedName("uri", local, default);
        Assert.Equal(new XPathExpandedName("uri", local), name);
        Assert.Equal(name, new XPathVariable(name, Span, default).Name);
        Assert.Equal(name, new XPathNodeTest(XPathNodeTestKind.Name, name, null, default).Name);
    }
    [Theory]
    [InlineData("")]
    [InlineData("a:b")]
    [InlineData("1name")]
    public void CancellationAwareNamesStillRejectInvalidNames(string local)
        => Assert.Throws<ArgumentException>(() => new XPathExpandedName("", local, default));
    [Fact]
    public void CancellationAwareNamesRejectMalformedScalarsAndDefaults()
    {
        Assert.Throws<ArgumentException>(() => new XPathExpandedName("", "a\uD800", default));
        Assert.Throws<ArgumentNullException>(() => new XPathExpandedName(null!, "a", default));
        Assert.Throws<ArgumentNullException>(() => new XPathExpandedName("", null!, default));
        Assert.Throws<ArgumentNullException>(() => new XPathVariable(default, Span, default));
        Assert.Throws<ArgumentException>(() => new XPathNodeTest(XPathNodeTestKind.ProcessingInstruction, default, "\uD800", default));
        Assert.Equal("", new XPathNodeTest(XPathNodeTestKind.ProcessingInstruction, default, "", default).ProcessingInstructionTarget);
    }
    [Fact]
    public void EveryNewConstructionBoundaryHonorsCancelledToken()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel(); var token = cts.Token;
        var name = new XPathExpandedName("", "name"); var literal = Literal();
        var test = new XPathNodeTest(XPathNodeTestKind.Node, default, null);
        var step = new XPathStep(XPathAxis.Child, test, [], Span);
        var actions = new Action[] {
            () => _ = new XPathExpandedName("", "name", token),
            () => _ = new XPathVariable(name, Span, token),
            () => _ = new XPathNodeTest(XPathNodeTestKind.Name, name, null, token),
            () => _ = new XPathNodeTest(XPathNodeTestKind.ProcessingInstruction, default, "", token),
            () => _ = new XPathCall(XPathCoreSignatures.ByName[new("", "true")], [], Span, token),
            () => _ = new XPathFilter(literal, [], Span, token),
            () => _ = new XPathStep(XPathAxis.Child, test, [], Span, token),
            () => _ = new XPathPath(false, null, [step], Span, token) };
        foreach (var action in actions) Assert.Equal(token, Assert.Throws<OperationCanceledException>(action).CancellationToken);
    }
    [Theory]
    [InlineData("call")]
    [InlineData("filter")]
    [InlineData("step")]
    [InlineData("path")]
    public void ActualCollectionCopyPollsAndReturnsNoPartialAst(string kind)
    {
        using var cts = new CancellationTokenSource(); var token = cts.Token;
        var literal = Literal(); var test = new XPathNodeTest(XPathNodeTestKind.Node, default, null);
        if (kind == "path")
        {
            var items = new CancelOnRead<XPathStep>(new XPathStep(XPathAxis.Child, test, [], Span), cts);
            Assert.Equal(token, Assert.Throws<OperationCanceledException>(() => new XPathPath(false, null, items, Span, token)).CancellationToken);
            Assert.Equal(2, items.Reads);
        }
        else
        {
            var items = new CancelOnRead<XPathAst>(literal, cts);
            Action construct = kind switch {
                "call" => () => _ = new XPathCall(XPathCoreSignatures.ByName[new("", "concat")], items, Span, token),
                "filter" => () => _ = new XPathFilter(literal, items, Span, token),
                _ => () => _ = new XPathStep(XPathAxis.Child, test, items, Span, token) };
            Assert.Equal(token, Assert.Throws<OperationCanceledException>(construct).CancellationToken);
            Assert.Equal(2, items.Reads);
        }
    }
    [Fact]
    public void SuccessfulCopyRemainsOwnedAndValidatesAllEntries()
    {
        var first = Literal(); XPathAst[] values = [first];
        var ast = new XPathFilter(first, values, Span, default); values[0] = Literal();
        Assert.Same(first, ast.Predicates[0]);
        Assert.Throws<ArgumentNullException>(() => new XPathFilter(first, [null!], Span, default));
        Assert.Throws<ArgumentException>(() => new XPathCall(XPathCoreSignatures.ByName[new("", "true")], [first], Span, default));
    }
    private sealed class CancelOnRead<T>(T value, CancellationTokenSource source) : IReadOnlyList<T>
    {
        public int Reads { get; private set; }
        public int Count => 1000;
        public T this[int index] { get { Reads++; if (index == 1) source.Cancel(); return value; } }
        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdIdentity;

public sealed class ParserBudgetTests
{
    private static readonly SchemaNamespaceContext Context = new(new Dictionary<string, string>(), "", null);
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LongAxisLookaheadIsChargedEvenWhenItFails(bool valid)
    {
        var expression = "child" + new string(' ', 8192) + (valid ? "::row" : ":row");
        var observed = new OperationTests.Ledger();
        if (valid)
        {
            Assert.NotNull(IdentityXPath.Compile(expression, false, Context, observed, default));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => IdentityXPath.Compile(expression, false, Context, observed, default));
        }

        Assert.True(observed.Work >= 8192);
        Assert.True(observed.IsComplete);
        var exact = new OperationTests.Ledger(observed.Work, observed.Components);
        if (valid)
        {
            Assert.NotNull(IdentityXPath.Compile(expression, false, Context, exact, default));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => IdentityXPath.Compile(expression, false, Context, exact, default));
            Assert.True(exact.IsComplete);
        }
        var shortWork = new OperationTests.Ledger(observed.Work - 1);
        Assert.Throws<SchemaCompileException>(() => IdentityXPath.Compile(expression, false, Context, shortWork, default));
        Assert.False(shortWork.IsComplete);
    }
    [Theory]
    [InlineData(".", "/row")]
    [InlineData("child", "::row")]
    [InlineData("attribute", "::id")]
    [InlineData(".", "//row")]
    public void ValidAxesHaveBoundedMidLookaheadCancellationWithNullCaps(string axis, string tail)
    {
        var expression = axis + new string(' ', 16384) + tail;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var op = new OperationTests.Ledger(token: cts.Token) { OnCharge = n => { if (n == 512) { cts.Cancel(); } }, Limits = new() { EvaluationWork = null } };
        Assert.Throws<OperationCanceledException>(() => IdentityXPath.Compile(expression, axis == "attribute", Context, op, default));
        Assert.Equal(512, op.Calls);
        Assert.True(op.IsComplete);
        Assert.Empty(op.Diagnostics);
    }
    [Fact]
    public void FailedAxisLookaheadPollsBeforeItFallsBackToNameParsing()
    {
        var expression = "child" + new string(' ', 16384) + ":row";
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var op = new OperationTests.Ledger(token: cts.Token) { OnCharge = n => { if (n == 512) { cts.Cancel(); } }, Limits = new() { EvaluationWork = null } };
        Assert.Throws<OperationCanceledException>(() => IdentityXPath.Compile(expression, false, Context, op, default));
        Assert.Equal(512, op.Calls);
        Assert.Empty(op.Diagnostics);
    }
    [Theory]
    [InlineData("a", "x")]
    [InlineData("根", "😀")]
    public void LongNcNamesHaveExactWorkAndScalarValidationCancellation(string first, string rest)
    {
        var name = first + string.Concat(Enumerable.Repeat(rest, 8192));
        var observed = new OperationTests.Ledger();
        Assert.NotNull(IdentityXPath.Compile(name, false, Context, observed, default));
        Assert.NotNull(IdentityXPath.Compile(name, false, Context, new OperationTests.Ledger(observed.Work, observed.Components), default));
        var insufficient = new OperationTests.Ledger(observed.Work - 1);
        Assert.Throws<SchemaCompileException>(() => IdentityXPath.Compile(name, false, Context, insufficient, default));
        Assert.False(insufficient.IsComplete);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        // The name token has exactly name.Length UTF-16 units. An additional256
        // charge calls exceed the fixed parser preamble and fall inside its
        // separate scalar-validation pass, including surrogate-pair input.
        var afterTokenScan = name.Length + 256;
        var cancel = new OperationTests.Ledger(token: cts.Token) { OnCharge = n => { if (n == afterTokenScan) { cts.Cancel(); } }, Limits = new() { EvaluationWork = null } };
        Assert.Throws<OperationCanceledException>(() => IdentityXPath.Compile(name, false, Context, cancel, default));
        Assert.Equal(afterTokenScan, cancel.Calls);
        Assert.Empty(cancel.Diagnostics);
    }
    [Theory]
    [InlineData("a\u0000")]
    [InlineData("a:b:c")]
    [InlineData("1a")]
    [InlineData("a!b")]
    [InlineData("a:b")]
    public void MalformedUtf16AndNamesAreStructuredIdentityErrors(string expression)
    {
        var op = new OperationTests.Ledger();
        Assert.Throws<SchemaCompileException>(() => IdentityXPath.Compile(expression, false, Context, op, default));
        Assert.True(op.IsComplete);
        Assert.Contains(op.Diagnostics, d => d.Code == "XSD.Identity");
    }
    [Theory]
    [InlineData("a", 0xd800, 0)]
    [InlineData("a", 0xdc00, 0)]
    [InlineData("a", 0xd800, 'x')]
    [InlineData("", 0xdc00, 0xd800)]
    public void MalformedSurrogatesAreConstructedAfterTestDataTransport(string prefix, int first, int second)
    {
        var expression = prefix + (char)first + (second == 0 ? "" : new string((char)second, 1));
        var op = new OperationTests.Ledger();
        Assert.Throws<SchemaCompileException>(() => IdentityXPath.Compile(expression, false, Context, op, default));
        Assert.True(op.IsComplete);
        Assert.Contains(op.Diagnostics, d => d.Code == "XSD.Identity");
    }
}

using System.Xml.XPath;
using Xunit;
using Xemele.XPath;
using Xemele.XPath.Internal;
namespace Xemele.Tests.M4Compile;

public sealed class ReferenceTests
{
    private static XPathAst Compile(string expression) => new XPathCompiler().Compile(expression, XPathStaticContext.FromOptions(null, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
    [Fact]
    public void ScalarAstSemanticsAgreeWithPinnedFrameworkParser()
    {
        // A test-only scalar interpreter checks grouping/operator shape against an
        // independent XPath 1.0 implementation; it has no production evaluator seam.
        var navigator = new XPathDocument(new StringReader("<r/>")).CreateNavigator();
        string[] atoms = ["0", "1", "2.5", "-3", "(4 - 1)"];
        string[] operators = ["+", "-", "*", "div", "mod", "=", "!=", "<", "<=", ">", ">=", "and", "or"];
        foreach (var left in atoms)
        {
            foreach (var op in operators)
            {
                foreach (var right in atoms)
                {
                    var expression = $"{left} {op} {right} + 2 * 3";
                    var actual = Scalar(Compile(expression));
                    var expected = navigator.Evaluate(expression);
                    if (expected is bool boolean)
                    {
                        Assert.Equal(boolean, actual.AsBoolean());
                    }
                    else
                    {
                        Assert.Equal((double)expected, actual.AsNumber());
                    }
                }
            }
        }

        foreach (var expression in new[] { "--1", "1 - 2 - 3", "1 div 2 mod 3", "1 = 2 < 3", "1 or 0 and 0", "2 * -(1+2)", "(1 + 2) * 3", "'根😀' = \"根😀\"" })
        {
            var actual = Scalar(Compile(expression));
            var expected = navigator.Evaluate(expression);
            if (expected is bool boolean)
            {
                Assert.Equal(boolean, actual.AsBoolean());
            }
            else
            {
                Assert.Equal((double)expected, actual.AsNumber());
            }
        }
    }
    [Fact]
    public void All105FrozenInventoryExpressionsHaveExpectedCompileOutcome()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory, "tests/corpora/xpath/inventory.json")))
        {
            directory = System.IO.Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        using var inventory = System.Text.Json.JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(directory, "tests/corpora/xpath/inventory.json")));
        var context = XPathStaticContext.FromOptions(new()
        {
            Namespaces = new Dictionary<string, string> { { "p", "urn:p" } }
        }, TestContext.Current.CancellationToken);
        var count = 0;
        foreach (var group in new[] { "axes", "operators", "functions", "cases" })
        {
            foreach (var item in inventory.RootElement.GetProperty(group).EnumerateArray())
            {
                var expression = item.GetProperty("expression").GetString()!;
                var expected = item.GetProperty("expected");
                var code = expected.ValueKind == System.Text.Json.JsonValueKind.String ? expected.GetString() : null;
                if (code is "XPATH-SYNTAX" or "XPATH-PREFIX" or "XPATH-FUNCTION" or "XPATH-ARITY")
                {
                    Assert.Equal(code, Assert.Throws<XPathCompileException>(() => new XPathCompiler().Compile(expression, context, TestContext.Current.CancellationToken)).Diagnostic.Code);
                }
                else
                {
                    Assert.NotNull(new XPathCompiler().Compile(expression, context, TestContext.Current.CancellationToken));
                }

                count++;
            }
        }

        Assert.Equal(105, count);
    }
    private static XPathValue Scalar(XPathAst ast)
    {
        if (ast is XPathLiteral literal)
        {
            return literal.Value;
        }

        if (ast is XPathUnaryMinus minus)
        {
            return XPathValue.FromNumber(-Scalar(minus.Operand).AsNumber());
        }

        var binary = Assert.IsType<XPathBinary>(ast);
        var left = Scalar(binary.Left);
        var right = Scalar(binary.Right);
        double a = left.AsNumber(), b = right.AsNumber();
        return binary.Operator switch
        {
            XPathBinaryOperator.Add => XPathValue.FromNumber(a + b),
            XPathBinaryOperator.Subtract => XPathValue.FromNumber(a - b),
            XPathBinaryOperator.Multiply => XPathValue.FromNumber(a * b),
            XPathBinaryOperator.Divide => XPathValue.FromNumber(a / b),
            XPathBinaryOperator.Modulo => XPathValue.FromNumber(a % b),
            XPathBinaryOperator.Or => XPathValue.FromBoolean(left.AsBoolean() || right.AsBoolean()),
            XPathBinaryOperator.And => XPathValue.FromBoolean(left.AsBoolean() && right.AsBoolean()),
            XPathBinaryOperator.Equal => XPathValue.FromBoolean(left.Kind == XPathValueKind.String && right.Kind == XPathValueKind.String ? left.AsString() == right.AsString() : a == b),
            XPathBinaryOperator.NotEqual => XPathValue.FromBoolean(a != b),
            XPathBinaryOperator.Less => XPathValue.FromBoolean(a < b),
            XPathBinaryOperator.LessOrEqual => XPathValue.FromBoolean(a <= b),
            XPathBinaryOperator.Greater => XPathValue.FromBoolean(a > b),
            XPathBinaryOperator.GreaterOrEqual => XPathValue.FromBoolean(a >= b),
            _ => throw new Exception("Outside scalar oracle.")
        };
    }
    [Fact]
    public async Task CancellationDuringLongLexicalWorkReturnsSuppliedToken()
    {
        var options = new XPathCompileOptions { Limits = new() { MaxExpressionCharacters = null, MaxTokens = null, MaxAstNodes = null, MaxDepth = null } };
        var context = XPathStaticContext.FromOptions(options, TestContext.Current.CancellationToken);
        foreach (var expression in new[] { new string(' ', 8_000_000) + "1", "'" + new string('根', 4_000_000) + "'", new string('名', 4_000_000) })
        {
            using var token = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            using var started = new ManualResetEventSlim();
            var work = Task.Factory.StartNew(() => { started.Set(); return new XPathCompiler().Compile(expression, context, token.Token); },
                TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            started.Wait(TestContext.Current.CancellationToken);
            // A millisecond timer can resume after the whole scan on Windows.
            // Keep the original interval without timer or threadpool scheduling.
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            while (System.Diagnostics.Stopwatch.GetElapsedTime(start) < TimeSpan.FromMilliseconds(1))
            {
                TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                Thread.SpinWait(64);
            }
            token.Cancel();
            var error = await Assert.ThrowsAsync<OperationCanceledException>(async () => await work.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.Equal(token.Token, error.CancellationToken);
        }
    }
}

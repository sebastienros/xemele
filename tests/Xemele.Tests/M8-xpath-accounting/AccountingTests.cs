using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;
namespace Xemele.Tests.M8XPathAccounting;

public sealed class AccountingTests
{
    private static readonly XPathExpandedName Extension = new("urn:e", "f");
    private static readonly XPathCompileOptions Extensions = new()
    {
        Namespaces = new Dictionary<string, string> { ["e"] = "urn:e" },
        Functions = [new(Extension, 0, 0)]
    };
    private static XmlDocument Parse(string xml) => XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
    private static XPathExpression Compile(string expression, XPathCompileOptions? options = null) =>
        XPathExpression.Compile(expression, options, TestContext.Current.CancellationToken);
    private static XPathValue Evaluate(XPathExpression expression, XmlNode node, bool item, XPathContext? options, out long used) =>
        item ? XPathEvaluationAccounting.EvaluateWithAccounting(expression, XPathItem.FromNode(node), options, out used)
             : XPathEvaluationAccounting.EvaluateWithAccounting(expression, node, options, out used);
    private static XPathContext WithFunction(XPathExtensionFunction function, long? cap = null) => new()
    {
        Functions = new Dictionary<XPathExpandedName, XPathExtensionFunction> { [Extension] = function },
        Limits = new()
        {
            MaxOperations = cap
        },
        CancellationToken = TestContext.Current.CancellationToken
    };
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulRunsHaveExactIndependentBudgetsAndOwnedResults(bool item)
    {
        var root = Parse("<r><a>1</a><a>2</a></r>").RootElement;
        foreach (var source in new[] { "1 + 2 * 3", "/r/a", "/r/a[1]", "string(/r/a)", "/r/a != /r/a" })
        {
            var expression = Compile(source);
            var actual = Evaluate(expression, root, item, null, out var used);
            Assert.True(used > 0);
            var expected = item ? expression.Evaluate(XPathItem.FromNode(root)) : expression.Evaluate(root);
            AssertValue(expected, actual);
            AssertValue(actual, Evaluate(expression, root, item, new()
            {
                Limits = new()
                {
                    MaxOperations = used
                }
            }, out var exact));
            Assert.Equal(used, exact);
            long rejected = -1;
            var error = Assert.Throws<XPathEvaluationException>(() => Evaluate(expression, root, item,
                new()
                {
                    Limits = new()
                    {
                        MaxOperations = used - 1
                    }
                }, out rejected));
            Assert.Equal("XPATH-EVALUATION-LIMIT", error.Diagnostic.Code);
            Assert.InRange(rejected, 1, used - 1);
            Evaluate(expression, root, item, null, out var again);
            Assert.Equal(used, again);
        }
    }
    [Fact]
    public void AttributeNamespaceAndTextItemContextsUseTheExistingBindingPath()
    {
        var root = Parse("<r k='v' xmlns:p='urn:p'>A<![CDATA[B]]></r>").RootElement;
        var expression = Compile("string(.)");
        foreach (var context in new[] { XPathItem.FromAttribute(root.Attributes[0]), XPathItem.FromNamespace(root, "p"), XPathItem.FromNode(root.FirstChild.NextSibling) })
        {
            var result = XPathEvaluationAccounting.EvaluateWithAccounting(expression, context, null, out var used);
            Assert.Equal(expression.Evaluate(context).AsString(), result.AsString());
            Assert.True(used > 0);
            Assert.Equal(result.AsString(), expression.EvaluateWithAccounting(context, new()
            {
                Limits = new()
                {
                    MaxOperations = used
                }
            }, out var exact).AsString());
            Assert.Equal(used, exact);
        }
    }
    [Fact]
    public void BeforeBudgetCreationCountsZeroAndPreservesContextSpecificGuardOrder()
    {
        var expression = Compile("1");
        long used = -1;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var options = new XPathContext { CancellationToken = canceled.Token };
        Assert.Throws<OperationCanceledException>(() => expression.EvaluateWithAccounting(default(XmlNode), options, out used));
        Assert.Equal(0, used);
        used = -1;
        Assert.Throws<ArgumentException>(() => expression.EvaluateWithAccounting(default(XPathItem), options, out used));
        Assert.Equal(0, used);
        foreach (var item in new[] { false, true })
        {
            used = -1;
            Assert.Throws<OperationCanceledException>(() => Evaluate(expression, new XmlDocument().DocumentNode, item, options, out used));
            Assert.Equal(0, used);
        }
        used = -1;
        Assert.Throws<ArgumentException>(() => expression.EvaluateWithAccounting(default(XmlNode), null, out used));
        Assert.Equal(0, used);
    }
    [Fact]
    public void DetachedNodeRefusalAccountsConnectivityBeforeSnapshotOrDispatch()
    {
        var document = Parse("<r><a/><a/></r>");
        long used = -1;
        var called = false;
        var options = WithFunction((_, _) => { called = true; return default; }) with
        {
            Limits = new()
            {
                MaxInputItems = 1
            }
        };
        Assert.Throws<ArgumentException>(() => Compile("e:f()", Extensions).EvaluateWithAccounting(document.CreateElement("detached"), options, out used));
        Assert.Equal(1, used);
        Assert.False(called);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetupEnumerationAndCallbackExceptionsPreserveIdentityAndActualCharges(bool item)
    {
        var root = Parse("<r/>").RootElement;
        Exception[] errors = [new InvalidOperationException("caller"), new OperationCanceledException(),
            new XPathEvaluationException(new("XPATH-EVALUATION-LIMIT", XPathDiagnosticCategory.EvaluationLimit, "caller", new(1, 1)))];
        foreach (var error in errors)
        {
            foreach (var functions in new[] { false, true })
            {
                long used = -1;
                var options = functions ? new XPathContext { Functions = new ThrowingDictionary<XPathExtensionFunction>(error) }
                                        : new XPathContext { Variables = new ThrowingDictionary<XPathValue>(error) };
                Assert.Same(error, Assert.ThrowsAny<Exception>(() => Evaluate(Compile("1"), root, item, options, out used)));
                Assert.Equal(item ? 0 : 2, used);
            }
            var expression = Compile("e:f()", Extensions);
            long baseline = -1, charged = -1;
            Assert.Same(error, Assert.ThrowsAny<Exception>(() => Evaluate(expression, root, item, WithFunction((_, _) => throw error), out baseline)));
            Assert.Same(error, Assert.ThrowsAny<Exception>(() => Evaluate(expression, root, item,
                WithFunction((context, _) => { context.ChargeOperations(7); throw error; }), out charged)));
            Assert.Equal(baseline + 7, charged);
            long rejected = -1;
            Assert.Equal("XPATH-EVALUATION-LIMIT", Assert.Throws<XPathEvaluationException>(() => Evaluate(expression, root, item,
                WithFunction((context, _) => { context.ChargeOperations(7); throw error; }, baseline + 6), out rejected)).Diagnostic.Code);
            Assert.Equal(baseline, rejected); // The rejected seven-operation attempt is never charged.
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringSetupAndDispatchAccountsFinallyWithoutMaskingFailure(bool item)
    {
        var root = Parse("<r/>").RootElement;
        var expression = Compile("e:f()", Extensions);
        using var canceled = new CancellationTokenSource();
        long used = -1;
        var options = WithFunction((context, _) => { context.ChargeOperations(7); canceled.Cancel(); return XPathValue.FromNumber(1); }) with
        {
            CancellationToken = canceled.Token
        };
        var budget = new SharedBudget(null, canceled.Token);
        var error = Assert.Throws<OperationCanceledException>(() => budget.Evaluate(expression, root, item, options));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.True(budget.Used > 7);
        Assert.False(budget.IsComplete); // Final debit cannot conceal primary cancellation.
        using var setup = new CancellationTokenSource();
        var setupOptions = new XPathContext { CancellationToken = setup.Token, Variables = new ThrowingDictionary<XPathValue>(new OperationCanceledException(setup.Token), setup.Cancel) };
        Assert.Throws<OperationCanceledException>(() => Evaluate(Compile("1"), root, item, setupOptions, out used));
        Assert.Equal(item ? 0 : 2, used);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingLimitsAndEvaluationDiagnosticsRemainVisible(bool item)
    {
        var root = Parse("<r><a>text</a><a/></r>").RootElement;
        foreach (var limits in new[] { new XPathEvaluationLimits { MaxInputItems = 1 }, new() { MaxInputCharacters = 1 },
            new() { MaxNodeSetItems = 1 }, new() { MaxStringCharacters = 1 } })
        {
            var expression = Compile("/r/a");
            var options = new XPathContext { Limits = limits };
            long used = -1;
            var expected = Assert.Throws<XPathEvaluationException>(() => { if (item) { expression.Evaluate(XPathItem.FromNode(root), options); } else { expression.Evaluate(root, options); } });
            var actual = Assert.Throws<XPathEvaluationException>(() => Evaluate(expression, root, item, options, out used));
            Assert.Equal(expected.Diagnostic, actual.Diagnostic);
            Assert.True(used > 0);
        }
        foreach (var source in new[] { "$missing", "1 | 2", "e:f()" })
        {
            var expression = Compile(source, Extensions);
            long used = -1;
            var expected = Assert.Throws<XPathEvaluationException>(() => { if (item) { expression.Evaluate(XPathItem.FromNode(root)); } else { expression.Evaluate(root); } });
            Assert.Equal(expected.Diagnostic, Assert.Throws<XPathEvaluationException>(() => Evaluate(expression, root, item, null, out used)).Diagnostic);
            Assert.True(used > 0);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutationStillUsesEntrySnapshotsAndLaterRunsObserveTheLiveTree(bool item)
    {
        var document = Parse("<r><a>old</a><b/></r>");
        var root = document.RootElement;
        var a = root.FirstChild;
        var variable = new XPathExpandedName("", "value");
        var variables = new Dictionary<XPathExpandedName, XPathValue> { [variable] = XPathValue.FromNodeSet([XPathItem.FromNode(a)]) };
        var functions = new Dictionary<XPathExpandedName, XPathExtensionFunction>();
        functions[Extension] = (_, _) => { variables.Clear(); functions.Clear(); var text = a.FirstChild; text.Value = "new"; a.Remove(); return XPathValue.FromNumber(1); };
        var options = new XPathContext { Variables = variables, Functions = functions };
        var expression = Compile("e:f() + count($value | /r/a)", Extensions);
        Assert.Equal(2, Evaluate(expression, root, item, options, out var used).AsNumber());
        Assert.True(used > 0);
        Assert.Equal(0, Evaluate(Compile("count(/r/a)"), root, item, null, out _).AsNumber());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeveralExpressionsConsumeOneExactSharedBudgetAndRefuseBeforeZeroCap(bool item)
    {
        var root = Parse("<r><a>1</a><a>2</a></r>").RootElement;
        var expressions = new[] { Compile("1"), Compile("count(/r/a)"), Compile("string(/r/a)") };
        var counts = expressions.Select(e => { Evaluate(e, root, item, null, out var used); return used; }).ToArray();
        var exact = new SharedBudget(counts.Sum());
        foreach (var expression in expressions)
        {
            exact.Evaluate(expression, root, item, new()
            {
                Limits = new()
                {
                    MaxOperations = null
                }
            });
        }

        Assert.Equal(counts.Sum(), exact.Used);
        Assert.Equal(0, exact.Remaining);
        Assert.True(exact.IsComplete);
        Assert.Throws<InvalidOperationException>(() => exact.Evaluate(expressions[0], root, item, new()));
        Assert.Equal(counts.Sum(), exact.Used);
        Assert.False(exact.IsComplete);
        Assert.Equal(3, exact.Starts);
        var shortBudget = new SharedBudget(counts.Sum() - 1);
        shortBudget.Evaluate(expressions[0], root, item, new());
        shortBudget.Evaluate(expressions[1], root, item, new());
        Assert.Throws<XPathEvaluationException>(() => shortBudget.Evaluate(expressions[2], root, item, new()));
        Assert.InRange(shortBudget.Used, counts[0] + counts[1], counts.Sum() - 1);
        Assert.False(shortBudget.IsComplete);
        var limited = new SharedBudget(counts.Sum());
        Assert.Throws<XPathEvaluationException>(() => limited.Evaluate(expressions[1], root, item, new() { Limits = new() { MaxOperations = counts[1] - 1 } }));
        Assert.InRange(limited.Used, 1, counts[1] - 1);
        var unlimited = new SharedBudget(null);
        foreach (var expression in expressions)
        {
            unlimited.Evaluate(expression, root, item, new());
        }

        Assert.Null(unlimited.Remaining);
        Assert.Equal(counts.Sum(), unlimited.Used);
        var noCharge = new SharedBudget(counts[0]);
        Assert.Throws<ArgumentException>(() => noCharge.Evaluate(expressions[0], default, item, new()));
        Assert.Equal(0, noCharge.Used);
    }
    // A test-local enclosing budget models the frozen join without depending on M8-model.
    private sealed class SharedBudget(long? cap, CancellationToken token = default)
    {
        internal long Used
        {
            get; private set;
        }
        internal long? Remaining => cap is long value ? value - Used : null;
        internal bool IsComplete { get; private set; } = true;
        internal int Starts
        {
            get; private set;
        }
        internal XPathValue Evaluate(XPathExpression expression, XmlNode node, bool item, XPathContext options)
        {
            long? remaining = Remaining, perExpression = options.Limits.MaxOperations;
            var allowance = remaining is long r && perExpression is long p ? Math.Min(r, p) : remaining ?? perExpression;
            if (allowance is <= 0)
            {
                IsComplete = false;
                throw new InvalidOperationException("Shared work exhausted.");
            }
            long used = 0;
            Starts++;
            try
            {
                return AccountingTests.Evaluate(expression, node, item,
                    options with
                    {
                        Limits = options.Limits with
                        {
                            MaxOperations = allowance
                        },
                        CancellationToken = token
                    }, out used);
            }
            catch { IsComplete = false; throw; }
            finally
            {
                Used = checked(Used + used);
                if (token.IsCancellationRequested)
                {
                    IsComplete = false;
                }
            }
        }
    }
    private static void AssertValue(XPathValue expected, XPathValue actual)
    {
        Assert.Equal(expected.Kind, actual.Kind);
        switch (expected.Kind)
        {
            case XPathValueKind.Number:
                Assert.Equal(expected.AsNumber(), actual.AsNumber());
                break;
            case XPathValueKind.String:
                Assert.Equal(expected.AsString(), actual.AsString());
                break;
            case XPathValueKind.Boolean:
                Assert.Equal(expected.AsBoolean(), actual.AsBoolean());
                break;
            case XPathValueKind.NodeSet:
                Assert.Equal(expected.AsNodeSet().Select(n => (n.Kind, n.Name, n.StringValue)), actual.AsNodeSet().Select(n => (n.Kind, n.Name, n.StringValue)));
                break;
        }
    }
    private sealed class ThrowingDictionary<T>(Exception error, Action? beforeThrow = null) : IReadOnlyDictionary<XPathExpandedName, T>
    {
        public int Count => 1;
        public IEnumerable<XPathExpandedName> Keys => throw error;
        public IEnumerable<T> Values => throw error;
        public T this[XPathExpandedName key] => throw error;
        public bool ContainsKey(XPathExpandedName key) => throw error;
        public bool TryGetValue(XPathExpandedName key, out T value) => throw error;
        public IEnumerator<KeyValuePair<XPathExpandedName, T>> GetEnumerator()
        {
            beforeThrow?.Invoke();
            throw error;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

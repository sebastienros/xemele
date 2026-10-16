using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;
using ReferenceDocument = System.Xml.XPath.XPathDocument;
namespace Xemele.Tests.M4Evaluate;

public sealed class EvaluationTests
{
    private static XPathExpression Compile(string source, XPathCompileOptions? options = null) => XPathExpression.Compile(source, options, TestContext.Current.CancellationToken);
    private static XmlDocument Parse(string source) => XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken);
    private const string Xml = "<r xmlns:p='urn:p' xml:lang='en-US'><a k='1'>1</a><a k='2'>2<b>3</b></a><p:c p:k='4'>4</p:c><!--note--><?go data?><z/></r>";
    private static readonly XPathCompileOptions Options = new() { Namespaces = new Dictionary<string, string> { ["p"] = "urn:p" } };
    public static IEnumerable<object[]> Expressions => new[] {
        "1 + 2 * 3", "8 - 3 - 1", "7 div 2", "-7 mod 2", "--3", "true() or false()", "false() and true()",
        "1 = '1'", "1 != 2", "1 < 2", "1 <= 1", "2 > 1", "2 >= 2", "'false' = true()", "0 div 0 = 0 div 0", "0 div 0 != 0 div 0",
        "/r/a = /r/a", "/r/a != /r/a", "/r/a < /r/p:c", "/r/a != 1", "/r/a = '1'", "/r/a = true()", "/r/missing != /r/a",
        "/r/a", "/r/a | /r/p:c | /r/a", "/r/*[position() = last()]", "/r/a[1]", "/r/a[2][1]", "(/r/a)[last()]",
        "/r/a[2]/preceding-sibling::*[1]", "(/r/p:c/preceding-sibling::*)[1]", "/r/a[2]/b/ancestor::*[1]", "(/r/a[2]/b/ancestor::*)[1]",
        "/r/a[1]/following::node()", "/r/p:c/preceding::node()", "/r/a[2]/ancestor-or-self::node()[2]", "/r/descendant-or-self::node()", "/r/a[2]/parent::node()", "/r/self::node()",
        "/r/a[1]/following-sibling::*", "/r/a/attribute::*", "/r/a[1]/@k/parent::*", "/r/text()", "/r/comment()", "/r/processing-instruction('go')", "//b", "/r/a[2]/./../p:c",
        "last()", "position()", "count(/r/a)", "id('missing')", "local-name(/r/p:c)", "namespace-uri(/r/p:c)", "name(/r/p:c)", "string(/r/a)",
        "concat('a','b','c')", "starts-with('abc','ab')", "contains('abc','b')", "substring-before('abc','b')", "substring-after('abc','b')", "substring('12345',1.5,2.6)",
        "string-length('abc')", "normalize-space('  A   B  ')", "translate('abba','ab','X')", "boolean(/r/a)", "not(/r/missing)", "true()", "false()", "lang('en')", "number(' -2.5 ')", "sum(/r/a/@k)", "floor(-1.2)", "ceiling(-1.2)", "round(-1.5)"
    }.Select(expression => new object[] { expression });
    [Theory, MemberData(nameof(Expressions))]
    public void PublicFacadeMatchesPinnedCoreReference(string expression)
    {
        var document = Parse(Xml);
        var actual = Compile(expression, Options).Evaluate(document.RootElement);
        var reference = new ReferenceDocument(new StringReader(Xml)).CreateNavigator();
        reference.MoveToChild("r", "");
        var resolver = new System.Xml.XmlNamespaceManager(new System.Xml.NameTable());
        resolver.AddNamespace("p", "urn:p");
        var compiled = reference.Compile(expression);
        compiled.SetContext(resolver);
        var expected = reference.Evaluate(compiled);
        switch (expected)
        {
            case bool flag:
                Assert.Equal(flag, actual.AsBoolean());
                break;
            case double number:
                Assert.Equal(number, actual.AsNumber());
                break;
            case string text:
                Assert.Equal(text, actual.AsString());
                break;
            case System.Xml.XPath.XPathNodeIterator iterator:
                var rows = new List<(string Name, string Text)>();
                while (iterator.MoveNext())
                {
                    rows.Add((iterator.Current!.Name, iterator.Current.Value));
                }

                Assert.Equal(rows, actual.AsNodeSet().Select(item => (item.Name, item.StringValue)));
                break;
            default:
                throw new InvalidOperationException();
        }
    }
    [Fact]
    public void AttributeNamespaceTextAndEmptyDocumentContexts()
    {
        var document = Parse("<r xmlns:p='urn:p' k='v'>A<![CDATA[B]]><a/></r>");
        Assert.Equal("r", Compile("name(..)").Evaluate(XPathItem.FromAttribute(document.RootElement.Attributes[0])).AsString());
        var ns = XPathItem.FromNamespace(document.RootElement, "p");
        Assert.Equal("urn:p", Compile("string(.)").Evaluate(ns).AsString());
        Assert.Equal("r", Compile("name(..)").Evaluate(ns).AsString());
        Assert.Equal(["p", "xml"], Compile("namespace::*").Evaluate(document.RootElement).AsNodeSet().Select(x => x.Name));
        Assert.Equal("AB", Compile("string(.)").Evaluate(document.RootElement.FirstChild.NextSibling).AsString());
        Assert.Equal(1, Compile("count(/)").Evaluate(new XmlDocument().DocumentNode).AsNumber());
        Assert.Throws<ArgumentException>(() => Compile(".").Evaluate(default(XmlNode)));
        Assert.Throws<ArgumentException>(() => Compile(".").Evaluate(default(XPathItem)));
        Assert.Throws<ArgumentException>(() => Compile(".").Evaluate(document.CreateElement("detached")));
    }
    [Fact]
    public void VariablesExtensionsCaptureBindingsAndRebindAllReturnedMetadata()
    {
        var document = Parse("<r><a>old</a><b/></r>");
        var a = document.RootElement.FirstChild;
        var old = XPathItem.FromNode(a);
        var textNode = a.FirstChild;
        textNode.Value = "entry";
        var key = new XPathExpandedName("", "value");
        var extension = new XPathExpandedName("urn:e", "mutate");
        var variables = new Dictionary<XPathExpandedName, XPathValue> { [key] = XPathValue.FromNodeSet([old]) };
        var functions = new Dictionary<XPathExpandedName, XPathExtensionFunction>();
        var namespaces = new Dictionary<string, string> { ["e"] = "urn:e" };
        var signatures = new List<XPathFunctionSignature> { new(extension, 1, 1) };
        var compiled = Compile("e:mutate($value) | /r/a", new()
        {
            Namespaces = namespaces,
            Functions = signatures
        });
        namespaces.Clear();
        signatures.Clear();
        functions[extension] = (focus, args) =>
        {
            Assert.Equal(1, focus.Position);
            Assert.Equal(1, focus.Size);
            Assert.Equal("entry", Assert.Single(args[0].AsNodeSet()).StringValue);
            Assert.Throws<NotSupportedException>(() => ((IList<XPathValue>)args).Clear());
            variables.Clear();
            functions.Clear();
            textNode.Value = "mutated";
            a.Remove();
            return args[0];
        };
        var result = compiled.Evaluate(document.RootElement, new()
        {
            Variables = variables,
            Functions = functions
        });
        Assert.Equal("entry", Assert.Single(result.AsNodeSet()).StringValue);
        Assert.Equal("mutated", old.Node!.Value.FirstChild.Value);
        Assert.Equal(0, Compile("count(/r/a)").Evaluate(document.RootElement).AsNumber());
        Assert.Equal("XPATH-CONTEXT", Assert.Throws<XPathEvaluationException>(() => compiled.Evaluate(old)).Diagnostic.Code);
        Assert.Equal("XPATH-NODESET", Assert.Throws<XPathEvaluationException>(() => Compile("$value").Evaluate(document.RootElement, new() { Variables = new Dictionary<XPathExpandedName, XPathValue> { { key, result } } })).Diagnostic.Code);
    }
    [Fact]
    public void ExtensionsRejectForeignOrNewIdentitiesAndCapReturns()
    {
        var document = Parse("<r/>");
        var foreign = Parse("<r/>");
        var name = new XPathExpandedName("urn:e", "f");
        var expression = Compile("e:f()", new()
        {
            Namespaces = new Dictionary<string, string> { { "e", "urn:e" } },
            Functions = [new(name, 0, 0)]
        });
        XPathValue Invoke(XPathExtensionFunction function, XPathEvaluationLimits? limits = null) => expression.Evaluate(document.RootElement, new()
        {
            Functions = new Dictionary<XPathExpandedName, XPathExtensionFunction> { { name, function } },
            Limits = limits ?? new()
        });
        Assert.Equal("XPATH-NODESET", Assert.Throws<XPathEvaluationException>(() => Invoke((_, _) => XPathValue.FromNodeSet([XPathItem.FromNode(foreign.RootElement)]))).Diagnostic.Code);
        Assert.Equal("XPATH-NODESET", Assert.Throws<XPathEvaluationException>(() => Invoke((_, _) => { var n = document.CreateElement("new"); document.RootElement.AppendChild(n); return XPathValue.FromNodeSet([XPathItem.FromNode(n)]); })).Diagnostic.Code);
        Assert.Equal("XPATH-EVALUATION-LIMIT", Assert.Throws<XPathEvaluationException>(() => Invoke((_, _) => XPathValue.FromString(new string('x', 101)), new() { MaxStringCharacters = 100 })).Diagnostic.Code);
        Assert.Equal("XPATH-EXTENSION", Assert.Throws<XPathEvaluationException>(() => expression.Evaluate(document.RootElement)).Diagnostic.Code);
    }
    [Fact]
    public void CallbackExceptionsPropagateByIdentityIncludingEngineShapedExceptions()
    {
        var name = new XPathExpandedName("urn:e", "f");
        var expression = Compile("1 + e:f()", new()
        {
            Namespaces = new Dictionary<string, string> { { "e", "urn:e" } },
            Functions = [new(name, 0, 0)]
        });
        Exception[] errors = [new InvalidOperationException("user"), new XPathEvaluationException(new("XPATH-TYPE", XPathDiagnosticCategory.Evaluation, "user", new(0, 1))), new XPathCompileException(new("XPATH-SYNTAX", XPathDiagnosticCategory.Syntax, "user", new(0, 1))), new OperationCanceledException()];
        foreach (var error in errors)
        {
            Assert.Same(error, Assert.ThrowsAny<Exception>(() => expression.Evaluate(Parse("<r/>").RootElement, new() { Functions = new Dictionary<XPathExpandedName, XPathExtensionFunction> { { name, (_, _) => throw error } } })));
        }
    }
    [Fact]
    public async Task CompiledExpressionsAreReusableAndConcurrentWithSeparateContexts()
    {
        var expression = Compile("sum(/r/a) + $x");
        var name = new XPathExpandedName("", "x");
        var outputs = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => expression.Evaluate(Parse("<r><a>2</a><a>3</a></r>").RootElement, new() { Variables = new Dictionary<XPathExpandedName, XPathValue> { { name, XPathValue.FromNumber(i) } } }).AsNumber(), TestContext.Current.CancellationToken)));
        Assert.Equal(Enumerable.Range(0, 24).Select(i => (double)i + 5), outputs);
    }
    [Fact]
    public void DiagnosticSpansShortCircuitAndSignedZero()
    {
        var root = Parse("<r/>").RootElement;
        foreach (var (source, code, start, length) in new[] { ("1 + $missing", "XPATH-VARIABLE", 4, 8), ("count(1)", "XPATH-TYPE", 0, 8), ("(1)/a", "XPATH-TYPE", 0, 5), ("1 | /", "XPATH-TYPE", 0, 5) })
        {
            var error = Assert.Throws<XPathEvaluationException>(() => Compile(source).Evaluate(root));
            Assert.Equal(code, error.Diagnostic.Code);
            Assert.Equal(new(start, length), error.Diagnostic.Span);
        }
        Assert.True(Compile("true() or $missing").Evaluate(root).AsBoolean());
        Assert.False(Compile("false() and $missing").Evaluate(root).AsBoolean());
        Assert.Equal(double.NegativeInfinity, Compile("1 div -0").Evaluate(root).AsNumber());
        Assert.Equal("0", Compile("string(-0)").Evaluate(root).AsString());
        Assert.True(double.IsNaN(Compile("0 div 0").Evaluate(root).AsNumber()));
        Assert.True(Compile("(/missing != /missing) = false()").Evaluate(root).AsBoolean());
    }
    [Fact]
    public void ExactAndOneOverLimitsApplyBeforeUserDispatchAndResultGrowth()
    {
        var document = Parse("<r/>");
        var root = document.RootElement;
        var token = TestContext.Current.CancellationToken;
        var snapshot = new XPathBudget(new()
        {
            MaxOperations = null
        }, new(0, 1), token);
        _ = new XPathNavigator(document, snapshot, new(0, 1));
        // Context ancestry and rebind have their own real operation charges; derive
        // the exact whole Evaluate threshold by the public success/failure boundary.
        var expression = Compile("1");
        long high = 1024, low = 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            try
            {
                expression.Evaluate(root, new()
                {
                    Limits = new()
                    {
                        MaxOperations = mid
                    }
                });
                high = mid;
            }
            catch (XPathEvaluationException) { low = mid + 1; }
        }
        Assert.Equal(1, expression.Evaluate(root, new()
        {
            Limits = new()
            {
                MaxOperations = low
            }
        }).AsNumber());
        Assert.Equal("XPATH-EVALUATION-LIMIT", Assert.Throws<XPathEvaluationException>(() => expression.Evaluate(root, new() { Limits = new() { MaxOperations = low - 1 } })).Diagnostic.Code);
        Assert.Equal(1, expression.Evaluate(root, new()
        {
            Limits = new()
            {
                MaxInputItems = snapshotInput(document).Items,
                MaxInputCharacters = snapshotInput(document).Chars
            }
        }).AsNumber());
        var (Items, Chars) = snapshotInput(document);
        Assert.Throws<XPathEvaluationException>(() => expression.Evaluate(root, new() { Limits = new() { MaxInputItems = Items - 1 } }));
        Assert.Throws<XPathEvaluationException>(() => expression.Evaluate(root, new() { Limits = new() { MaxInputCharacters = Chars - 1 } }));
        Assert.Equal("ab", Compile("concat('a','b')").Evaluate(new XmlDocument().DocumentNode, new()
        {
            Limits = new()
            {
                MaxStringCharacters = 2
            }
        }).AsString());
        Assert.Throws<XPathEvaluationException>(() => Compile("concat('a','b')").Evaluate(new XmlDocument().DocumentNode, new() { Limits = new() { MaxStringCharacters = 1 } }));
        var two = Parse("<r><a/><a/></r>").RootElement;
        Assert.Equal(2, Compile("/r/a").Evaluate(two, new()
        {
            Limits = new()
            {
                MaxNodeSetItems = 2
            }
        }).AsNodeSet().Count);
        Assert.Throws<XPathEvaluationException>(() => Compile("/r/a").Evaluate(two, new() { Limits = new() { MaxNodeSetItems = 1 } }));
        static (int Items, long Chars) snapshotInput(XmlDocument d)
        {
            var b = new XPathBudget(new()
            {
                MaxOperations = null
            }, default, default);
            var n = new XPathNavigator(d, b);
            return (n.InputItems, n.InputCharacters);
        }
    }
    [Fact]
    public void LongLeftDeepProgramsAndNestedPredicatesUseExplicitFrames()
    {
        var root = Parse("<r><a>2</a></r>").RootElement;
        var options = new XPathCompileOptions { Limits = new() { MaxExpressionCharacters = null, MaxTokens = null, MaxAstNodes = null, MaxDepth = null } };
        var longSource = string.Join("+", Enumerable.Repeat("1", 6000));
        Assert.Equal(6000, Compile(longSource, options).Evaluate(root, new()
        {
            Limits = new()
            {
                MaxOperations = null
            }
        }).AsNumber());
        var nested = "true()";
        for (var i = 0; i < 80; i++)
        {
            nested = "boolean(/r/a[" + nested + "])";
        }

        Assert.True(Compile(nested, options).Evaluate(root, new()
        {
            Limits = new()
            {
                MaxOperations = null
            }
        }).AsBoolean());
        Assert.Throws<XPathCompileException>(() => Compile(new string('(', 300) + "1" + new string(')', 300)));
        Assert.Throws<XPathEvaluationException>(() => Compile(longSource, options).Evaluate(root, new() { Limits = new() { MaxOperations = 100 } }));
    }
    [Fact]
    public void CancellationIsCheckedAtEntryAndAfterUserDispatch()
    {
        var root = Parse("<r/>").RootElement;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => XPathExpression.Compile("1", cancellationToken: canceled.Token)).CancellationToken);
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => Compile("1").Evaluate(root, new() { CancellationToken = canceled.Token })).CancellationToken);
        using var source = new CancellationTokenSource();
        var name = new XPathExpandedName("urn:e", "f");
        var expression = Compile("e:f()", new()
        {
            Namespaces = new Dictionary<string, string> { { "e", "urn:e" } },
            Functions = [new(name, 0, 0)]
        });
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => expression.Evaluate(root, new() { CancellationToken = source.Token, Functions = new Dictionary<XPathExpandedName, XPathExtensionFunction> { { name, (context, _) => { context.ChargeOperations(0); source.Cancel(); return XPathValue.FromNumber(1); } } } })).CancellationToken);
    }
}

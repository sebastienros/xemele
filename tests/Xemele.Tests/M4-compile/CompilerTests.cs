using Xunit;
using System.Globalization;
using Xemele.XPath;
using Xemele.XPath.Internal;
namespace Xemele.Tests.M4Compile;

public sealed class CompilerTests
{
    private static XPathAst Compile(string source, XPathCompileOptions? options = null) => new XPathCompiler().Compile(source, XPathStaticContext.FromOptions(options, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
    private static XPathCompileException Reject(string source, string code = "XPATH-SYNTAX", XPathCompileOptions? options = null)
    {
        var error = Assert.Throws<XPathCompileException>(() => Compile(source, options));
        Assert.Equal(code, error.Diagnostic.Code);
        Assert.InRange(error.Message.Length, 1, 256);
        return error;
    }
    private static XPathPath Path(string source) => Assert.IsType<XPathPath>(Compile(source));
    [Fact]
    public void EveryPrecedenceLevelAndLeftAssociativity()
    {
        var or = Assert.IsType<XPathBinary>(Compile("1 or 2 and 3 = 4 < 5 + 6 * -7 | /x"));
        Assert.Equal(XPathBinaryOperator.Or, or.Operator);
        var and = Assert.IsType<XPathBinary>(or.Right);
        Assert.Equal(XPathBinaryOperator.And, and.Operator);
        var eq = Assert.IsType<XPathBinary>(and.Right);
        Assert.Equal(XPathBinaryOperator.Equal, eq.Operator);
        var less = Assert.IsType<XPathBinary>(eq.Right);
        Assert.Equal(XPathBinaryOperator.Less, less.Operator);
        var plus = Assert.IsType<XPathBinary>(less.Right);
        Assert.Equal(XPathBinaryOperator.Add, plus.Operator);
        var multiply = Assert.IsType<XPathBinary>(plus.Right);
        Assert.Equal(XPathBinaryOperator.Multiply, multiply.Operator);
        var unary = Assert.IsType<XPathUnaryMinus>(multiply.Right);
        Assert.Equal(XPathBinaryOperator.Union, Assert.IsType<XPathBinary>(unary.Operand).Operator);
        foreach (var symbol in new[] { "or", "and", "=", "!=", "<", "<=", ">", ">=", "+", "-", "*", "div", "mod", "|" })
        {
            var repeated = Assert.IsType<XPathBinary>(Compile($"1 {symbol} 2 {symbol} 3"));
            Assert.IsType<XPathBinary>(repeated.Left);
            Assert.IsType<XPathLiteral>(repeated.Right);
            Assert.Equal(repeated.Operator, ((XPathBinary)repeated.Left).Operator);
        }
        Assert.IsType<XPathUnaryMinus>(Assert.IsType<XPathUnaryMinus>(Compile("--2")).Operand);
        Assert.Equal(XPathBinaryOperator.Multiply, Assert.IsType<XPathBinary>(Compile("(-1) * 2")).Operator);
    }
    [Fact]
    public void AllAxesAndNodeTestsAndAbbreviations()
    {
        string[] names = ["child", "descendant", "parent", "ancestor", "following-sibling", "preceding-sibling", "following", "preceding", "attribute", "namespace", "self", "descendant-or-self", "ancestor-or-self"];
        for (var i = 0; i < names.Length; i++)
        {
            Assert.Equal((XPathAxis)i, Assert.Single(Path(names[i] + "::node()").Steps).Axis);
        }

        Assert.Empty(Path("/").Steps);
        Assert.True(Path("/").Absolute);
        var path = Path("//a/@b/./../c//text()");
        Assert.True(path.Absolute);
        Assert.Equal([XPathAxis.DescendantOrSelf, XPathAxis.Child, XPathAxis.Attribute, XPathAxis.Self, XPathAxis.Parent, XPathAxis.Child, XPathAxis.DescendantOrSelf, XPathAxis.Child], path.Steps.Select(s => s.Axis));
        Assert.Equal(XPathNodeTestKind.Node, path.Steps[0].Test.Kind);
        Assert.Equal(new XPathSourceSpan(0, 2), path.Steps[0].Span);
        Assert.Equal(XPathNodeTestKind.Text, path.Steps[^1].Test.Kind);
        foreach (var pair in new[] { ("*", XPathNodeTestKind.AnyPrincipal), ("node()", XPathNodeTestKind.Node), ("text()", XPathNodeTestKind.Text), ("comment()", XPathNodeTestKind.Comment), ("processing-instruction()", XPathNodeTestKind.ProcessingInstruction), ("a", XPathNodeTestKind.Name) })
        {
            Assert.Equal(pair.Item2, Assert.Single(Path(pair.Item1).Steps).Test.Kind);
        }

        Assert.Equal("根😀", Assert.Single(Path("processing-instruction('根😀')").Steps).Test.ProcessingInstructionTarget);
        var ns = new XPathCompileOptions { Namespaces = new Dictionary<string, string> { ["p"] = "urn:p" } };
        var wildcard = Assert.Single(Assert.IsType<XPathPath>(Compile("p:*", ns)).Steps).Test;
        Assert.Equal(XPathNodeTestKind.NamespaceWildcard, wildcard.Kind);
        Assert.Equal("urn:p", wildcard.Name.NamespaceUri);
        Assert.Equal("", wildcard.Name.LocalName);
        Assert.Equal("", Assert.Single(Path("root").Steps).Test.Name.NamespaceUri);
    }
    [Fact]
    public void PrimaryFilterFunctionAndPathGrammar()
    {
        var path = Path("(//a)[1][position()=last()]/b[c]/d");
        Assert.False(path.Absolute);
        var filter = Assert.IsType<XPathFilter>(path.Input);
        Assert.Equal(2, filter.Predicates.Count);
        Assert.True(Assert.IsType<XPathPath>(filter.Input).Absolute);
        Assert.Equal(2, path.Steps.Count);
        Assert.Single(path.Steps[0].Predicates);
        Assert.Equal(3, Assert.IsType<XPathCall>(Compile("concat('a',string(2),'b')")).Arguments.Count);
        Assert.IsType<XPathPath>(Compile("$v//a"));
        Assert.IsType<XPathFilter>(Compile("$v[1]"));
        Assert.IsType<XPathFilter>(Compile("'s'[1]"));
        Assert.IsType<XPathLiteral>(Compile("((1))"));
        Assert.IsType<XPathPath>(Compile("(a)/b"));
        Assert.Equal("v", Assert.IsType<XPathVariable>(Compile("$v")).Name.LocalName);
        Assert.Equal(2, Assert.Single(Path("a[b[c]][d]").Steps).Predicates.Count);
    }
    [Fact]
    public void LexicalAmbiguitiesRemainContextSensitive()
    {
        foreach (var name in new[] { "and", "or", "div", "mod", "a-b", "node", "text", "child", "根😀" })
        {
            Assert.Equal(name, Assert.Single(Path(name).Steps).Test.Name.LocalName);
            Assert.Equal(name, Assert.Single(Path("@" + name).Steps).Test.Name.LocalName);
            Assert.Equal(name, Assert.Single(Path("child::" + name).Steps).Test.Name.LocalName);
        }
        Assert.Equal(XPathBinaryOperator.Multiply, Assert.IsType<XPathBinary>(Compile("* * *")).Operator);
        Assert.Equal(XPathBinaryOperator.And, Assert.IsType<XPathBinary>(Compile("and and and")).Operator);
        Assert.Equal(XPathBinaryOperator.Divide, Assert.IsType<XPathBinary>(Compile("div div mod")).Operator);
        Assert.Equal("a-b", Assert.Single(Path("a-b").Steps).Test.Name.LocalName);
        Assert.Equal(XPathBinaryOperator.Subtract, Assert.IsType<XPathBinary>(Compile("a - b")).Operator);
        Assert.IsType<XPathCall>(Compile("string ('s')"));
        Assert.Equal(XPathAxis.Child, Assert.Single(Path("child :: a").Steps).Axis);
    }
    [Fact]
    public void LiteralNumbersAreInvariantAndFullyOwned()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "fr-FR", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (var pair in new[] { (".5", .5), ("5.", 5d), ("123.45", 123.45), ("0002", 2d) })
                {
                    Assert.Equal(pair.Item2, Assert.IsType<XPathLiteral>(Compile(pair.Item1)).Value.AsNumber());
                }

                Assert.Equal(double.PositiveInfinity, Assert.IsType<XPathLiteral>(Compile(new string('9', 400))).Value.AsNumber());
                Assert.Equal("a\"b根😀", Assert.IsType<XPathLiteral>(Compile("'a\"b根😀'")).Value.AsString());
                Assert.Equal("a'b", Assert.IsType<XPathLiteral>(Compile("\"a'b\"")).Value.AsString());
                Assert.Equal("", Assert.IsType<XPathLiteral>(Compile("''")).Value.AsString());
                Assert.Equal("&amp;", Assert.IsType<XPathLiteral>(Compile("'&amp;'")).Value.AsString());
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a/")]
    [InlineData("//")]
    [InlineData("a//")]
    [InlineData("a///b")]
    [InlineData("a[]")]
    [InlineData("a[1")]
    [InlineData("(1")]
    [InlineData("1)")]
    [InlineData("a b")]
    [InlineData("1e2")]
    [InlineData("+1")]
    [InlineData("1..2")]
    [InlineData("p : a")]
    [InlineData("p: a")]
    [InlineData("p:")]
    [InlineData("$*")]
    [InlineData("$p:*")]
    [InlineData("child : : a")]
    [InlineData("invalid::a")]
    [InlineData("node('a')")]
    [InlineData("text(1)")]
    [InlineData("comment('x')")]
    [InlineData("processing-instruction(1)")]
    [InlineData("processing-instruction('a','b')")]
    [InlineData(".[1]")]
    [InlineData("..[1]")]
    [InlineData("concat(1,)")]
    [InlineData("true(,)")]
    [InlineData("true(1 2)")]
    [InlineData("a=>b")]
    [InlineData("(:x:)")]
    [InlineData("'no end")]
    [InlineData("\"no end")]
    [InlineData("p:*()")]
    [InlineData("@string()")]
    [InlineData("child::string()")]
    [InlineData("a\u00a0b")]
    [InlineData("1E+2")]
    [InlineData("1 & 2")]
    [InlineData("a:1")]
    [InlineData("a:b:c")]
    public void MalformedGrammarRejects(string source) => Reject(source);
    [Fact]
    public void UnicodeErrorsAndEofHaveOriginalUtf16Spans()
    {
        foreach (var source in new[] { "\ud800", "\udc00", "a\ud800b", "'\ud800'", "'\u0000'", "a\ufffe" })
        {
            Reject(source);
        }

        Assert.Equal(new XPathSourceSpan(4, 1), Reject("根😀/]").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(4, 0), Reject("根😀/").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(4, 0), Reject("a[1 ").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(0, 4), Reject("'根😀").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(2, 1), Reject("1 ! 2").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(2, 1), Reject("1 : 2").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(0, 7), Compile("(1)+(2)").Span);
        Assert.Equal(new XPathSourceSpan(0, 4), Compile("-(1)").Span);
        var path = Path(" \t根😀[ 2 ]/b ");
        Assert.Equal(new XPathSourceSpan(2, 10), path.Span);
        Assert.Equal(new XPathSourceSpan(2, 8), path.Steps[0].Span);
        Assert.Equal(new XPathSourceSpan(7, 1), path.Steps[0].Predicates[0].Span);
    }
    [Fact]
    public void StaticResolutionAndSyntaxBeforeResolution()
    {
        var options = new XPathCompileOptions { Namespaces = new Dictionary<string, string> { { "p", "urn:p" } }, Functions = [new(new("urn:p", "f"), 1, 2)] };
        Assert.Equal(new XPathExpandedName("urn:p", "v"), Assert.IsType<XPathVariable>(Compile("$p:v", options)).Name);
        Assert.Equal("urn:p", Assert.IsType<XPathCall>(Compile("p:f(1,2)", options)).Signature.Name.NamespaceUri);
        Assert.Equal(new XPathSourceSpan(0, 3), Reject("p:a", "XPATH-PREFIX").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(0, 4), Reject("$p:a", "XPATH-PREFIX").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(0, 9), Reject("unknown()", "XPATH-FUNCTION").Diagnostic.Span);
        Assert.Equal(new XPathSourceSpan(0, 7), Reject("count() ", "XPATH-ARITY").Diagnostic.Span);
        Reject("p:f()", "XPATH-ARITY", options);
        Reject("p:f(1,2,3)", "XPATH-ARITY", options);
        foreach (var signature in XPathCoreSignatures.All)
        {
            var args = string.Join(",", Enumerable.Repeat("1", signature.MinArguments));
            Assert.Equal(signature, Assert.IsType<XPathCall>(Compile(signature.Name.LocalName + "(" + args + ")")).Signature);
            if (signature.MinArguments > 0)
            {
                Reject(signature.Name.LocalName + "()", "XPATH-ARITY");
            }

            if (signature.MaxArguments is int max)
            {
                Reject(signature.Name.LocalName + "(" + string.Join(",", Enumerable.Repeat("1", max + 1)) + ")", "XPATH-ARITY");
            }
        }
        foreach (var bad in new[] { "unknown() => 1", "p:a => 1", "count() => 1", "p:f(1) => 1", "unknown()[1" })
        {
            Reject(bad);
        }

        Assert.Equal(TreeValidation.XmlUri, Assert.Single(Path("xml:lang").Steps).Test.Name.NamespaceUri);
    }
    [Fact]
    public void ExactAndOneOverEveryCompileQuota()
    {
        static XPathCompileOptions Limits(XPathCompileLimits limits) => new()
        {
            Limits = limits
        };
        Compile("1+2", Limits(new()
        {
            MaxExpressionCharacters = 3
        }));
        Reject("1+2", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxExpressionCharacters = 2
        }));
        Compile(" 1 + 2 ", Limits(new()
        {
            MaxTokens = 3
        }));
        Reject("1+2", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxTokens = 2
        }));
        Compile("1+2", Limits(new()
        {
            MaxAstNodes = 3
        }));
        Reject("1+2", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxAstNodes = 2
        }));
        Compile("a", Limits(new()
        {
            MaxAstNodes = 3,
            MaxDepth = 2
        }));
        Reject("a", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxAstNodes = 2
        }));
        Reject("a", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxDepth = 1
        }));
        Compile("/", Limits(new()
        {
            MaxAstNodes = 1,
            MaxDepth = 1
        }));
        Compile("//a", Limits(new()
        {
            MaxAstNodes = 5
        }));
        Reject("//a", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxAstNodes = 4
        }));
        Compile("(1)", Limits(new()
        {
            MaxDepth = 2
        }));
        Reject("(1)", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxDepth = 1
        }));
        Compile("-1", Limits(new()
        {
            MaxDepth = 2
        }));
        Reject("-1", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxDepth = 1
        }));
        Compile("a[1]", Limits(new()
        {
            MaxDepth = 4,
            MaxAstNodes = 4
        }));
        Reject("a[1]", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxDepth = 3
        }));
        Compile("(1)[2]", Limits(new()
        {
            MaxDepth = 3,
            MaxAstNodes = 3
        }));
        Reject("(1)[2]", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxDepth = 2
        }));
        Compile("string(1)", Limits(new()
        {
            MaxDepth = 2,
            MaxAstNodes = 2
        }));
        Reject("string(1)", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxDepth = 1
        }));
        var options = new XPathCompileOptions { Namespaces = new Dictionary<string, string> { { "p", "urn:p" } }, Functions = [new(new("urn:p", "f"), 0, 0)], Limits = new() { MaxTokens = 5, MaxExpressionCharacters = 17 } };
        Compile("p:f()", options);
        Reject("p:f()", "XPATH-COMPILE-LIMIT", options with
        {
            Limits = new()
            {
                MaxTokens = 4
            }
        });
        Reject("p:f()", "XPATH-COMPILE-LIMIT", options with
        {
            Limits = new()
            {
                MaxExpressionCharacters = 16
            }
        });
        // Implicit xml is a built-in, not a caller entry; explicit xml consumes one entry plus its UTF-16 data.
        Compile("1", Limits(new()
        {
            MaxTokens = 1,
            MaxExpressionCharacters = 1
        }));
        var explicitXml = new XPathCompileOptions { Namespaces = new Dictionary<string, string> { { "xml", TreeValidation.XmlUri } }, Limits = new() { MaxTokens = 2, MaxExpressionCharacters = TreeValidation.XmlUri.Length + 4 } };
        Compile("1", explicitXml);
        Reject("1", "XPATH-COMPILE-LIMIT", explicitXml with
        {
            Limits = new()
            {
                MaxTokens = 1
            }
        });
        var error = Reject("a[1]", "XPATH-COMPILE-LIMIT", Limits(new()
        {
            MaxTokens = 2
        }));
        Assert.Equal(new XPathSourceSpan(2, 1), error.Diagnostic.Span);
    }
    [Theory]
    [InlineData(".", 1, 3, 2)]
    [InlineData("..", 1, 3, 2)]
    [InlineData("/", 1, 1, 1)]
    [InlineData("/a", 2, 3, 2)]
    [InlineData("//a", 2, 5, 2)]
    [InlineData("a//b", 3, 7, 2)]
    [InlineData("@a", 2, 3, 2)]
    [InlineData("$v", 2, 1, 1)]
    [InlineData("true()", 3, 1, 1)]
    [InlineData("concat('a','b')", 6, 3, 2)]
    [InlineData("(1)[2]", 6, 3, 3)]
    [InlineData("a[1]", 4, 4, 4)]
    [InlineData("a[b]", 4, 6, 5)]
    [InlineData("a[b[c]]", 7, 9, 8)]
    [InlineData("node()", 3, 3, 2)]
    [InlineData("processing-instruction('go')", 4, 3, 2)]
    [InlineData("1+2*3", 5, 5, 1)]
    [InlineData("--1", 3, 3, 3)]
    [InlineData("(a)[1]/b", 8, 8, 3)]
    public void EveryVariantCountsAtExactTokenAstAndDepthLimits(string source, int tokens, int nodes, int depth)
    {
        Compile(source, new()
        {
            Limits = new()
            {
                MaxTokens = tokens,
                MaxAstNodes = nodes,
                MaxDepth = depth
            }
        });
        if (tokens > 1)
        {
            Reject(source, "XPATH-COMPILE-LIMIT", new()
            {
                Limits = new()
                {
                    MaxTokens = tokens - 1
                }
            });
        }

        if (nodes > 1)
        {
            Reject(source, "XPATH-COMPILE-LIMIT", new()
            {
                Limits = new()
                {
                    MaxAstNodes = nodes - 1
                }
            });
        }

        if (depth > 1)
        {
            Reject(source, "XPATH-COMPILE-LIMIT", new()
            {
                Limits = new()
                {
                    MaxDepth = depth - 1
                }
            });
        }
    }
    [Fact]
    public async Task SharedCompilerAndContextDoNotShareMutableOperationState()
    {
        IXPathCompiler compiler = new XPathCompiler();
        var context = XPathStaticContext.FromOptions(null, TestContext.Current.CancellationToken);
        var outputs = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => compiler.Compile("//book[@id='b1']/title", context, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        foreach (var output in outputs)
        {
            var path = Assert.IsType<XPathPath>(output);
            Assert.Equal(3, path.Steps.Count);
            Assert.Throws<NotSupportedException>(() => ((IList<XPathStep>)path.Steps).Clear());
            Assert.Single(path.Steps[1].Predicates);
        }
        for (var i = 1; i < outputs.Length; i++)
        {
            Assert.NotSame(outputs[0], outputs[i]);
        }
    }
    [Fact]
    public void ExplicitStacksHandleUnlimitedDeepAndWideExpressions()
    {
        var options = new XPathCompileOptions { Limits = new() { MaxExpressionCharacters = null, MaxTokens = null, MaxAstNodes = null, MaxDepth = null } };
        Assert.IsType<XPathLiteral>(Compile(new string('(', 10000) + "1" + new string(')', 10000), options));
        Assert.IsType<XPathUnaryMinus>(Compile(new string('-', 10000) + "1", options));
        Assert.IsType<XPathBinary>(Compile(string.Join("+", Enumerable.Repeat("1", 10000)), options));
        Assert.Equal(10000, Assert.IsType<XPathPath>(Compile(string.Join("/", Enumerable.Repeat("a", 10000)), options)).Steps.Count);
    }
    [Fact]
    public void CancellationAndNullArgumentsAreNotLanguageDiagnostics()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var compiler = new XPathCompiler();
        var context = XPathStaticContext.FromOptions(null, TestContext.Current.CancellationToken);
        var error = Assert.Throws<OperationCanceledException>(() => compiler.Compile("1", context, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(null!, context, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => compiler.Compile("1", null!, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void LexicalPrimitiveAndVocabularyHaveIndependentOracle()
    {
        var source = " 根😀:名 42. .5 'x' \"y\" / // . .. :: @ $ ( ) [ ] , * + - = != < <= > >= | ";
        XPathTokenKind[] kinds = [XPathTokenKind.Name, XPathTokenKind.Number, XPathTokenKind.Number, XPathTokenKind.Literal, XPathTokenKind.Literal, XPathTokenKind.Slash, XPathTokenKind.DoubleSlash, XPathTokenKind.Dot, XPathTokenKind.Parent, XPathTokenKind.Axis, XPathTokenKind.At, XPathTokenKind.Dollar, XPathTokenKind.Open, XPathTokenKind.Close, XPathTokenKind.Bracket, XPathTokenKind.EndBracket, XPathTokenKind.Comma, XPathTokenKind.Star, XPathTokenKind.Plus, XPathTokenKind.Minus, XPathTokenKind.Equal, XPathTokenKind.NotEqual, XPathTokenKind.Less, XPathTokenKind.LessEqual, XPathTokenKind.Greater, XPathTokenKind.GreaterEqual, XPathTokenKind.Union];
        var lexer = new XPathLexer(source, default, null, 0);
        var last = 0;
        foreach (var kind in kinds)
        {
            var token = lexer.Next();
            Assert.Equal(kind, token.Kind);
            Assert.True(token.Start >= last);
            Assert.True(token.Length > 0);
            Assert.Equal(source.AsSpan(token.Start, token.Length).ToString(), lexer.Text(token).ToString());
            last = token.Start + token.Length;
        }
        Assert.Equal(new XPathToken(XPathTokenKind.End, source.Length, 0), lexer.Next());
        foreach (var text in new[] { "Child", "childx", "child-", "", "根", "ancestor-or-selfx", new string('x', 1000) })
        {
            Assert.Null(XPathCompiler.Axis(text));
        }

        foreach (var text in new[] { "Node", "nodeX", "", "processing-instructionx", "根" })
        {
            Assert.Null(XPathCompiler.NodeKind(text));
        }
    }
}

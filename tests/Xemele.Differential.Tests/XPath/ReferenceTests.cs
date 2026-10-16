using System.Runtime.InteropServices;
using Xemele.XPath;
using Xunit;
using BclDocument = System.Xml.XPath.XPathDocument;
using BclNavigator = System.Xml.XPath.XPathNavigator;
using BclNodeType = System.Xml.XPath.XPathNodeType;
namespace Xemele.Differential.Tests.XPath;

public sealed class ReferenceTests
{
    private const string Xml = "<r xmlns='urn:d' xmlns:p='urn:p' xmlns:q='urn:q' z='Z' a='A' p:b='PB' xml:lang='en-US'>A<![CDATA[B]]>C<n xmlns='' k='2'>2<i>3</i></n><!--note--><?go data?><n xmlns='' k='0'>0</n><p:y p:a='Y'>word</p:y></r>";
    private static readonly Dictionary<string, string> Bindings = new() { ["d"] = "urn:d", ["p"] = "urn:p", ["q"] = "urn:q" };
    public static IEnumerable<object[]> Cases => new[]
    {
        "/", "/d:r", "/d:r/node()", "/d:r/text()", "/d:r/comment()", "/d:r/processing-instruction('go')", "/d:r/n", "/d:r/p:*", "/d:r/*",
        "/d:r/n[1]/ancestor::node()", "/d:r/n[1]/ancestor::node()[1]", "(/d:r/n[1]/ancestor::node())[1]",
        "/d:r/n[2]/preceding-sibling::node()", "/d:r/n[2]/preceding-sibling::node()[1]", "(/d:r/n[2]/preceding-sibling::node())[1]",
        "/d:r/p:y/preceding::node()", "/d:r/p:y/preceding::node()[1]", "(/d:r/p:y/preceding::node())[1]",
        "/d:r/n[1]/following::node()", "/d:r/n[1]/following-sibling::node()", "/d:r/n[1]/descendant-or-self::node()",
        "/d:r/n[1]/i/ancestor-or-self::*", "/d:r/n[1]/i/ancestor-or-self::*[1]", "/d:r/n[1]/parent::*", "/d:r/self::*",
        "/d:r/n/@k", "/d:r/n[1]/@k/parent::*", "/d:r/n[1]/@k/following::node()", "/d:r/n[1]/@k/preceding::node()", "/d:r/n[1]/@k/child::node()",
        "/d:r/namespace::p/parent::*", "/d:r/namespace::p/following::node()", "/d:r/namespace::p/preceding::node()", "/d:r/namespace::p/child::node()",
        "string(/d:r/text())", "string(/d:r)", "name(/d:r/p:y/@p:a)", "namespace-uri(/d:r/p:y/@p:a)", "local-name(/d:r/p:y/@p:a)",
        "count(/d:r/text())", "count(/d:r/n[1]/namespace::*)", "string(/d:r/n[1]/namespace::p)", "name(/d:r/n[1]/namespace::p)",
        "/d:r/n[2] | /d:r/n[1] | /d:r/n[1]", "/d:r/n[position()=last()]", "(/d:r/n)[1]/i", "sum(/d:r/n/@k)", "lang('en')", "normalize-space(' A  B ')",
        "/d:r/n/@k != /d:r/n/@k", "/d:r/n/@k = /d:r/n/@k", "/d:r/n/@k < 1", "0 div 0 != 0 div 0", "1 div 0", "round(-0.5)"
    }.Select(expression => new object[] { expression });
    [Theory, MemberData(nameof(Cases))]
    public void CoreExpressionsMatchPinnedReferenceMetadataAndOrder(string expression) => Compare(Xml, expression);

    public static IEnumerable<object[]> BooleanComparisons()
    {
        foreach (var operation in new[] { "=", "!=", "<", "<=", ">", ">=" })
        {
            foreach (var nodes in new[] { "/r/missing", "/r/zero", "/r/one", "/r/word" })
            {
                foreach (var flag in new[] { "true()", "false()" })
                {
                    foreach (var reversed in new[] { false, true })
                    {
                        yield return [reversed ? flag + " " + operation + " " + nodes : nodes + " " + operation + " " + flag];
                    }
                }
            }
        }
    }
    [Theory, MemberData(nameof(BooleanComparisons))]
    public void NodeSetBooleanAllSixOperatorsBothOrientations(string expression) => Compare("<r><zero>0</zero><one>1</one><word>word</word></r>", expression);

    [Theory]
    [InlineData("/d:r/@*", false)]
    [InlineData("/d:r/namespace::*", true)]
    [InlineData("/d:r/n[1]/namespace::*", true)]
    public void UnspecifiedBclAttributeNamespaceOrderHasExactMembershipAndFrozenProductOrder(string expression, bool namespaces)
    {
        var actual = Evaluate(Xml, expression).AsNodeSet();
        var reference = Reference(Xml, expression);
        var rows = ReadNodes((System.Xml.XPath.XPathNodeIterator)reference);
        // XPath permits implementation-dependent namespace/attribute ordering.
        // Compare membership by complete identity+metadata, then independently enforce the frozen product order.
        Assert.Equal(rows.OrderBy(r => r, StringComparer.Ordinal), actual.Select(Row).OrderBy(r => r, StringComparer.Ordinal));
        if (namespaces)
        {
            Assert.Equal(actual.Select(i => i.LocalName).Order(StringComparer.Ordinal), actual.Select(i => i.LocalName));
        }
        else
        {
            Assert.Equal(actual.Select(i => (i.NamespaceUri, i.LocalName)).OrderBy(i => i.NamespaceUri, StringComparer.Ordinal).ThenBy(i => i.LocalName, StringComparer.Ordinal), actual.Select(i => (i.NamespaceUri, i.LocalName)));
        }

        if (expression.Contains("n[1]", StringComparison.Ordinal))
        {
            Assert.Equal(["p", "q", "xml"], actual.Select(i => i.LocalName));
        }
        else if (namespaces)
        {
            Assert.Equal(["", "p", "q", "xml"], actual.Select(i => i.LocalName));
        }
    }
    [Fact]
    public void SupplementaryCharacterReferenceDeviationIsDisclosedAgainstScalarContract()
    {
        // System.Xml 10.0.11 counts UTF-16 units; frozen XPath scalar semantics count Unicode characters.
        Assert.Equal(3, Evaluate("<r/>", "string-length('A😀B')").AsNumber());
        Assert.Equal(4d, (double)Reference("<r/>", "string-length('A😀B')"));
        Assert.Equal("😀", Evaluate("<r/>", "substring('A😀B',2,1)").AsString());
        Assert.Equal("\ud83d", (string)Reference("<r/>", "substring('A😀B',2,1)"));
        Assert.Equal("AXXB", Evaluate("<r/>", "translate('A😀😀B','😀','X')").AsString());
        Assert.Equal("AXXB", (string)Reference("<r/>", "translate('A😀😀B','😀','X')"));
    }
    [Fact]
    public void AttributeNamespaceAndLogicalTextEntryContextsMatchReference()
    {
        var document = Parse(Xml);
        var root = document.RootElement;
        var attributes = root.Attributes;
        XPathItem[] items = [XPathItem.FromAttribute(attributes[0]), XPathItem.FromNamespace(root, "p"), XPathItem.FromNode(root.FirstChild.NextSibling)];
        string[] addresses = ["/d:r/@z", "/d:r/namespace::p", "/d:r/text()"];
        string[] expressions = [".", "..", "ancestor::node()", "following::node()", "preceding::node()", "string(.)", "name(.)", "local-name(.)", "namespace-uri(.)"];
        for (var i = 0; i < items.Length; i++)
        {
            foreach (var expression in expressions)
            {
                var navigator = CreateReference(Xml);
                navigator = navigator.SelectSingleNode(addresses[i], Resolver())!;
                var compiled = navigator.Compile(expression);
                compiled.SetContext(Resolver());
                AssertResult(navigator.Evaluate(compiled), XPathExpression.Compile(expression, new()
                {
                    Namespaces = Bindings
                }, TestContext.Current.CancellationToken).Evaluate(items[i]));
            }
        }
    }
    private static void Compare(string xml, string expression)
    {
        Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);
        AssertResult(Reference(xml, expression), Evaluate(xml, expression));
    }
    private static XmlDocument Parse(string xml) => XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
    private static XPathValue Evaluate(string xml, string expression) => XPathExpression.Compile(expression, new() { Namespaces = Bindings }, TestContext.Current.CancellationToken).Evaluate(Parse(xml).RootElement, new() { CancellationToken = TestContext.Current.CancellationToken });
    private static BclNavigator CreateReference(string xml)
    {
        using var reader = System.Xml.XmlReader.Create(new StringReader(xml), new()
        {
            DtdProcessing = System.Xml.DtdProcessing.Parse,
            XmlResolver = null
        });
        var navigator = new BclDocument(reader).CreateNavigator();
        navigator.MoveToChild(BclNodeType.Element);
        return navigator;
    }
    private static System.Xml.XmlNamespaceManager Resolver()
    {
        var resolver = new System.Xml.XmlNamespaceManager(new System.Xml.NameTable());
        foreach (var binding in Bindings)
        {
            resolver.AddNamespace(binding.Key, binding.Value);
        }

        return resolver;
    }
    private static object Reference(string xml, string expression)
    {
        var navigator = CreateReference(xml);
        var compiled = navigator.Compile(expression);
        compiled.SetContext(Resolver());
        return navigator.Evaluate(compiled);
    }
    private static void AssertResult(object expected, XPathValue actual)
    {
        switch (expected)
        {
            case bool value:
                Assert.Equal(XPathValueKind.Boolean, actual.Kind);
                Assert.Equal(value, actual.AsBoolean());
                break;
            case double value:
                Assert.Equal(XPathValueKind.Number, actual.Kind);
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(actual.AsNumber()));
                break;
            case string value:
                Assert.Equal(XPathValueKind.String, actual.Kind);
                Assert.Equal(value, actual.AsString());
                break;
            case System.Xml.XPath.XPathNodeIterator nodes:
                Assert.Equal(XPathValueKind.NodeSet, actual.Kind);
                Assert.Equal(ReadNodes(nodes), actual.AsNodeSet().Select(Row));
                break;
            default:
                throw new InvalidOperationException();
        }
    }
    private static List<string> ReadNodes(System.Xml.XPath.XPathNodeIterator iterator)
    {
        var result = new List<string>();
        while (iterator.MoveNext())
        {
            var node = iterator.Current!;
            var kind = node.NodeType switch
            {
                BclNodeType.Root => "Root",
                BclNodeType.Element => "Element",
                BclNodeType.Attribute => "Attribute",
                BclNodeType.Namespace => "Namespace",
                BclNodeType.Text or BclNodeType.Whitespace or BclNodeType.SignificantWhitespace => "Text",
                BclNodeType.Comment => "Comment",
                BclNodeType.ProcessingInstruction => "ProcessingInstruction",
                _ => throw new InvalidOperationException()
            };
            result.Add(string.Join("|", ReferenceAddress(node), kind, node.Name, node.LocalName, node.NamespaceURI, node.Prefix, node.Value));
        }
        return result;
    }
    private static string Row(XPathItem item) => string.Join("|", ProductAddress(item), item.Kind, item.Name, item.LocalName, item.NamespaceUri, item.Prefix, item.StringValue);
    private static string ProductAddress(XPathItem item)
    {
        if (item.Kind == XPathItemKind.Attribute)
        {
            return NodeAddress(item.OwnerElement!.Value) + "/@{" + item.NamespaceUri + "}" + item.LocalName;
        }

        if (item.Kind == XPathItemKind.Namespace)
        {
            return NodeAddress(item.OwnerElement!.Value) + "/namespace::" + item.LocalName;
        }

        return NodeAddress(item.Node!.Value);
    }
    private static string NodeAddress(XmlNode node)
    {
        if (node.Kind == XmlNodeKind.Document)
        {
            return "/";
        }

        var label = node.Kind switch
        {
            XmlNodeKind.Element => "{" + node.NamespaceUri + "}" + node.LocalName,
            XmlNodeKind.Text or XmlNodeKind.CData => "text()",
            XmlNodeKind.Comment => "comment()",
            XmlNodeKind.ProcessingInstruction => "processing-instruction()",
            _ => throw new InvalidOperationException()
        };
        var index = 1;
        for (var previous = node.PreviousSibling; !previous.IsDefault; previous = previous.PreviousSibling)
        {
            if (previous.Kind == node.Kind && (node.Kind != XmlNodeKind.Element || (previous.NamespaceUri == node.NamespaceUri && previous.LocalName == node.LocalName)))
            {
                index++;
            }
        }

        var parent = NodeAddress(node.Parent);
        return (parent == "/" ? "" : parent) + "/" + label + "[" + index + "]";
    }
    private static string ReferenceAddress(BclNavigator source)
    {
        var node = source.Clone();
        if (node.NodeType == BclNodeType.Root)
        {
            return "/";
        }

        if (node.NodeType is BclNodeType.Attribute or BclNodeType.Namespace)
        {
            var suffix = node.NodeType == BclNodeType.Attribute ? "/@{" + node.NamespaceURI + "}" + node.LocalName : "/namespace::" + node.LocalName;
            node.MoveToParent();
            return ReferenceAddress(node) + suffix;
        }
        var label = node.NodeType switch
        {
            BclNodeType.Element => "{" + node.NamespaceURI + "}" + node.LocalName,
            BclNodeType.Text or BclNodeType.Whitespace or BclNodeType.SignificantWhitespace => "text()",
            BclNodeType.Comment => "comment()",
            BclNodeType.ProcessingInstruction => "processing-instruction()",
            _ => throw new InvalidOperationException()
        };
        var index = 1;
        var previous = node.Clone();
        while (previous.MoveToPrevious())
        {
            if (previous.NodeType == node.NodeType && (node.NodeType != BclNodeType.Element || (previous.NamespaceURI == node.NamespaceURI && previous.LocalName == node.LocalName)))
            {
                index++;
            }
        }

        node.MoveToParent();
        var parent = ReferenceAddress(node);
        return (parent == "/" ? "" : parent) + "/" + label + "[" + index + "]";
    }
}

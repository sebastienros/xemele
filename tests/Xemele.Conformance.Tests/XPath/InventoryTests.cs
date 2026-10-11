using System.Text.Json;
using System.Runtime.InteropServices;
using Xemele.XPath;
using Xunit;
namespace Xemele.Conformance.Tests.XPath;

public sealed class InventoryTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs/tasks/M4-corpus.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root absent.");
    }
    public static IEnumerable<object[]> Vectors()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tests/corpora/xpath/inventory.json")));
        foreach (string group in new[] { "axes", "operators", "functions", "cases" })
            foreach (var vector in inventory.RootElement.GetProperty(group).EnumerateArray())
                yield return [vector.GetProperty("id").GetString()!, vector.GetRawText()];
    }
    [Fact]
    public void FrozenInventoryIsUnmodifiedAndComplete()
    {
        Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);
        var root = Root();
        Assert.Equal(File.ReadAllBytes(Path.Combine(root, "tests/Contracts/Fixtures/M4/inventory.json")), File.ReadAllBytes(Path.Combine(root, "tests/corpora/xpath/inventory.json")));
        Assert.Equal(105, Vectors().Count());
        Assert.Equal(105, Vectors().Select(v => (string)v[0]).Distinct(StringComparer.Ordinal).Count());
    }
    [Theory, MemberData(nameof(Vectors))]
    public void FrozenVectorExecutesProductionFacade(string acceptanceId, string json)
    {
        Assert.StartsWith("M4-contracts-A2-", acceptanceId);
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tests/corpora/xpath/inventory.json")));
        using var vectorDocument = JsonDocument.Parse(json);
        var vector = vectorDocument.RootElement;
        string source = vector.GetProperty("expression").GetString()!;
        string documentKey = vector.TryGetProperty("document", out var documentProperty) ? documentProperty.GetString()! : "main";
        var document = XmlDocument.Parse(inventory.RootElement.GetProperty("documents").GetProperty(documentKey).GetString()!, new() { DtdProcessing = XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        // Context addresses are fixed by the freeze, selected directly from tree handles.
        XmlNode context = documentKey == "ids" ? document.RootElement : Children(document.RootElement).Where(n => n.Kind == XmlNodeKind.Element && n.LocalName == "x").ElementAt(1);
        var expected = vector.GetProperty("expected");
        var options = new XPathCompileOptions { Namespaces = new Dictionary<string, string> { ["p"] = "urn:p" } };
        var variables = new Dictionary<XPathExpandedName, XPathValue>();
        if (vector.TryGetProperty("variables", out var bindings))
            foreach (var binding in bindings.EnumerateArray()) variables.Add(new(binding.GetProperty("namespace_uri").GetString()!, binding.GetProperty("local_name").GetString()!), XPathValue.FromNumber(binding.GetProperty("value").GetDouble()));
        try
        {
            var expression = XPathExpression.Compile(source, options, TestContext.Current.CancellationToken);
            var result = expression.Evaluate(context, new() { Variables = variables, CancellationToken = TestContext.Current.CancellationToken });
            if (expected.ValueKind == JsonValueKind.String && expected.GetString()!.StartsWith("XPATH-", StringComparison.Ordinal)) Assert.Fail("Expected diagnostic " + expected.GetString());
            Check(expected, result);
            if (vector.TryGetProperty("proximity_order", out var order))
            {
                int position = 0;
                foreach (var address in order.EnumerateArray())
                {
                    var nearest = XPathExpression.Compile(source + "[" + (++position) + "]", options, TestContext.Current.CancellationToken).Evaluate(context);
                    Assert.Equal(address.GetString(), Address(Assert.Single(nearest.AsNodeSet())));
                }
            }
        }
        catch (XPathCompileException error) { Assert.Equal(expected.GetString(), error.Diagnostic.Code); }
        catch (XPathEvaluationException error) { Assert.Equal(expected.GetString(), error.Diagnostic.Code); }
    }
    private static void Check(JsonElement expected, XPathValue actual)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Array:
                Assert.Equal(XPathValueKind.NodeSet, actual.Kind);
                Assert.Equal(expected.EnumerateArray().Select(v => v.GetString()), actual.AsNodeSet().Select(Address)); break;
            case JsonValueKind.Number: Assert.Equal(XPathValueKind.Number, actual.Kind); Assert.Equal(expected.GetDouble(), actual.AsNumber()); break;
            case JsonValueKind.True: case JsonValueKind.False: Assert.Equal(XPathValueKind.Boolean, actual.Kind); Assert.Equal(expected.GetBoolean(), actual.AsBoolean()); break;
            case JsonValueKind.String:
                string text = expected.GetString()!;
                if (text == "NaN") { Assert.Equal(XPathValueKind.Number, actual.Kind); Assert.True(double.IsNaN(actual.AsNumber())); }
                else if (text == "Infinity") { Assert.Equal(XPathValueKind.Number, actual.Kind); Assert.Equal(double.PositiveInfinity, actual.AsNumber()); }
                else if (text == "-0") { Assert.Equal(XPathValueKind.Number, actual.Kind); Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(actual.AsNumber())); }
                else { Assert.Equal(XPathValueKind.String, actual.Kind); Assert.Equal(text, actual.AsString()); }
                break;
            default: throw new InvalidOperationException();
        }
    }
    private static string Address(XPathItem item) => item.Kind switch
    {
        XPathItemKind.Attribute => NodeAddress(item.OwnerElement!.Value) + "/@" + item.Name,
        XPathItemKind.Namespace => NodeAddress(item.OwnerElement!.Value) + "/namespace::" + item.LocalName,
        _ => NodeAddress(item.Node!.Value)
    };
    private static IEnumerable<XmlNode> Children(XmlNode node)
    { for (var child = node.FirstChild; !child.IsDefault; child = child.NextSibling) yield return child; }
    private static string Name(XmlNode node) => node.Prefix.Length == 0 ? node.LocalName : node.Prefix + ":" + node.LocalName;
    private static string NodeAddress(XmlNode node)
    {
        if (node.Kind == XmlNodeKind.Document) return "/";
        var peers = Children(node.Parent).Where(n => n.Kind == node.Kind && (node.Kind != XmlNodeKind.Element || Name(n) == Name(node))).ToArray();
        string name = node.Kind switch { XmlNodeKind.Element => Name(node), XmlNodeKind.Text or XmlNodeKind.CData => "text()", XmlNodeKind.Comment => "comment()", XmlNodeKind.ProcessingInstruction => "processing-instruction()", _ => throw new InvalidOperationException() };
        string suffix = node.Kind == XmlNodeKind.Element && peers.Length == 1 ? "" : "[" + (Array.IndexOf(peers, node) + 1) + "]";
        string parent = NodeAddress(node.Parent);
        return (parent == "/" ? "" : parent) + "/" + name + suffix;
    }
}

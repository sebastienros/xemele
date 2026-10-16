using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;
using Xemele.Css;
using Xemele.Html;
using Xunit;

namespace Xemele.Differential.Tests.HtmlCss;

public sealed class NativeSelectorTests
{
    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "Xemele.slnx")))
            {
                return d.FullName;
            }
        }

        throw new InvalidOperationException();
    }
    private static JsonDocument Read(string path) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), path)));
    public static IEnumerable<object[]> Cases()
    {
        using var input = Read("tests/corpora/css/native-input.json");
        foreach (var vector in input.RootElement.GetProperty("vectors").EnumerateArray())
        {
            yield return [vector.Clone()];
        }
    }
    [Fact]
    public void IndependentNativeOutputAndEveryMismatchKeepExactProvenance()
    {
        using var provenance = Read("tests/corpora/css/native-selector-provenance.json");
        Assert.Equal("555055f1ea6bf9944488fd96415b6f03b5b43696", provenance.RootElement.GetProperty("commit").GetString());
        foreach (var file in provenance.RootElement.GetProperty("files").EnumerateObject())
        {
            Assert.Equal(file.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root(), file.Name)))));
        }

        using var ledger = Read("tests/corpora/css/native-dispositions.json");
        var rows = ledger.RootElement.EnumerateArray().ToArray();
        Assert.Equal(35, rows.Length);
        Assert.Equal(35, rows.Select(r => r.GetProperty("css").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(23, rows.Count(r => r.GetProperty("classification").GetString() == "exact-overlap"));
        Assert.Equal(4, rows.Count(r => r.GetProperty("classification").GetString() == "scope-root-difference"));
        Assert.Equal(8, rows.Count(r => r.GetProperty("classification").GetString() == "native-unsupported"));
    }
    [Theory, MemberData(nameof(Cases))]
    public void NativeOverlapOrNamedDifferenceStillRequiresTheFullProductResult(JsonElement vector)
    {
        using var input = Read("tests/corpora/css/native-input.json");
        using var output = Read("tests/corpora/css/native-output.json");
        using var dispositions = Read("tests/corpora/css/native-dispositions.json");
        using var bcl = Read("tests/corpora/css/bcl-overlap.json");
        var css = vector.GetProperty("css").GetString()!;
        var xml = input.RootElement.GetProperty("xml").GetString()!;
        var expected = vector.GetProperty("expected").EnumerateArray().Select(v => v.GetString()).ToArray();
        var pairing = Assert.Single(bcl.RootElement.GetProperty("vectors").EnumerateArray(), r => r.GetProperty("css").GetString() == css);
        Assert.Equal(expected, XElement.Parse(xml, LoadOptions.PreserveWhitespace).XPathSelectElements(pairing.GetProperty("xpath").GetString()!).Select(n => n.Attribute("id")!.Value));
        var selector = CssSelector.Compile(css);
        var x = Xemele.XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
        var h = Html5Parser.Parse("<body>" + xml);
        var root = Assert.Single(CssSelector.Compile("root").Select(h.DocumentNode));
        Assert.Equal(expected, selector.Select(x.RootElement).Select(n => n.GetAttribute("id")));
        Assert.Equal(expected, selector.Select(root).Select(n => n.GetAttribute("id")));
        var native = Assert.Single(output.RootElement.EnumerateArray(), r => r.GetProperty("css").GetString() == css);
        var ledger = Assert.Single(dispositions.RootElement.EnumerateArray(), r => r.GetProperty("css").GetString() == css);
        Assert.False(string.IsNullOrWhiteSpace(ledger.GetProperty("reason").GetString()));
        switch (ledger.GetProperty("classification").GetString())
        {
            case "exact-overlap":
                Assert.Equal(expected, native.GetProperty("ids").EnumerateArray().Select(v => v.GetString()));
                break;
            case "scope-root-difference":
                Assert.Contains("r", expected);
                Assert.Equal(expected.Where(v => v != "r"), native.GetProperty("ids").EnumerateArray().Select(v => v.GetString()));
                break;
            case "native-unsupported":
                Assert.StartsWith("CSS selector error", native.GetProperty("error").GetString());
                break;
            default:
                Assert.Fail("Unclassified native result.");
                break;
        }
    }
}

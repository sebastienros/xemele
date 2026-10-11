using System.Security.Cryptography;
using System.Text.Json;
using Xemele.Html;
using Xunit;

namespace Xemele.Conformance.Tests.Html;

public sealed class InventoryTests
{
    internal static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "docs/tasks/M7-corpus.json"))) return d.FullName;
        throw new InvalidOperationException("Repository root absent.");
    }
    private static JsonDocument Read(string path) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), path)));

    [Fact]
    public void IntegratedOriginalsLicensesLedgersAndReportsRetainTheirExactBytes()
    {
        using var inventory = Read("tests/corpora/html/inventory.json");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in inventory.RootElement.GetProperty("files").EnumerateArray())
        {
            string path = item.GetProperty("path").GetString()!;
            Assert.True(paths.Add(path), "Duplicate inventory path " + path);
            byte[] bytes = File.ReadAllBytes(Path.Combine(Root(), path));
            Assert.Equal(item.GetProperty("bytes").GetInt64(), bytes.LongLength);
            Assert.Equal(item.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        Assert.Equal(240, paths.Count);
        Assert.Contains("tests/fixtures/M7-tokenizer/LICENSE", paths);
        Assert.Contains("tests/fixtures/M7-builder/WPT-LICENSE.md", paths);
        Assert.Contains("tests/fixtures/M7-serialize/html5lib/LICENSE", paths);
        Assert.Contains("docs/reports/M7-legacy/UPSTREAM-Copyright.txt", paths);
    }

    [Fact]
    public void FullTokenizerTreeAndSerializerCountsHaveNoSilentFiltering()
    {
        string folder = Path.Combine(Root(), "tests/fixtures/M7-tokenizer/tokenizer");
        int originals = 0, states = 0, coercion = 0;
        foreach (string path in Directory.GetFiles(folder, "*.test"))
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            if (json.RootElement.TryGetProperty("tests", out var cases))
                foreach (var item in cases.EnumerateArray())
                { originals++; states += item.TryGetProperty("initialStates", out var s) ? s.GetArrayLength() : 1; }
            if (json.RootElement.TryGetProperty("xmlViolationTests", out var controls)) coercion += controls.GetArrayLength();
        }
        Assert.Equal(6823, originals); Assert.Equal(7049, states); Assert.Equal(4, coercion);
        using var wpt = Read("tests/fixtures/M7-builder/resource-dispositions.json");
        Assert.True(wpt.RootElement.GetProperty("complete").GetBoolean());
        var resources = wpt.RootElement.GetProperty("resources").EnumerateArray().ToArray();
        Assert.Equal(166, resources.Length);
        Assert.Equal(166, resources.Select(r => r.GetProperty("path").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var resource in resources)
        {
            Assert.False(string.IsNullOrWhiteSpace(resource.GetProperty("evidence").GetString()));
            string path = "tests/fixtures/M7-builder/wpt-parsing/" + resource.GetProperty("path").GetString();
            Assert.Equal(resource.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root(), path)))));
            if (resource.GetProperty("disposition").GetString() == "browser_execution_dependency")
                Assert.False(string.IsNullOrWhiteSpace(resource.GetProperty("dependency_witness").GetString()));
        }
        using var ledger = Read("tests/fixtures/M7-serialize/html5lib-dispositions.json");
        var records = ledger.RootElement.EnumerateArray().ToArray();
        Assert.Equal(230, records.Length);
        Assert.Equal(10, records.Count(r => r.GetProperty("classification").GetString() == "exact-original-overlap"));
        Assert.Equal(69, records.Count(r => r.GetProperty("classification").GetString() == "normative-DOM-witness"));
        Assert.Equal(151, records.Count(r => r.GetProperty("classification").GetString() == "unrepresentable-token-control"));
        int serializerOriginals = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(Root(), "tests/fixtures/M7-serialize/html5lib"), "*.test"))
        {
            using var source = JsonDocument.Parse(File.ReadAllBytes(file));
            var cases = source.RootElement.GetProperty("tests"); serializerOriginals += cases.GetArrayLength();
            for (int i = 0; i < cases.GetArrayLength(); i++)
            {
                var record = Assert.Single(records, r => r.GetProperty("source").GetString() == Path.GetFileName(file) && r.GetProperty("index").GetInt32() == i);
                Assert.True(JsonElement.DeepEquals(cases[i], record.GetProperty("original")));
                if (record.GetProperty("classification").GetString() != "exact-original-overlap")
                    Assert.NotEmpty(record.GetProperty("reasons").EnumerateArray());
            }
        }
        Assert.Equal(230, serializerOriginals);
        using var native = Read("tests/fixtures/M7-serialize/native.json"); Assert.Equal(640, native.RootElement.GetArrayLength());
        using var legacy = Read("tests/fixtures/M7-legacy/oracle.json"); Assert.Equal(539, legacy.RootElement.GetArrayLength());
    }

    [Fact]
    public void ExactOriginalWptNoscriptTemplateAssertionNowPassesAcrossTheComposition()
    {
        var doc = Html5Parser.Parse("<template id=\"template1\"><noscript><div></div></noscript></template>", new() { ScriptingEnabled = true });
        var template = doc.RootElement.Children[0].FirstChild!;
        var noscript = template.TemplateContent!.FirstChild!;
        Assert.Equal("noscript", noscript.LocalName);
        Assert.Equal("<div></div>", noscript.GetTextContent());
        Assert.Empty(Xemele.Css.CssSelector.Compile("div").Select(noscript));
        Assert.Equal("&lt;div&gt;&lt;/div&gt;", string.Concat(noscript.Children.Select(n => HtmlSerializer.Serialize(n))));
        Assert.Equal("<noscript>&lt;div&gt;&lt;/div&gt;</noscript>", HtmlSerializer.Serialize(noscript));
        Assert.Contains("&lt;div&gt;&lt;/div&gt;", File.ReadAllText(Path.Combine(Root(), "tests/fixtures/M7-serialize/noscript-template-querySelector.html")), StringComparison.Ordinal);
    }
}

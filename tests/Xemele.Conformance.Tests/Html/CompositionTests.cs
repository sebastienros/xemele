using System.Text;
using System.Text.Json;
using Xemele.Html;
using Xunit;

namespace Xemele.Conformance.Tests.Html;

public sealed class CompositionTests
{
    public static IEnumerable<object[]> Regressions()
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(InventoryTests.Root(), "tests/corpora/html/regressions.json")));
        foreach (var item in json.RootElement.EnumerateArray())
        {
            yield return [item.GetProperty("id").GetString()!, item.GetProperty("input").GetString()!, item.GetProperty("expectedText").GetString()!];
        }
    }
    [Theory, MemberData(nameof(Regressions))]
    public void NamedParserSerializerSelectorAndImportRegressions(string id, string input, string expectedText)
    {
        var doc = Html5Parser.Parse(input);
        Assert.Equal(expectedText, doc.RootElement.GetTextContent());
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(input)];
        Assert.Equal(HtmlSerializer.Serialize(doc), HtmlSerializer.Serialize(Html5Parser.Parse(bytes)));
        using var stream = new MemoryStream(bytes);
        var loaded = Html5Parser.Load(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(stream.CanRead);
        Assert.Equal(HtmlSerializer.Serialize(doc), HtmlSerializer.Serialize(loaded));
        using var output = new MemoryStream();
        HtmlSerializer.Save(loaded, output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(HtmlSerializer.Serialize(doc)), output.ToArray());
        Assert.True(output.CanWrite);
        var target = new HtmlDocument();
        var imported = target.Import(doc.RootElement);
        Assert.Same(target, imported.OwnerDocument);
        Assert.Null(imported.Parent);
        Assert.Equal(HtmlSerializer.Serialize(doc.RootElement), HtmlSerializer.Serialize(imported));
        if (id == "frameset-no-body")
        {
            Assert.Empty(Xemele.Css.CssSelector.Compile("body").Select(doc.DocumentNode));
        }

        if (id == "template-text-excluded")
        {
            Assert.Single(Xemele.Css.CssSelector.Compile("template").Select(doc.DocumentNode));
        }
    }
}

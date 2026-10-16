using System.Text.Json;
using System.Text.Json.Nodes;
using Xemele.Html;
using Xunit;
namespace Xemele.Tests.LegacyHtml;

public sealed class LegacyTests
{
    private static string Fixture(string name = "oracle.json")
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "tests/fixtures/M7-legacy/oracle.json")))
        {
            path = Directory.GetParent(path)?.FullName ?? throw new InvalidOperationException();
        }

        return Path.Combine(path, "tests/fixtures/M7-legacy", name);
    }
    public static IEnumerable<object[]> Cases()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Fixture()));
        foreach (var item in json.RootElement.EnumerateArray())
        {
            yield return [item.Clone()];
        }
    }
    [Theory, MemberData(nameof(Cases))]
    public void PinnedLibxml2Trees(JsonElement item)
    {
        var id = item.GetProperty("id").GetString()!;
        var bytes = Convert.FromBase64String(item.GetProperty("bytes").GetString()!);
        var expected = Expected(item);
        Assert.True(expected == Tree(HtmlParser.Parse(bytes)), id + " bytes\nexpected " + expected + "\nactual " + Tree(HtmlParser.Parse(bytes)));
        if (item.TryGetProperty("text", out var text))
        {
            Assert.True(expected == Tree(HtmlParser.Parse(text.GetString()!)), id + " characters\nexpected " + expected + "\nactual " + Tree(HtmlParser.Parse(text.GetString()!)));
        }

        foreach (var chunk in new[] { 1, 3, 17, 1024 })
        {
            using var stream = new ChunkStream(bytes, chunk);
            Assert.Equal(expected, Tree(HtmlParser.Load(stream, cancellationToken: TestContext.Current.CancellationToken)));
            Assert.True(stream.CanRead);
        }
    }
    private static string Expected(JsonElement item)
    {
        using var adaptations = JsonDocument.Parse(File.ReadAllText(Fixture("adaptations.json")));
        var native = item.GetProperty("tree");
        if (adaptations.RootElement.TryGetProperty(item.GetProperty("id").GetString()!, out var adapted))
        {
            native = adapted.GetProperty("managedTree");
        }

        return Normalize(JsonNode.Parse(native.GetRawText())!.AsArray()).ToJsonString();
    }
    [Fact]
    public void EveryRawNativeShortReadControlHasManagedCoalescedCoverage()
    {
        using var native = JsonDocument.Parse(File.ReadAllText(Fixture("native-io.json")));
        using var fixtures = JsonDocument.Parse(File.ReadAllText(Fixture()));
        int differences = 0, cases = 0;
        foreach (var control in native.RootElement.EnumerateArray())
        {
            var item = fixtures.RootElement.EnumerateArray().Single(v => v.GetProperty("id").GetString() == control.GetProperty("id").GetString());
            var expected = Expected(item);
            var bytes = Convert.FromBase64String(item.GetProperty("bytes").GetString()!);
            Assert.Equal(expected, Normalize(JsonNode.Parse(control.GetProperty("streams").GetProperty("4096").GetRawText())!.AsArray()).ToJsonString());
            foreach (var raw in control.GetProperty("streams").EnumerateObject())
            {
                cases++;
                if (raw.Value.GetRawText() != control.GetProperty("memory").GetRawText())
                {
                    differences++;
                }

                using var stream = new ChunkStream(bytes, int.Parse(raw.Name));
                Assert.Equal(expected, Tree(HtmlParser.Load(stream, cancellationToken: TestContext.Current.CancellationToken)));
            }
        }
        Assert.True(cases > 0);
        Assert.True(differences > 0);
    }
    private static JsonArray Normalize(JsonArray nodes)
    {
        var result = new JsonArray();
        foreach (var n in nodes)
        {
            var copy = n!.DeepClone().AsObject();
            copy["children"] = Normalize(copy["children"]!.AsArray());
            if (copy["kind"]!.GetValue<string>() == "Text" && result.Count > 0 && result[^1]!["kind"]!.GetValue<string>() == "Text")
            {
                result[^1]!["value"] = result[^1]!["value"]!.GetValue<string>() + copy["value"]!.GetValue<string>();
            }
            else
            {
                result.Add(copy);
            }
        }
        return result;
    }
    internal static string Tree(HtmlDocument document) => Normalize([.. document.DocumentNode.Children.Select(Node).ToArray()]).ToJsonString();
    private static JsonNode Node(HtmlNode n)
    {
        var o = new JsonObject { ["kind"] = n.Kind.ToString(), ["name"] = n.LocalName, ["value"] = n.Value };
        if (n.Doctype is { } d)
        {
            o["public"] = d.PublicIdentifier;
            o["system"] = d.SystemIdentifier;
        }
        o["attributes"] = new JsonArray(n.Attributes.Select(a => (JsonNode)new JsonArray(a.LocalName, a.Value)).ToArray());
        o["children"] = new JsonArray(n.Children.Select(Node).ToArray());
        return o;
    }
    [Fact]
    public void QuotasValidateBeforeTransportAndLeaveOpen()
    {
        using var stream = new ChunkStream("<p>hello"u8.ToArray(), 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => HtmlParser.Load(stream, new() { Limits = new() { Depth = 0 } }, TestContext.Current.CancellationToken));
        Assert.Equal(0, stream.Position);
        Assert.Throws<XmlParseException>(() => HtmlParser.Parse("<p>hello", new() { Limits = new() { DocumentCharacters = 7 } }));
        Assert.Equal("hello", HtmlParser.Parse("<p>hello", new()
        {
            Limits = new()
            {
                DocumentCharacters = 8
            }
        }).RootElement.GetTextContent());
        Assert.Throws<XmlParseException>(() => HtmlParser.Parse("<p a=b c=d>", new() { Limits = new() { AttributesPerElement = 1 } }));
        Assert.Throws<XmlParseException>(() => HtmlParser.Parse("<p>", new() { Limits = new() { Depth = 2 } }));
        Assert.Throws<XmlParseException>(() => HtmlParser.Parse("<p>&amp;&amp;", new() { Limits = new() { ExpandedCharacters = 1 } }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => HtmlParser.Load(stream, cancellationToken: canceled.Token));
        Assert.True(stream.CanRead);
    }
    [Fact]
    public void ProgressRejectsBeforeEofAndLocationsAreScalarBased()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("<p>" + new string('x', 10000));
        using var stream = new ChunkStream(bytes, 13);
        var error = Assert.Throws<XmlParseException>(() => HtmlParser.Load(stream, new() { Limits = new() { DocumentCharacters = 1100 } }, TestContext.Current.CancellationToken));
        Assert.True(stream.Position < bytes.Length);
        Assert.True(stream.Position <= 5120);
        Assert.Equal(1100, error.Diagnostics[0].Location.ByteOffset);
        Assert.True(stream.CanRead);
        var e = Assert.Throws<XmlParseException>(() => HtmlParser.Parse("<p>😀\r\nx", new() { Limits = new() { DocumentCharacters = 6 } }));
        Assert.Null(e.Diagnostics[0].Location.ByteOffset);
        Assert.Equal(2, e.Diagnostics[0].Location.LineNumber);
        Assert.Equal(1, e.Diagnostics[0].Location.LinePosition);
    }
    [Fact]
    public void ExternalReferencesAreInertAndXmlIsIsolated()
    {
        var d = HtmlParser.Parse("<!DOCTYPE html SYSTEM 'https://invalid.test/no-fetch'><P>&zzexternal;</P>");
        Assert.Equal("&zzexternal;", d.RootElement.GetTextContent());
        var xml = new XmlDocument();
        Assert.Throws<ArgumentException>(() => xml.CreateElement("bad name"));
    }
    private sealed class ChunkStream(byte[] bytes, int chunk) : MemoryStream(bytes)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, chunk)]); public override int Read(byte[] b, int offset, int count) => base.Read(b, offset, Math.Min(count, chunk));
    }
}

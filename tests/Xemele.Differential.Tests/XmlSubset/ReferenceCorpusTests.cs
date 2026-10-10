using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele.TestUtilities.References;
using Xunit;

namespace Xemele.Differential.Tests.XmlSubset;

public sealed class ReferenceCorpusTests
{
    private static string Root
    {
        get
        {
            var path = new DirectoryInfo(AppContext.BaseDirectory);
            while (path is not null && !File.Exists(Path.Combine(path.FullName, "global.json"))) path = path.Parent;
            return path?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    public static IEnumerable<object[]> Rejections()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests/corpora/xml/inventory.json")));
        return inventory.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("m1").GetProperty("outcome").GetString() == "rejected")
            .Select(c => new object[] { c.GetProperty("id").GetString()!, c.GetProperty("inputPath").GetString()!, c.GetProperty("inputSha256").GetString()! }).ToArray();
    }
    private static async Task<JsonDocument> Reference(string id, byte[] bytes)
    {
        string request = JsonSerializer.Serialize(new { protocolVersion = 1, caseId = id, operation = "xml.events", inputBase64 = Convert.ToBase64String(bytes), profile = "xml-1.0" });
        return JsonDocument.Parse(await ReferenceProcess.ExecuteAsync(Environment.GetEnvironmentVariable("XEMELE_DOTNET") ?? "dotnet",
            [Environment.GetEnvironmentVariable("XEMELE_BCL_RUNNER") ?? Path.Combine(Root, "tools/ReferenceRunners/Bcl/bin", ReferenceProcess.BuildConfiguration(AppContext.BaseDirectory), "net10.0/BclReference.dll")],
            request, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }
    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task OriginalSupportedBytesAreRejectedByIndependentReferenceAndProduct(string id, string relative, string hash)
    {
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        Assert.StartsWith(root + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
        for (FileSystemInfo? item = new FileInfo(path); item is not null && item.FullName != root; item = item is FileInfo f ? f.Directory : ((DirectoryInfo)item).Parent) Assert.Null(item.LinkTarget);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var reference = await Reference(id, bytes);
        // runner-failure is an infrastructure failure, never a parser rejection/pass.
        Assert.Equal("parse-rejected", reference.RootElement.GetProperty("outcome").GetString());
        Assert.False(XmlDocument.TryParse(bytes, out var document, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken), id);
        Assert.Null(document); Assert.Single(diagnostics);
    }
    [Theory]
    [InlineData("<r/>")]
    [InlineData("<p:r xmlns:p='urn:p' z='2' a='1'>a\r\nb<![CDATA[c]]>&amp;<p:e/></p:r>")]
    [InlineData("<r a='&#x9;&#xA;&#xD;'>&#x1F600;&#xD;</r>")]
    [InlineData("<r xmlns='urn:d'><e/><plain xmlns=''/></r>")]
    [InlineData("<xmlns xmlns:p='urn:p' p:xmlns='legal'><p:xmlns/></xmlns>")]
    public async Task AcceptedSemanticEventsMatchReferenceBeforeAndAfterSerialization(string xml)
    {
        using var reference = await Reference("authored-events", Encoding.UTF8.GetBytes(xml));
        Assert.Equal("success", reference.RootElement.GetProperty("outcome").GetString());
        var product = XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
        string expected = reference.RootElement.GetProperty("events").GetRawText();
        Assert.Equal(expected, Events(product));
        using var output = new MemoryStream();
        product.Save(output, cancellationToken: TestContext.Current.CancellationToken);
        using var roundTrip = await Reference("authored-output", output.ToArray());
        Assert.Equal("success", roundTrip.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(expected, roundTrip.RootElement.GetProperty("events").GetRawText());
    }
    private static string Events(XmlDocument document)
    {
        var events = new List<Dictionary<string, object>>();
        var stack = new Stack<(XmlNode Node, bool End)>();
        stack.Push((document.RootElement, false));
        while (stack.TryPop(out var item))
        {
            var node = item.Node;
            if (node.Kind == XmlNodeKind.Element)
            {
                var row = new Dictionary<string, object> { ["kind"] = item.End ? "end" : "start", ["namespaceUri"] = node.NamespaceUri, ["localName"] = node.LocalName };
                if (!item.End)
                {
                    var attributes = new List<Dictionary<string, string>>();
                    foreach (var a in node.Attributes) attributes.Add(new() { ["namespaceUri"] = a.NamespaceUri, ["localName"] = a.LocalName, ["value"] = a.Value });
                    row["attributes"] = attributes.OrderBy(a => a["namespaceUri"], StringComparer.Ordinal).ThenBy(a => a["localName"], StringComparer.Ordinal).ToArray();
                    stack.Push((node, true));
                    for (var child = node.LastChild; !child.IsDefault; child = child.PreviousSibling) stack.Push((child, false));
                }
                events.Add(row);
            }
            else if (node.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
            {
                if (events.Count > 0 && (string)events[^1]["kind"] == "text") events[^1]["value"] = (string)events[^1]["value"] + node.Value;
                else events.Add(new() { ["kind"] = "text", ["value"] = node.Value });
            }
        }
        return JsonSerializer.Serialize(events);
    }
}

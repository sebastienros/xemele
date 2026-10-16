using System.Text;
using System.Text.Json;
using Xemele.TestUtilities.References;
using Xunit;

namespace Xemele.Differential.Tests.Protocol;

public sealed class M2OfflineSourceTests
{
    private static string Root
    {
        get
        {
            var path = new DirectoryInfo(AppContext.BaseDirectory);
            while (path is not null && !File.Exists(Path.Combine(path.FullName, "global.json")))
            {
                path = path.Parent;
            }

            return path?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    private static async Task<JsonDocument> Run(string xml, object? options, Dictionary<string, string>? resources = null)
    {
        var request = JsonSerializer.Serialize(new
        {
            protocolVersion = 1,
            caseId = "m2-source",
            operation = "xml.events",
            inputBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)),
            profile = "xml-1.0",
            options,
            offlineResources = resources
        });
        return JsonDocument.Parse(await ReferenceProcess.ExecuteAsync(Environment.GetEnvironmentVariable("XEMELE_DOTNET") ?? "dotnet",
            [Environment.GetEnvironmentVariable("XEMELE_BCL_RUNNER") ?? Path.Combine(Root, "tools/ReferenceRunners/Bcl/bin", ReferenceProcess.BuildConfiguration(AppContext.BaseDirectory), "net10.0/BclReference.dll")],
            request, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("https://offline.invalid/suite/main.xml")]
    [InlineData("file:///offline/suite/main.xml")]
    public async Task ExplicitSourceResolvesNestedRelativeResourcesOnlyFromMap(string source)
    {
        var origin = new Uri(source);
        var dtd = new Uri(origin, "dtd/root.dtd");
        var nested = new Uri(dtd, "../entities/value.ent");
        var resources = new Dictionary<string, string>
        {
            [dtd.AbsoluteUri] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<!ENTITY % p SYSTEM '../entities/value.ent'>%p;")),
            [nested.AbsoluteUri] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<!ENTITY e 'nested'>"))
        };
        const string xml = "<!DOCTYPE r SYSTEM 'dtd/root.dtd'><r>&e;</r>";
        using var allowed = await Run(xml, new
        {
            sourceUri = source
        }, resources);
        Assert.Equal("success", allowed.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("nested", allowed.RootElement.GetProperty("events")[1].GetProperty("value").GetString());
        resources.Remove(nested.AbsoluteUri);
        using var denied = await Run(xml, new
        {
            sourceUri = source
        }, resources);
        Assert.Equal("runner-failure", denied.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("resource-denied", denied.RootElement.GetProperty("diagnostics")[0].GetProperty("category").GetString());
    }
    [Theory]
    [InlineData("relative.xml")]
    [InlineData("")]
    [InlineData("https://offline.invalid/main.xml#part")]
    [InlineData("https://offline.invalid/a b")]
    public async Task MalformedSourceIsProtocolFailure(string source)
    {
        using var result = await Run("<r/>", new
        {
            sourceUri = source
        });
        Assert.Equal("runner-failure", result.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("protocol", result.RootElement.GetProperty("diagnostics")[0].GetProperty("category").GetString());
    }
    [Fact]
    public async Task UnknownAndNonStringOptionsAreProtocolFailures()
    {
        foreach (var option in new object[] { new { unknown = "value" }, new { sourceUri = 7 }, new { sourceUri = (string?)null } })
        {
            using var result = await Run("<r/>", option);
            Assert.Equal("runner-failure", result.RootElement.GetProperty("outcome").GetString());
            Assert.Equal("protocol", result.RootElement.GetProperty("diagnostics")[0].GetProperty("category").GetString());
        }
    }
    [Theory]
    [InlineData("<r/>", "success")]
    [InlineData("<r>", "parse-rejected")]
    public async Task ExplicitSourcePreservesSuccessAndSyntaxRejection(string xml, string outcome)
    {
        using var result = await Run(xml, new
        {
            sourceUri = "https://offline.invalid/main.xml"
        });
        Assert.Equal(outcome, result.RootElement.GetProperty("outcome").GetString());
    }
}

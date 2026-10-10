using System.Text;
using System.Text.Json;
using Xemele.TestUtilities.References;
using Xunit;

namespace Xemele.Differential.Tests.Protocol;

[Trait("Category", "Infrastructure")]
public sealed class ReferenceProtocolTests
{
    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    private static string Fixture => Environment.GetEnvironmentVariable("XEMELE_CHILD_FIXTURE")
        ?? Path.Combine(Root, "docs/reports/M0-references-protocol/child_fixture.py");
    private static string Request(string xml, object? offlineResources = null, string operation = "xml.events", int protocolVersion = 1) =>
        JsonSerializer.Serialize(new { protocolVersion, caseId = "known", operation,
            inputBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)), profile = "xml-1.0", offlineResources });
    private static Task<string> Run(string request) => ReferenceProcess.ExecuteAsync(
        Environment.GetEnvironmentVariable("XEMELE_DOTNET") ?? "dotnet",
        [Environment.GetEnvironmentVariable("XEMELE_BCL_RUNNER") ?? Path.Combine(Root, "tools/ReferenceRunners/Bcl/bin", AppContext.BaseDirectory.Contains("/Release/", StringComparison.Ordinal) ? "Release" : "Debug", "net10.0/BclReference.dll")],
        request, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    [Fact]
    public async Task NormalizesExpandedNamesAttributesTextAndEmptyElements()
    {
        using var result = JsonDocument.Parse(await Run(Request("<p:r xmlns:p='urn:p' z='2' a='1'>a\r\nb<![CDATA[c]]>&amp;<p:e/></p:r>")));
        var root = result.RootElement;
        Assert.Equal("known", root.GetProperty("caseId").GetString());
        Assert.Equal("success", root.GetProperty("outcome").GetString());
        Assert.Contains(".NET 10", root.GetProperty("engine").GetProperty("runtime").GetString()!);
        var events = root.GetProperty("events");
        Assert.Equal(5, events.GetArrayLength());
        Assert.Equal("urn:p", events[0].GetProperty("namespaceUri").GetString());
        Assert.Equal("a", events[0].GetProperty("attributes")[0].GetProperty("localName").GetString());
        Assert.Equal("a\nbc&", events[1].GetProperty("value").GetString());
        Assert.Equal("end", events[3].GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData("<r>", "xml.events", 1, "parse-rejected")]
    [InlineData("<r/>", "unknown", 1, "runner-failure")]
    [InlineData("<r/>", "xml.events", 2, "runner-failure")]
    public async Task CategorizesParseAndProtocolFailures(string xml, string operation, int version, string expected)
    {
        using var result = JsonDocument.Parse(await Run(Request(xml, operation: operation, protocolVersion: version)));
        Assert.Equal(expected, result.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task UsesOnlyOfflineResources()
    {
        const string xml = "<!DOCTYPE r SYSTEM 'https://example.invalid/test.dtd'><r>&hello;</r>";
        using var allowed = JsonDocument.Parse(await Run(Request(xml, new Dictionary<string, string>
        { ["https://example.invalid/test.dtd"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<!ENTITY hello 'offline'>")) })));
        Assert.Equal("success", allowed.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("offline", allowed.RootElement.GetProperty("events")[1].GetProperty("value").GetString());
        using var denied = JsonDocument.Parse(await Run(Request(xml)));
        Assert.Equal("runner-failure", denied.RootElement.GetProperty("outcome").GetString());
        Assert.Contains("denied", denied.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString()!);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("invalid")]
    [InlineData("identity")]
    [InlineData("flood")]
    [InlineData("stderr-flood")]
    [InlineData("sleep")]
    public async Task ActualChildFailuresAreInfrastructureFailures(string mode)
    {
        await Assert.ThrowsAsync<ReferenceFailureException>(() => ReferenceProcess.ExecuteAsync("python3",
            [Fixture, mode], Request("<r/>"), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationPropagatesAfterChildCleanup()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReferenceProcess.ExecuteAsync("python3",
            [Fixture, "sleep"], Request("<r/>"), TimeSpan.FromSeconds(10), cancel.Token));
    }
}

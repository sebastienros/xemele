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
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    private static string Fixture => Environment.GetEnvironmentVariable("XEMELE_CHILD_FIXTURE")
        ?? Path.Combine(Root, "tests/Xemele.Differential.Tests/Protocol/child_fixture.py");
    private static string Request(string xml, object? offlineResources = null, string operation = "xml.events", int protocolVersion = 1) =>
        JsonSerializer.Serialize(new
        {
            protocolVersion,
            caseId = "known",
            operation,
            inputBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)),
            profile = "xml-1.0",
            offlineResources
        });
    private static Task<string> Run(string request) => ReferenceProcess.ExecuteAsync(
        Environment.GetEnvironmentVariable("XEMELE_DOTNET") ?? "dotnet",
        [Environment.GetEnvironmentVariable("XEMELE_BCL_RUNNER") ?? Path.Combine(Root, "tools", "ReferenceRunners", "Bcl", "bin", ReferenceProcess.BuildConfiguration(AppContext.BaseDirectory), "net10.0", "BclReference.dll")],
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
    [InlineData("<r>&MaxCharactersFromEntities;</r>", "xml.events", 1, "parse-rejected")]
    [InlineData("<r>&MaxCharactersInDocument;</r>", "xml.events", 1, "parse-rejected")]
    [InlineData("<r/>", "unknown", 1, "runner-failure")]
    [InlineData("<r/>", "xml.events", 2, "runner-failure")]
    public async Task CategorizesParseAndProtocolFailures(string xml, string operation, int version, string expected)
    {
        using var result = JsonDocument.Parse(await Run(Request(xml, operation: operation, protocolVersion: version)));
        Assert.Equal(expected, result.RootElement.GetProperty("outcome").GetString());
    }

    [Theory]
    [InlineData("https")]
    [InlineData("file")]
    public async Task UsesOnlyOfflineResources(string scheme)
    {
        var uri = scheme == "file" ? new Uri(Path.Combine(Root, "global.json")).AbsoluteUri
            : "https://example.invalid/test.dtd";
        var xml = "<!DOCTYPE r SYSTEM '" + uri + "'><r>&hello;</r>";
        using var allowed = JsonDocument.Parse(await Run(Request(xml, new Dictionary<string, string>
        {
            [uri] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<!ENTITY hello 'offline'>"))
        })));
        Assert.Equal("success", allowed.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("offline", allowed.RootElement.GetProperty("events")[1].GetProperty("value").GetString());
        using var denied = JsonDocument.Parse(await Run(Request(xml)));
        Assert.Equal("runner-failure", denied.RootElement.GetProperty("outcome").GetString());
        Assert.Contains("denied", denied.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString()!);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("invalid")]
    [InlineData("invalid-utf8")]
    [InlineData("invalid-utf8-json-string")]
    [InlineData("extra-line")]
    [InlineData("unterminated")]
    [InlineData("identity")]
    [InlineData("flood")]
    [InlineData("stderr-flood")]
    [InlineData("sleep")]
    [InlineData("version-overflow")]
    [InlineData("version-exponent")]
    [InlineData("diagnostic-number")]
    [InlineData("diagnostic-missing-category")]
    [InlineData("diagnostic-message-number")]
    [InlineData("diagnostic-category-null")]
    [InlineData("event-number")]
    [InlineData("start-missing-names")]
    [InlineData("start-namespace-null")]
    [InlineData("start-name-number")]
    [InlineData("start-attributes-missing")]
    [InlineData("attribute-number")]
    [InlineData("attribute-missing-value")]
    [InlineData("attribute-value-number")]
    [InlineData("text-missing-value")]
    [InlineData("text-value-number")]
    [InlineData("end-missing-namespace")]
    [InlineData("end-attributes")]
    [InlineData("start-value")]
    [InlineData("text-namespace")]
    [InlineData("unknown-kind")]
    [InlineData("failure-events")]
    public async Task ActualChildFailuresAreInfrastructureFailures(string mode)
    {
        var failure = await Assert.ThrowsAsync<ReferenceFailureException>(() => ReferenceProcess.ExecuteAsync("python3",
            [Fixture, mode], Request("<r/>"), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        if (mode == "invalid-utf8-json-string")
        {
            Assert.Contains("Malformed response encoding", failure.Message);
        }
    }

    [Fact]
    public async Task UnknownAdditiveFieldsAndExplicitEmptyValuesRemainValid()
    {
        using var result = JsonDocument.Parse(await ReferenceProcess.ExecuteAsync("python3", [Fixture, "additive-fields"],
            Request("<r/>"), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal("success", result.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("", result.RootElement.GetProperty("events")[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task OtherOperationsRequireExplicitPayloadValidator()
    {
        var request = Request("<r/>", operation: "fixture.custom");
        var failure = await Assert.ThrowsAsync<ReferenceFailureException>(() => ReferenceProcess.ExecuteAsync("python3",
            [Fixture, "custom-payload"], request, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Contains("Unsupported success payload", failure.Message);
        using var result = JsonDocument.Parse(await ReferenceProcess.ExecuteAsync("python3", [Fixture, "custom-payload"],
            request, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken,
            validateSuccessPayload: root => root.TryGetProperty("fixtureValue", out var value)
                && value.ValueKind == JsonValueKind.String && value.GetString() == "checked"));
        Assert.Equal("checked", result.RootElement.GetProperty("fixtureValue").GetString());
        var invalid = await Assert.ThrowsAsync<ReferenceFailureException>(() => ReferenceProcess.ExecuteAsync("python3",
            [Fixture, "custom-invalid"], request, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken,
            validateSuccessPayload: root => root.TryGetProperty("fixtureValue", out var value)
                && value.ValueKind == JsonValueKind.String));
        Assert.Contains("Adapter rejected", invalid.Message);
    }

    [Fact]
    public async Task CancellationPropagatesAfterChildCleanup()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancel.CancelAfter(TimeSpan.FromSeconds(1));
        var pidFile = Path.GetTempFileName();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReferenceProcess.ExecuteAsync("python3",
                [Fixture, "sleep", pidFile], Request("<r/>"), TimeSpan.FromSeconds(10), cancel.Token));
            var pid = int.Parse(await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Throws<ArgumentException>(() => System.Diagnostics.Process.GetProcessById(pid));
        }
        finally { File.Delete(pidFile); }
    }

    [Theory]
    [InlineData("C:\\repo\\bin\\Release\\net10.0\\", "Release")]
    [InlineData("/repo/bin/Release/net10.0/", "Release")]
    [InlineData("C:\\repo\\bin\\Debug\\net10.0\\", "Debug")]
    [InlineData("/repo/bin/Debug/net10.0/", "Debug")]
    public void BuildConfigurationIsPortable(string path, string expected) =>
        Assert.Equal(expected, ReferenceProcess.BuildConfiguration(path));

    [Theory]
    [InlineData(null)]
    [InlineData("utf-8")]
    public async Task MalformedUtf8IsParseRejection(string? hint)
    {
        var request = JsonSerializer.Serialize(new
        {
            protocolVersion = 1,
            caseId = "known",
            operation = "xml.events",
            inputBase64 = Convert.ToBase64String(new byte[] { 60, 114, 62, 0xc3, 0x28, 60, 47, 114, 62 }),
            profile = "xml-1.0",
            encodingHint = hint
        });
        using var result = JsonDocument.Parse(await Run(request));
        Assert.Equal("parse-rejected", result.RootElement.GetProperty("outcome").GetString());
    }

    [Theory]
    [InlineData(262, "success")]
    [InlineData(263, "runner-failure")]
    public async Task EntityQuotaIsInfrastructureLimit(int count, string outcome)
    {
        var xml = "<!DOCTYPE r [<!ENTITY x '" + new string('x', 1000) + "'>]><r>"
            + string.Concat(Enumerable.Repeat("&x;", count)) + "</r>";
        using var result = JsonDocument.Parse(await Run(Request(xml)));
        Assert.Equal(outcome, result.RootElement.GetProperty("outcome").GetString());
        if (outcome == "runner-failure")
        {
            Assert.Equal("limit", result.RootElement.GetProperty("diagnostics")[0].GetProperty("category").GetString());
        }
    }
}

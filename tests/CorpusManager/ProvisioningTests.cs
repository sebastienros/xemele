using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xemele.CorpusInfrastructure;
using Xunit;

namespace Xemele.CorpusInfrastructure.Tests;

[Trait("Category", "Infrastructure")]
public sealed class ProvisioningTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xemele-corpora-" + Guid.NewGuid().ToString("N"));
    private readonly string directory;
    private readonly string manifestPath;
    private readonly string cache;
    private readonly JsonObject manifest;

    public ProvisioningTests()
    {
        directory = Path.Combine(root, "tests", "corpora", "unit");
        Directory.CreateDirectory(directory);
        manifestPath = Path.Combine(directory, "manifest.json");
        cache = Path.Combine(root, "artifacts", "corpora");
        var fixtures = new JsonArray();
        var files = new List<object>();
        foreach (var (name, text, classification) in new[] { ("valid.xml", "<root/>\n", "accepted"), ("invalid.xml", "<root>\n", "rejected") })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(Path.Combine(directory, name), bytes);
            fixtures.Add(new JsonObject { ["path"] = name, ["sha256"] = Digest(bytes), ["classification"] = classification });
            files.Add(new
            {
                path = name,
                base64 = Convert.ToBase64String(bytes)
            });
        }
        var source = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            files
        });
        File.WriteAllBytes(Path.Combine(directory, "source.json"), source);
        manifest = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["id"] = "unit-corpus",
            ["sourceUrl"] = "https://example.com/unit",
            ["revision"] = "sha256:" + Digest(source),
            ["sha256"] = Digest(source),
            ["license"] = "MIT",
            ["notice"] = "Original synthetic test data",
            ["adapterVersion"] = "xemele-offline-bundle-v1",
            ["profile"] = "unit-xml",
            ["sourcePath"] = "source.json",
            ["fixtures"] = fixtures
        };
        Save();
    }

    [Fact]
    public void ValidCacheProvisionsExactBytesAndHonestCountsOffline()
    {
        var corpus = Load();
        var manager = Manager();
        manager.ImportSource(corpus);
        File.Delete(Path.Combine(directory, "source.json"));
        foreach (var fixture in corpus.Fixtures)
            File.Delete(Path.Combine(directory, fixture.Path));
        var report = manager.Provision(corpus, Profiles());
        Assert.True(report.Successful);
        Assert.Equal(2, report.Total);
        Assert.Equal(2, report.Provisioned);
        Assert.Equal(0, report.ParserCasesExecuted);
        Assert.Equal(0, report.StandardsPassed);
        Assert.Equal(corpus.License, report.License);
        Assert.Equal(corpus.Notice, report.Notice);
        Assert.Equal(corpus.Revision, report.Revision);
        Assert.Equal(corpus.Sha256, report.SourceSha256);
        Assert.Equal(new[] { "<root/>\n", "<root>\n" }, report.Cases.Select(c => File.ReadAllText(c.CachePath!)));
        foreach (var item in report.Cases)
            Assert.Equal(corpus.Fixtures.Single(f => f.Path == item.Path).Sha256, Digest(File.ReadAllBytes(item.CachePath!)));
        Assert.True(manager.Provision(corpus, Profiles()).Successful);
    }

    [Fact]
    public void MissingCacheDoesNotUseCheckoutFixturesAsSuccess()
    {
        var report = Manager().Provision(Load(), Profiles());
        Assert.False(report.Successful);
        Assert.Equal(2, report.MissingCache);
        Assert.Equal(0, report.Provisioned);
        Assert.Equal(0, report.ParserCasesExecuted);
    }

    [Fact]
    public void CorruptSourceCacheIsExplicitForEveryEligibleCase()
    {
        var corpus = Load();
        var manager = Manager();
        manager.ImportSource(corpus);
        File.WriteAllText(manager.SourceCachePath(corpus), "corrupt");
        var report = manager.Provision(corpus, Profiles());
        Assert.Equal(2, report.CorruptCache);
        Assert.Equal(0, report.Provisioned);
        Assert.False(report.Successful);
    }

    [Fact]
    public void SourceCacheDirectoryIsAnInfrastructureFailure()
    {
        var corpus = Load();
        Directory.CreateDirectory(Manager().SourceCachePath(corpus));
        var report = Manager().Provision(corpus, Profiles());
        Assert.Equal(2, report.InfrastructureFailure);
        Assert.Equal(0, report.MissingCache);
        Assert.Equal(0, report.Provisioned);
    }

    [Fact]
    public void ValidSourceChecksumWithInvalidBundleIsStillCorruptCache()
    {
        var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"files\":[]}");
        manifest["sha256"] = Digest(payload);
        Save();
        var corpus = Load();
        var path = Manager().SourceCachePath(corpus);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, payload);
        Assert.Equal(2, Manager().Provision(corpus, Profiles()).CorruptCache);
    }

    [Fact]
    public void CorruptCheckoutSourceCannotEnterCache()
    {
        File.WriteAllText(Path.Combine(directory, "source.json"), "corrupt");
        Assert.Throws<InvalidDataException>(() => Manager().ImportSource(Load()));
        Assert.False(File.Exists(Manager().SourceCachePath(Load())));
    }

    [Fact]
    public void SourceHashAloneDoesNotValidateFixtureBytes()
    {
        manifest["fixtures"]![0]!["sha256"] = new string('0', 64);
        Save();
        Assert.Throws<InvalidDataException>(() => Manager().ImportSource(Load()));
    }

    [Fact]
    public void UnsupportedProfileNeverCountsAsProvisioned()
    {
        var report = Manager().Provision(Load(), new HashSet<string> { "other" });
        Assert.Equal(2, report.UnsupportedProfile);
        Assert.Equal(0, report.MissingCache);
        Assert.Equal(0, report.Provisioned);
        Assert.False(report.Successful);
    }

    [Fact]
    public void ExplicitExclusionRetainsReasonAndDoesNotMaskMissingCache()
    {
        manifest["fixtures"]![0]!["exclusion"] = "Explicit synthetic policy exclusion";
        Save();
        var report = Manager().Provision(Load(), Profiles());
        Assert.Equal(1, report.Excluded);
        Assert.Equal(1, report.MissingCache);
        Assert.Equal("Explicit synthetic policy exclusion", report.Cases[0].Detail);
        Assert.Equal(report.Total, report.Excluded + report.MissingCache);
        Assert.False(report.Successful);
    }

    [Fact]
    public void InfrastructureWriteFailureIsNeitherRejectionNorPass()
    {
        var corpus = Load();
        var manager = Manager();
        manager.ImportSource(corpus);
        File.WriteAllText(Path.Combine(cache, "fixtures"), "Directory deliberately blocked by a regular file.");
        var report = manager.Provision(corpus, Profiles());
        Assert.Equal(2, report.InfrastructureFailure);
        Assert.Equal(0, report.Provisioned);
        Assert.False(report.Successful);
    }

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("schemaVersion", "\"1\"")]
    [InlineData("revision", "\"main\"")]
    [InlineData("license", "\"\"")]
    [InlineData("notice", "null")]
    [InlineData("sourceUrl", "\"file:///etc/passwd\"")]
    [InlineData("sha256", "\"UPPER\"")]
    [InlineData("fixtures", "[]")]
    public void InvalidManifestMetadataFails(string field, string json)
    {
        manifest[field] = JsonNode.Parse(json);
        Save();
        Assert.Throws<InvalidDataException>(() => Load());
    }

    [Fact]
    public void DuplicateJsonFieldsFail()
    {
        File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => Load());
    }

    [Fact]
    public void EscapingAndDuplicateFixturePathsFail()
    {
        manifest["fixtures"]![0]!["path"] = "../../../../outside.xml";
        Save();
        Assert.Throws<InvalidDataException>(() => Load());
        manifest["fixtures"]![0]!["path"] = "invalid.xml";
        Save();
        Assert.Throws<InvalidDataException>(() => Load());
    }

    [Fact]
    public void DiscoveryRejectsDuplicateCorpusIds()
    {
        var other = Path.Combine(root, "tests", "corpora", "other");
        Directory.CreateDirectory(other);
        File.Copy(manifestPath, Path.Combine(other, "manifest.json"));
        Assert.Throws<InvalidDataException>(() => CorpusManifest.Discover(root));
    }

    [Fact]
    public void UnknownSourceAdapterFailsExplicitly()
    {
        manifest["adapterVersion"] = "future-adapter";
        Save();
        Assert.Throws<InvalidDataException>(() => Manager().ImportSource(Load()));
    }

    [Fact]
    public void CachedOutputIsReverifiedAndRestoredFromVerifiedSource()
    {
        var corpus = Load();
        var manager = Manager();
        manager.ImportSource(corpus);
        var first = manager.Provision(corpus, Profiles());
        File.WriteAllText(first.Cases[0].CachePath!, "corrupt output");
        var second = manager.Provision(corpus, Profiles());
        Assert.True(second.Successful);
        Assert.Equal("<root/>\n", File.ReadAllText(second.Cases[0].CachePath!));
    }

    [Fact]
    public void SymlinkOutsideRepositoryFailsBeforeReadOrWrite()
    {
        var outside = Path.Combine(Path.GetTempPath(), "xemele-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "escape"), outside);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or PlatformNotSupportedException ||
                (OperatingSystem.IsWindows() && error is IOException && (error.HResult & 0xffff) == 1314))
            {
                Assert.Skip("Symbolic-link creation capability unavailable: " + error.Message);
            }
            Assert.Throws<InvalidDataException>(() => new CorpusProvisioner(root, Path.Combine(root, "escape")).ImportSource(Load()));
            manifest["fixtures"]![0]!["path"] = "../../../escape/fixture.xml";
            Save();
            Assert.Throws<InvalidDataException>(() => Load());
            Assert.Empty(Directory.GetFileSystemEntries(outside));
        }
        finally { Directory.Delete(outside, recursive: true); }
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private void Save() => File.WriteAllText(manifestPath, manifest.ToJsonString());
    private CorpusManifest Load() => CorpusManifest.Load(root, manifestPath);
    private CorpusProvisioner Manager() => new(root, cache);
    private static HashSet<string> Profiles() => new(StringComparer.Ordinal) { "unit-xml" };
    public void Dispose() => Directory.Delete(root, recursive: true);
}

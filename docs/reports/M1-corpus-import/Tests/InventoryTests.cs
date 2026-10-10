using System.Text.Json.Nodes;
using System.Text.Json;
using System.Xml;
using Xemele.CorpusInfrastructure;
using Xunit;

public sealed class InventoryTests
{
    private static readonly string Repository = FindRepository();
    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Xemele.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository absent");
    }

    private sealed class Local : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "xemele-full-inventory-" + Guid.NewGuid().ToString("N"));
        public string Module => Path.Combine(Root, "tests", "corpora", "xml");
        public CorpusManifest Manifest => CorpusManifest.Discover(Root).Single();
        public CorpusProvisioner Manager => new(Root, Path.Combine(Root, "cache"));
        public Local()
        {
            Directory.CreateDirectory(Module);
            foreach (var file in new[] { "manifest.json", "source.bundle.json" })
                File.Copy(Path.Combine(Repository, "tests", "corpora", "xml", file), Path.Combine(Module, file));
        }
        public void Dispose() => Directory.Delete(Root, true);
    }

    [Fact]
    public void AllFilesProvisionOfflineAndMatchChecksums()
    {
        using var local = new Local();
        var manifest = local.Manifest;
        Assert.Equal(3386, manifest.Fixtures.Count);
        local.Manager.ImportSource(manifest);
        File.Delete(Path.Combine(local.Module, "source.bundle.json"));
        var report = local.Manager.Provision(manifest, new HashSet<string> { manifest.Profile });
        Assert.True(report.Successful);
        Assert.Equal(3386, report.Provisioned);
        Assert.Equal(0, report.ParserCasesExecuted);
        Assert.Equal(0, report.StandardsPassed);
        foreach (var fixture in report.Cases)
            CorpusProvisioner.Verify(File.ReadAllBytes(fixture.CachePath!), manifest.Fixtures.Single(f => f.Path == fixture.Path).Sha256);
    }

    [Fact]
    public void MissingCachePreservesEveryInventoryRow()
    {
        using var local = new Local();
        var manifest = local.Manifest;
        var report = local.Manager.Provision(manifest, new HashSet<string> { manifest.Profile });
        Assert.Equal(3386, report.MissingCache);
        Assert.False(report.Successful);
    }

    [Fact]
    public void CorruptCachePreservesEveryInventoryRow()
    {
        using var local = new Local();
        var manifest = local.Manifest;
        local.Manager.ImportSource(manifest);
        File.WriteAllText(local.Manager.SourceCachePath(manifest), "corrupt");
        var report = local.Manager.Provision(manifest, new HashSet<string> { manifest.Profile });
        Assert.Equal(3386, report.CorruptCache);
        Assert.False(report.Successful);
    }

    [Fact]
    public void WrongProfileIsVisibleAndNeverSuccessful()
    {
        using var local = new Local();
        var report = local.Manager.Provision(local.Manifest, new HashSet<string> { "m1-unsupported" });
        Assert.Equal(3386, report.UnsupportedProfile);
        Assert.Equal(0, report.Provisioned);
        Assert.False(report.Successful);
    }

    [Theory]
    [InlineData("sourceUrl", null)]
    [InlineData("license", null)]
    [InlineData("revision", "main")]
    [InlineData("sha256", "bad")]
    public void InvalidProvenanceFailsDiscovery(string field, string? value)
    {
        using var local = new Local();
        var path = Path.Combine(local.Module, "manifest.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (value is null) node.Remove(field); else node[field] = value;
        File.WriteAllText(path, node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CorpusManifest.Discover(local.Root));
    }

    [Fact]
    public void CorruptImportedResourceFailsBeforeCachePublication()
    {
        using var local = new Local();
        var path = Path.Combine(local.Module, "source.bundle.json");
        File.WriteAllText(path, "corrupt");
        Assert.Throws<InvalidDataException>(() => local.Manager.ImportSource(local.Manifest));
        Assert.False(File.Exists(local.Manager.SourceCachePath(local.Manifest)));
    }

    [Fact]
    public void MissingImportFailsExplicitly()
    {
        using var local = new Local();
        File.Delete(Path.Combine(local.Module, "source.bundle.json"));
        Assert.Throws<FileNotFoundException>(() => local.Manager.ImportSource(local.Manifest));
    }

    [Fact]
    public void EscapingResourcePathFailsDiscovery()
    {
        using var local = new Local();
        var path = Path.Combine(local.Module, "manifest.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["fixtures"]![0]!["path"] = "../../../../outside";
        File.WriteAllText(path, node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CorpusManifest.Discover(local.Root));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOrCorruptBundleMemberFailsEvenWithRehashedSource(bool missing)
    {
        using var local = new Local();
        var source = Path.Combine(local.Module, "source.bundle.json");
        var bundle = JsonNode.Parse(File.ReadAllText(source))!;
        if (missing) bundle["files"]!.AsArray().RemoveAt(0);
        else bundle["files"]![0]!["base64"] = Convert.ToBase64String("corrupt resource"u8.ToArray());
        File.WriteAllText(source, bundle.ToJsonString());
        var manifestPath = Path.Combine(local.Module, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source)));
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        Assert.Throws<InvalidDataException>(() => local.Manager.ImportSource(local.Manifest));
        Assert.False(File.Exists(local.Manager.SourceCachePath(local.Manifest)));
    }

    [Fact]
    public void IndependentBclCatalogReaderMatchesAllAuthoritativeIdentities()
    {
        // Only catalog metadata is parsed. No test XML document or product API is executed.
        using var local = new Local();
        using var bundle = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(local.Module, "source.bundle.json")));
        foreach (var file in bundle.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = Path.Combine(local.Module, file.GetProperty("path").GetString()!);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.GetProperty("base64").GetBytesFromBase64());
        }
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Repository, "tests/corpora/xml/inventory.json")));
        var expected = inventory.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!, c => c.GetProperty("attributes").GetProperty("TYPE").GetString());
        var actual = new Dictionary<string, string?>();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() };
        using var reader = XmlReader.Create(Path.Combine(local.Module, "xmlconf/xmlconf.xml"), settings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "TEST") continue;
            var metadataPath = Path.GetRelativePath(local.Module, new Uri(reader.BaseURI).LocalPath).Replace(Path.DirectorySeparatorChar, '/');
            actual.Add(metadataPath + "#" + reader.GetAttribute("ID"), reader.GetAttribute("TYPE"));
        }
        Assert.Equal(2585, actual.Count);
        Assert.Equal(expected.OrderBy(x => x.Key), actual.OrderBy(x => x.Key));
    }

}

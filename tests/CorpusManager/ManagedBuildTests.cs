using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xemele.Build;
using Xunit;

namespace Xemele.CorpusInfrastructure.Tests;

[Trait("Category", "Infrastructure")]
public sealed class ManagedBuildTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xemele-managed-build-" + Guid.NewGuid().ToString("N"));
    private const string Valid = """{"declaration":"public class A","id":"original-a","member":"T:A"}""";

    public ManagedBuildTests() => Directory.CreateDirectory(Path.Combine(root, "docs/api"));

    private void Fragment(string name, string rows) =>
        File.WriteAllText(Path.Combine(root, "docs/api", name + ".txt"), "xemele-api-v1\n" + rows);

    [Fact]
    public void CompiledUnionRetainsInventoryProvenanceWithoutRewritingFragments()
    {
        Fragment("a", Valid + "\n");
        Fragment("join", "");
        var entries = ApiFragments.Read(root);
        var row = Assert.Single(entries);
        Assert.Equal("original-a", row.id);
        Assert.Equal("docs/api/a.txt", row.source);
        Assert.Equal(2, row.line);
        Assert.Equal("xemele-api-v1\n" + Valid.Replace("original-a", "T:A", StringComparison.Ordinal) + "\n", ApiFragments.Compose(entries));
        Assert.Equal("xemele-api-v1\n" + Valid + "\n", File.ReadAllText(Path.Combine(root, "docs/api/a.txt")));
    }

    [Theory]
    [InlineData("""{"id":"a","declaration":"class A","member":"T:A"}""")]
    [InlineData("""{"declaration":"","id":"a","member":"T:A"}""")]
    [InlineData("""{"declaration":"class A","id":"a","member":"invalid"}""")]
    [InlineData("""{"declaration":"class A","id":"a","member":"T:A B"}""")]
    [InlineData("""{"declaration":"class A","id":"a","member":"T:A","unknown":true}""")]
    [InlineData("""{"declaration":"class A","id":"a","id":"b","member":"T:A"}""")]
    public void InvalidApiRowsFailExplicitly(string row)
    {
        Fragment("a", row + "\n");
        Assert.Throws<InvalidDataException>(() => ApiFragments.Read(root));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("member")]
    public void DuplicateApiIdentitiesAcrossFragmentsFail(string field)
    {
        Fragment("a", Valid + "\n");
        var row = JsonNode.Parse(Valid)!;
        row[field == "id" ? "member" : "id"] = "different";
        Fragment("b", row.ToJsonString() + "\n");
        Assert.Throws<InvalidDataException>(() => ApiFragments.Read(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndHeaderOnlyApiAggregatesFail(bool header)
    {
        if (header)
            Fragment("empty", "");
        Assert.Throws<InvalidDataException>(() => ApiFragments.Read(root));
    }

    private string Corpus()
    {
        var directory = Path.Combine(root, "tests/corpora/xml");
        Directory.CreateDirectory(directory);
        foreach (var name in new[] { "manifest.json", "inventory.json" })
            File.Copy(Path.Combine(Xemele.Build.Program.Root, "tests/corpora/xml", name), Path.Combine(directory, name));
        return directory;
    }

    [Fact]
    public async Task CorruptArchiveFailsBeforeExtractionOrNetwork()
    {
        Corpus();
        var cache = Path.Combine(root, "artifacts/test-data");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f.tar.gz"), "corrupt");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CorpusData.PrepareAsync(root));
        Assert.Contains("Cached XML archive SHA-256 mismatch", error.Message);
        Assert.False(Directory.Exists(Path.Combine(root, "tests/corpora/xml/xmlconf")));
    }

    [Fact]
    public async Task CorruptCachedFixtureIsNotSilentlyReplaced()
    {
        var directory = Corpus();
        var inventory = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "inventory.json")))!;
        var relative = inventory["files"]![0]!["path"]!.GetValue<string>();
        var path = Path.Combine(directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "corrupt");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CorpusData.PrepareAsync(root));
        Assert.Contains("Corrupt corpus fixture", error.Message);
        Assert.Equal("corrupt", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("xmlconf/../../outside")]
    [InlineData("xmlconf\\outside")]
    [InlineData("/absolute")]
    public async Task UnsafeArchiveInventoryPathsFailBeforeDownload(string path)
    {
        var directory = Corpus();
        var inventoryPath = Path.Combine(directory, "inventory.json");
        var inventory = JsonNode.Parse(File.ReadAllText(inventoryPath))!;
        inventory["files"]![0]!["path"] = path;
        File.WriteAllText(inventoryPath, inventory.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => CorpusData.PrepareAsync(root));
        Assert.False(Directory.Exists(Path.Combine(root, "artifacts")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("content")]
    public void RegularTarEntriesPreserveEmptyAndNonemptyFixtureBytes(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        using var archive = new MemoryStream();
        using (var writer = new TarWriter(archive, leaveOpen: true))
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "fixture.xml") { DataStream = new MemoryStream(bytes) });
        archive.Position = 0;
        using var reader = new TarReader(archive);
        var entry = reader.GetNextEntry()!;
        Assert.Equal(bytes, CorpusData.ReadFixture(entry, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes))));
        Assert.Throws<InvalidDataException>(() => CorpusData.ReadFixture(entry, bytes.LongLength, new string('0', 64)));
    }

    [Theory]
    [InlineData(TarEntryType.SymbolicLink)]
    [InlineData(TarEntryType.HardLink)]
    public void ArchiveLinksCannotBecomeFixtureBytes(TarEntryType type)
    {
        var entry = new PaxTarEntry(type, "fixture.xml") { LinkName = "outside" };
        Assert.Throws<InvalidDataException>(() => CorpusData.ReadFixture(entry, 0,
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}

using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdImportVisibility.Support;
namespace Xemele.Tests.M8XsdImportVisibility;

public sealed class OriginalTests
{
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Xemele.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "tests/fixtures/M8-xsd-import-visibility");
    }
    [Fact]
    public void ExactOriginalBytesMetadataExpectationsAndEarlierFailuresArePreserved()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "manifest.json")));
        foreach (var item in manifest.RootElement.GetProperty("sha256").EnumerateObject())
        {
            Assert.Equal(item.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root(), item.Name)))));
        }

        Assert.Equal("failure preserved; new result UNRUN", manifest.RootElement.GetProperty("earlier_observation").GetProperty("status").GetString());
        Assert.Equal("compile-valid", manifest.RootElement.GetProperty("earlier_observation").GetProperty("actual").GetString());
        foreach (var group in manifest.RootElement.GetProperty("groups").EnumerateArray())
        {
            var schema = group.GetProperty("schema");
            Assert.Equal("invalid", schema.GetProperty("expected").GetString());
            Assert.Equal("invalid", schema.GetProperty("originalExpected")[0].GetProperty("validity").GetString());
            // Validate the metadata itself rather than accepting only its extracted manifest.
            var metadata = XDocument.Load(Path.Combine(Root(), group.GetProperty("metadata").GetString()!));
            var row = metadata.Descendants().Single(n => n.Name.LocalName == "testGroup" && (string?)n.Attribute("name") == group.GetProperty("name").GetString());
            Assert.Equal("invalid", (string?)row.Descendants().Single(n => n.Name.LocalName == "expected").Attribute("validity"));
        }
    }
    [Theory]
    [InlineData("msData/element/elemZ007.xsd", "elemZ007_a.xsd", "elemZ007_b.xsd")]
    [InlineData("msData/schema/schZ005.xsd", "schZ005a.xsd", "schZ005b.xsd")]
    public void UnchangedOriginalPrincipalIsRejectedAtItsOwnReference(string path, string child, string foreign)
    {
        Uri principal = new(Path.Combine(Root(), path));
        var resolver = Exact(principal, child, foreign);
        using var source = File.OpenRead(principal.LocalPath);
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, new() { BaseUri = principal, Resolver = resolver }));
        var diagnostic = Assert.Single(error.Diagnostics);
        Assert.Equal("XSD.SchemaRestriction", diagnostic.Code);
        Assert.Contains("without an import", diagnostic.Message);
        Assert.Equal(principal, diagnostic.Location.SourceUri);
        Assert.True(diagnostic.Location.LineNumber > 0);
        Assert.NotNull(diagnostic.Location.ByteOffset);
        Assert.True(source.CanRead);
        Assert.Equal(2, resolver.Requests.Count);
        resolver.Disposed();
    }
    [Theory]
    [InlineData("msData/element/elemZ007.xsd", "elemZ007_a.xsd", "elemZ007_b.xsd", false)]
    [InlineData("msData/element/elemZ007.xsd", "elemZ007_a.xsd", "elemZ007_b.xsd", true)]
    [InlineData("msData/schema/schZ005.xsd", "schZ005a.xsd", "schZ005b.xsd", false)]
    [InlineData("msData/schema/schZ005.xsd", "schZ005a.xsd", "schZ005b.xsd", true)]
    public void SeparatePrincipalImportControlCompilesWithoutChangingOriginalFiles(string path, string child, string foreign, bool afterInclude)
    {
        Uri principal = new(Path.Combine(Root(), path));
        var resolver = Exact(principal, child, foreign);
        var original = File.ReadAllText(principal.LocalPath);
        var include = $"<xsd:include schemaLocation=\"{child}\"/>";
        var permission = "<xsd:import namespace=\"b\"/>";
        var derived = original.Replace(include, afterInclude ? include + permission : permission + include, StringComparison.Ordinal);
        Assert.NotEqual(original, derived);
        Assert.NotNull(XmlSchema.Compile(derived, new()
        {
            BaseUri = principal,
            Resolver = resolver
        }));
        Assert.Equal(2, resolver.Requests.Count);
        resolver.Disposed();
    }
    private static Resolver Exact(Uri principal, string child, string foreign)
    {
        Resolver? resolver = null;
        resolver = new((r, _) =>
        {
            var first = resolver!.Requests.Count == 1;
            Assert.Equal(first ? child : foreign, r.SystemId);
            Assert.Equal(new Uri(principal, first ? child : foreign), r.Uri);
            Assert.Equal(first ? principal : new Uri(principal, child), r.Referrer);
            return resolver.Resolved(File.ReadAllBytes(r.Uri.LocalPath), r.Uri);
        });
        return resolver;
    }
}

using System.Text.Json;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdTypeRelations;
public sealed class OriginalTests
{
    [Fact]
    public void OriginalHashesAndMetadataRemainFrozen()
    {
        string root = Root(); using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        foreach (var item in manifest.RootElement.GetProperty("sha256").EnumerateObject())
            Assert.Equal(item.Value.GetString(), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, item.Name)))));
    }
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests/fixtures/M8-xsd-type-relations"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests/fixtures/M8-xsd-type-relations");
    }
    [Theory]
    [InlineData(0, "particlesZ002/particlesZ002")]
    [InlineData(1, "particlesZ002/particlesZ002.v")]
    [InlineData(2, "particlesZ012/particlesZ012")]
    [InlineData(3, "particlesZ012/particlesZ012.v")]
    [InlineData(4, "particlesZ015/particlesZ015")]
    [InlineData(5, "particlesZ015/particlesZ015.i")]
    [InlineData(6, "particlesZ020/particlesZ020")]
    [InlineData(7, "particlesZ031/particlesZ031")]
    [InlineData(8, "attZ015/attZ015")]
    [InlineData(9, "attZ015/attZ015.v")]
    [InlineData(10, "ctE019/ctE019")]
    [InlineData(11, "ctE019/ctE019.v")]
    [InlineData(12, "ctF013/ctF013")]
    [InlineData(13, "ctF013/ctF013.v")]
    [InlineData(14, "ctG001/ctG001")]
    [InlineData(15, "ctG001/ctG001.v")]
    [InlineData(16, "ctG013/ctG013")]
    [InlineData(17, "ctG013/ctG013.v")]
    [InlineData(18, "stZ007/stZ007")]
    [InlineData(19, "stZ007/stZ007.v")]
    [InlineData(20, "stZ047/stZ047")]
    [InlineData(21, "stZ047/stZ047.v")]
    [InlineData(22, "stZ055/stZ055")]
    [InlineData(23, "stZ055/stZ055.v")]
    [InlineData(24, "test009/test")]
    [InlineData(25, "test009/test.13.n")]
    [InlineData(26, "test009/test.8.n")]
    [InlineData(27, "test009/test.1.v")]
    [InlineData(28, "test009/test.10.v")]
    [InlineData(29, "test009/test.11.v")]
    [InlineData(30, "test009/test.12.v")]
    [InlineData(31, "test009/test.2.v")]
    [InlineData(32, "test009/test.3.v")]
    [InlineData(33, "test009/test.4.v")]
    [InlineData(34, "test009/test.5.v")]
    [InlineData(35, "test009/test.6.v")]
    [InlineData(36, "test009/test.7.v")]
    [InlineData(37, "test009/test.9.v")]
    public void UnchangedOriginalMatchesMetadataAndIndependentOracle(int index, string identity)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "manifest.json")));
        var item = manifest.RootElement.GetProperty("cases")[index]; bool expected = item.GetProperty("expected").GetString() == "valid";
        string path = Path.Combine(Root(), item.GetProperty("schemas")[0].GetString()!);
        using var input = File.OpenRead(path);
        var schema = XmlSchema.Compile(input, new() { BaseUri = new Uri(path), Resolver = new Resolver() }); Assert.True(input.CanRead);
        var oracle = new System.Xml.Schema.XmlSchemaSet { XmlResolver = new LocalResolver() };
        foreach (var file in item.GetProperty("schemas").EnumerateArray()) oracle.Add(null, Path.Combine(Root(), file.GetString()!)); oracle.Compile();
        if (item.GetProperty("kind").GetString() == "instance")
        {
            string instance = Path.Combine(Root(), item.GetProperty("instance").GetString()!);
            using var xml = File.OpenRead(instance); var result = schema.Validate(xml);
            Assert.True(result.IsComplete); Assert.Equal(expected, result.IsValid); Assert.True(xml.CanRead);
            Assert.DoesNotContain(result.Diagnostics, d => d.Code.StartsWith("SCHEMA.", StringComparison.Ordinal));
            bool valid = true;
            var settings = new System.Xml.XmlReaderSettings { Schemas = oracle, ValidationType = System.Xml.ValidationType.Schema, XmlResolver = null };
            settings.ValidationEventHandler += (_, _) => valid = false;
            using var reader = System.Xml.XmlReader.Create(instance, settings); while (reader.Read()) { } Assert.Equal(expected, valid);
        }
        Assert.NotEmpty(identity);
    }
    private sealed class Resolver : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!request.Uri.IsFile || !Path.GetFullPath(request.Uri.LocalPath).StartsWith(Root() + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return null;
            return new(File.OpenRead(request.Uri.LocalPath), request.Uri);
        }
    }
    private sealed class LocalResolver : System.Xml.XmlResolver
    {
        public override System.Net.ICredentials? Credentials { set { } }
        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (!absoluteUri.IsFile || !Path.GetFullPath(absoluteUri.LocalPath).StartsWith(Root() + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new System.Xml.XmlException("Denied outside fixtures.");
            return File.OpenRead(absoluteUri.LocalPath);
        }
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdParticleNormalization;

public sealed class OriginalTests
{
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs/tasks/M8-xsd-particle-normalization.json"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests/fixtures/M8-xsd-particle-normalization");
    }
    [Fact]
    public void OriginalSourcesCopyrightAndMetadataRemainFrozen()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "manifest.json")));
        foreach (var item in manifest.RootElement.GetProperty("sha256").EnumerateObject())
            Assert.Equal(item.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root(), item.Name)))));
    }
    [Theory]
    [InlineData(0, "elemZ027_e/elemZ027_e")]
    [InlineData(1, "elemZ027_f/elemZ027_f")]
    [InlineData(2, "particlesZ008/particlesZ008")]
    [InlineData(3, "particlesZ008/particlesZ008.v")]
    [InlineData(4, "addB194/addB194")]
    public void UnchangedOriginalMatchesExplicitMetadata(int index, string identity)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "manifest.json")));
        var item = manifest.RootElement.GetProperty("cases")[index]; Assert.Equal(identity, item.GetProperty("id").GetString());
        Assert.Equal("valid", item.GetProperty("expected").GetString());
        string path = Path.Combine(Root(), item.GetProperty("schemas")[0].GetString()!);
        using var source = File.OpenRead(path);
        XmlSchema schema;
        try { schema = XmlSchema.Compile(source, new() { BaseUri = new Uri(path) }); }
        catch (SchemaCompileException e) { Assert.Fail(string.Join("; ", e.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))); return; }
        Assert.True(source.CanRead);
        if (item.GetProperty("kind").GetString() == "instance")
        {
            using var instance = File.OpenRead(Path.Combine(Root(), item.GetProperty("instance").GetString()!));
            var result = schema.Validate(instance); Assert.True(result.IsComplete); Assert.True(result.IsValid); Assert.True(instance.CanRead);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DerivedControlUsingOriginalAmbiguousGroupRejectsActualContent(bool anonymous)
    {
        string source = File.ReadAllText(Path.Combine(Root(), "msData/additional/addB194.xsd"));
        string reference = "<xs:group ref='THDMVV'/>";
        string content = anonymous ? $"<xs:element name='GroupRoot'><xs:complexType>{reference}</xs:complexType></xs:element>" : $"<xs:complexType name='GroupUser'>{reference}</xs:complexType>";
        source = source.Replace("</xs:schema>", content + "</xs:schema>", StringComparison.Ordinal);
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction" && d.Message.Contains("unique particle attribution", StringComparison.Ordinal));
        Assert.DoesNotContain(error.Diagnostics, d => d.Code == "SCHEMA.Limit");
    }
    [Theory]
    [InlineData("msData/element/elemZ027e.xsd")]
    [InlineData("msData/element/elemZ027f.xsd")]
    [InlineData("msData/particles/particlesZ008.xsd")]
    [InlineData("msData/additional/addB194.xsd")]
    public void IndependentBclAcceptsOriginalSchema(string file)
    {
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        schemas.Add(null, Path.Combine(Root(), file)); schemas.Compile(); Assert.True(schemas.IsCompiled);
    }
}

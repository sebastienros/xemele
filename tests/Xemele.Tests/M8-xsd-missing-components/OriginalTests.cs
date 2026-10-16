using System.Security.Cryptography;
using System.Text.Json;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class OriginalTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Xemele.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "tests/fixtures/M8-xsd-missing-components");
    }
    private static JsonDocument Manifest() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "manifest.json")));
    [Fact]
    public void OriginalBytesAndSeparatedUnchangedOracleBindings()
    {
        using var manifest = Manifest();
        var root = manifest.RootElement;
        foreach (var resource in root.GetProperty("resources").EnumerateArray())
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root(), resource.GetProperty("path").GetString()!));
            Assert.Equal(resource.GetProperty("bytes").GetInt32(), bytes.Length);
            Assert.Equal(resource.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        Assert.Equal(8, root.GetProperty("positiveOriginals").GetArrayLength());
        var separate = root.GetProperty("separateNonassessmentControls");
        Assert.Equal(4, separate.GetArrayLength());
        Assert.Equal(["invalid", "valid", "invalid", "invalid"], separate.EnumerateArray().Select(r => r.GetProperty("record").GetProperty("expected").GetString()));
        Assert.All(separate.EnumerateArray(), row => { Assert.False(row.GetProperty("originalPassCredit").GetBoolean()); Assert.False(row.GetProperty("gateCredit").GetBoolean()); });
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EightOriginalPositiveCasesStayIndividuallyBound(int index)
    {
        using var manifest = Manifest();
        var row = manifest.RootElement.GetProperty("positiveOriginals")[index];
        var record = row.GetProperty("record");
        Assert.Equal("valid", record.GetProperty("expected").GetString());
        var path = Path.Combine(Root(), row.GetProperty("schemaPath").GetString()!);
        using var input = File.OpenRead(path);
        var schema = XmlSchema.Compile(input, new()
        {
            BaseUri = new Uri(path)
        });
        Assert.True(input.CanRead);
        if (record.GetProperty("kind").GetString() == "schema")
        {
            return;
        }

        var instancePath = Path.Combine(Root(), record.GetProperty("documents")[0].GetString()!);
        var instance = File.ReadAllText(instancePath);
        var result = schema.Validate(instance);
        Assert.True(result.IsComplete);
        Assert.True(result.IsValid);
        Assert.True(schema.Validate(XmlDocument.Parse(instance, cancellationToken: TestContext.Current.CancellationToken)).IsValid);
        using var stream = File.OpenRead(instancePath);
        Assert.True(schema.Validate(stream).IsValid);
        Assert.True(stream.CanRead);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FourOriginalBadInstancesAreSeparateNonassessmentControlsWithZeroPassCredit(int index)
    {
        using var manifest = Manifest();
        var row = manifest.RootElement.GetProperty("separateNonassessmentControls")[index];
        var schema = XmlSchema.Compile(File.ReadAllText(Path.Combine(Root(), row.GetProperty("schemaPath").GetString()!)));
        var instance = File.ReadAllText(Path.Combine(Root(), row.GetProperty("record").GetProperty("documents")[0].GetString()!));
        Missing(schema.Validate(instance));
        Missing(schema.Validate(XmlDocument.Parse(instance, cancellationToken: TestContext.Current.CancellationToken)));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(instance));
        Missing(schema.Validate(stream));
        Assert.False(row.GetProperty("originalPassCredit").GetBoolean());
        Assert.False(row.GetProperty("gateCredit").GetBoolean());
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OriginalMissingBaseSchemasRemainCompileInvalid(int index)
    {
        using var manifest = Manifest();
        var row = manifest.RootElement.GetProperty("missingBaseNegativeOriginals")[index].GetProperty("record");
        Assert.Equal("invalid", row.GetProperty("expected").GetString());
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(File.ReadAllText(Path.Combine(Root(), row.GetProperty("documents")[0].GetString()!))));
    }
}

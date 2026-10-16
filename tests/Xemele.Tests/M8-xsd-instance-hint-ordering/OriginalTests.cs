using System.Security.Cryptography;
using System.Text.Json;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdInstanceHintOrdering;

public sealed class OriginalTests
{
    internal static readonly Uri Base = new("https://www.w3.org/XML/2004/xml-schema-test-suite/msData/additional/");
    internal static string Fixture(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-instance-hint-ordering")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests/fixtures/M8-xsd-instance-hint-ordering", file);
    }
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void SevenUnchangedOriginalInvalidExpectations(int number)
    {
        using var provenance = JsonDocument.Parse(File.ReadAllText(Fixture("provenance.json")));
        var file = $"test93490_{number}.xml";
        var bytes = File.ReadAllBytes(Fixture(file));
        var pin = provenance.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("documents").TryGetProperty("msData/additional/" + file, out _));
        Assert.Equal("invalid", pin.GetProperty("original_expected").GetString());
        Assert.Equal(pin.GetProperty("documents").GetProperty("msData/additional/" + file).GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var resolver = new FirstSchemas();
        var directives = "<xs:import namespace='foo' schemaLocation='test93490_1.xsd'/>";
        if (number is 4 or 5 or 7 or 8 or 10)
        {
            directives += "<xs:import namespace='bar' schemaLocation='test93490_3.xsd'/>";
        }

        if (number is 7 or 8 or 10)
        {
            directives += "<xs:include schemaLocation='test93490_5.xsd'/>";
        }

        var schema = XmlSchema.Compile($"<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>{directives}</xs:schema>", new()
        {
            BaseUri = Base,
            Resolver = resolver,
            MaxOperations = 100_000_000,
            CancellationToken = TestContext.Current.CancellationToken
        });
        var acquired = resolver.Requests;
        using var stream = new MemoryStream(bytes);
        var document = XmlDocument.Load(stream, new()
        {
            Resolver = resolver
        }, new Uri(Base, file), TestContext.Current.CancellationToken);
        var revision = document.ContentRevision;
        var result = schema.Validate(document, new()
        {
            MaxOperations = 100_000_000,
            CancellationToken = TestContext.Current.CancellationToken
        });
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Code == "XSD.HintOrder");
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == XmlDiagnosticSeverity.Fatal);
        Assert.All(result.Diagnostics, d => Assert.Equal(new Uri(Base, file), d.Location.SourceUri));
        Assert.Equal(acquired, resolver.Requests);
        Assert.Equal(revision, document.ContentRevision);
        Assert.True(stream.CanRead);
    }
    internal sealed class FirstSchemas : XmlResourceResolver
    {
        internal int Requests;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests++;
            Assert.Equal(XmlResourceKind.Schema, request.Kind);
            var file = Path.GetFileName(request.Uri.AbsolutePath);
            Assert.Contains(file, new[] { "test93490_1.xsd", "test93490_3.xsd", "test93490_5.xsd" });
            var bytes = File.ReadAllBytes(Fixture(file));
            using var provenance = JsonDocument.Parse(File.ReadAllText(Fixture("provenance.json")));
            Assert.Equal(provenance.RootElement.GetProperty("authored_schema_pins").GetProperty("msData/additional/" + file).GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            return new(new MemoryStream(bytes), request.Uri);
        }
    }
}

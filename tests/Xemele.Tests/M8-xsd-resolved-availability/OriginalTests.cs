using System.Security.Cryptography;
using System.Text.Json;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdResolvedAvailability;

public sealed class OriginalTests
{
    internal static string FixtureRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Xemele.slnx"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "tests/fixtures/M8-xsd-resolved-availability");
    }
    private static string Read(string relative) => File.ReadAllText(Path.Combine(FixtureRoot(), relative));
    internal static SchemaCompileOptions Ordinary() => new() { MaxOperations = 100_000_000, MaxComponents = 1_000_000, Limits = new() { DocumentCharacters = 64 * 1024 * 1024, EvaluationWork = 100_000_000 } };
    [Fact]
    public void ExactOriginalBytesAndInvalidOraclesStaySeparateFromBehavior()
    {
        using var manifest = JsonDocument.Parse(Read("manifest.json"));
        foreach (var resource in manifest.RootElement.GetProperty("resources").EnumerateArray())
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(FixtureRoot(), resource.GetProperty("path").GetString()!));
            Assert.Equal(resource.GetProperty("bytes").GetInt32(), bytes.Length);
            Assert.Equal(resource.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        var records = manifest.RootElement.GetProperty("records"); Assert.Equal(5, records.GetArrayLength());
        foreach (var record in records.EnumerateArray())
        {
            string expected = record.GetProperty("name").GetString() == "annotZ002" ? "valid" : "invalid";
            Assert.Equal(expected, record.GetProperty("originalExpected").GetString());
            Assert.Contains("validity=\"" + expected + "\"", Read(record.GetProperty("metadataPath").GetString()!));
            Assert.False(record.GetProperty("originalPassCredit").GetBoolean()); Assert.False(record.GetProperty("gateCredit").GetBoolean());
        }
    }
    [Fact]
    public void UnchangedAnnotZ002CompletesUnderOrdinaryCorpusLimits()
    {
        string path = Path.Combine(FixtureRoot(), "msData/annotations/annotZ002.xsd");
        using var input = File.OpenRead(path);
        var schema = XmlSchema.Compile(input, Ordinary() with { BaseUri = new Uri(path) });
        Assert.NotNull(schema); Assert.True(input.CanRead);
    }
    [Theory]
    [InlineData("sunData/combined/xsd015/xsd015.e.xsd", "foo", "root", Xs, "abc")]
    [InlineData("sunData/combined/xsd016/xsd016.e.xsd", "foo", "root", Xs, "abc")]
    [InlineData("msData/element/elemM002.xsd", "", "myElem", "", "foo")]
    [InlineData("msData/identityConstraint/idB005.xsd", "", "keyElement", "", "keyinfo")]
    public void OriginalInvalidMetadataKeepsConcretePartialBehaviorAndExactUnresolvedQName(string relative, string ns, string element, string typeNs, string type)
    {
        string path = Path.Combine(FixtureRoot(), relative); var source = XmlDocument.Parse(Read(relative), cancellationToken: TestContext.Current.CancellationToken);
        var options = Ordinary() with { BaseUri = new Uri(path), Resolver = new LocalResolver() };
        var context = new SchemaCompileContext(options); var builder = new AvailabilityBuilder(context.Operation);
        var components = new XmlSchemaCompiler().CompileAvailable(source, context, builder); builder.Join(components); var availability = builder.Seal();
        var declaration = Assert.Single(components.Elements.Where(e => e.Name == new SchemaName(ns, element)));
        Assert.Equal(new SchemaName(typeNs, type), declaration.Type);
        Assert.DoesNotContain(components.Types, t => t.Name == new SchemaName(typeNs, type));
        Assert.DoesNotContain(components.SimpleTypes, t => t.Name == new SchemaName(typeNs, type));
        Assert.Equal(new SchemaName(typeNs, type), availability.Missing(ComponentAvailability.Element(declaration), DependencyUse.TypeAssessment, context.Operation, default)!.AuthoredName);
        if (relative.Contains("xsd015", StringComparison.Ordinal)) Assert.Contains(components.Types, t => t.Name == new SchemaName("foo", "abc"));
        IXsdCompiler strict = new XmlSchemaCompiler();
        Assert.Throws<SchemaCompileException>(() => strict.Compile(source, options, new SchemaCompileOperation(options)));
        var schema = XmlSchema.Compile(source, options);
        Missing(schema.Validate(ns.Length == 0 ? "<" + element + "/>" : "<" + element + " xmlns='" + ns + "'/>"));
    }
    [Fact]
    public void CaseCorrectedKeyInfoAcceptsEqualIdsAcrossDocuments()
    {
        string path = Path.Combine(FixtureRoot(), "msData/identityConstraint/idB005.xsd");
        string corrected = Read("msData/identityConstraint/idB005.xsd").Replace("type=\"keyinfo\"", "type=\"keyInfo\"", StringComparison.Ordinal);
        var options = Ordinary() with { BaseUri = new Uri(path), Resolver = new LocalResolver() };
        Assert.Contains("id=\"foo123\"", corrected); Assert.Contains("id=\"foo123\"", Read("msData/identityConstraint/idB005a.xsd"));
        Assert.True(XmlSchema.Compile(corrected, options).Validate("<root><keyElement keyField='value' key2='true'/></root>").IsValid);
        IXsdCompiler strict = new XmlSchemaCompiler();
        Assert.NotNull(strict.Compile(XmlDocument.Parse(corrected, cancellationToken: TestContext.Current.CancellationToken), options, new SchemaCompileOperation(options)));
    }
    [Fact]
    public void DuplicateSchemaIdWithinOneDocumentIsRejected()
    {
        string corrected = Read("msData/identityConstraint/idB005.xsd").Replace("type=\"keyinfo\"", "type=\"keyInfo\"", StringComparison.Ordinal)
            .Replace("<xsd:element name=\"keyElement\"", "<xsd:element id=\"foo123\" name=\"keyElement\"", StringComparison.Ordinal);
        string path = Path.Combine(FixtureRoot(), "msData/identityConstraint/idB005.xsd");
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(corrected, Ordinary() with { BaseUri = new Uri(path), Resolver = new LocalResolver() }));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    private sealed class LocalResolver : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string expected = Path.GetFullPath(Path.Combine(FixtureRoot(), "msData/identityConstraint/idB005a.xsd"));
            if (!request.Uri.IsFile || request.Uri.LocalPath != expected) throw new InvalidOperationException("Unlisted fixture request.");
            return new(File.OpenRead(expected), request.Uri);
        }
    }
}

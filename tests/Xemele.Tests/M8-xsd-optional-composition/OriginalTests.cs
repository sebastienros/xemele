using System.Security.Cryptography;
using System.Text.Json;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdOptionalComposition;

public sealed class OriginalTests
{
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs/tasks/M8-xsd-optional-composition.json"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests/fixtures/M8-xsd-optional-composition");
    }
    [Fact]
    public void OriginalBytesMetadataAndCopyrightRemainFrozen()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "manifest.json")));
        foreach (var item in manifest.RootElement.GetProperty("sha256").EnumerateObject())
            Assert.Equal(item.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root(), item.Name)))));
        var original = manifest.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "anyURI_a004_1339.i");
        Assert.Equal("invalid", original.GetProperty("expected")[0].GetProperty("validity").GetString());
        Assert.Equal("valid", manifest.RootElement.GetProperty("derived_a004").GetProperty("expected").GetString());
    }
    [Theory]
    [InlineData("anyURI_a002_1337", "msData/datatypes/Facets/anyURI/anyURI_a002.xsd", "msData/datatypes/Facets/anyURI/anyURI_a002.xml", 3)]
    [InlineData("anyURI_a004_1339", "msData/datatypes/Facets/anyURI/anyURI_a004.xsd", "msData/datatypes/Facets/anyURI/anyURI_a004.xml", 3)]
    [InlineData("schD7", "msData/schema/schD7_a.xsd", "msData/schema/schD7.xml", 2)]
    [InlineData("schG8", "msData/schema/schG8_a.xsd", "msData/schema/schG8.xml", 2)]
    public void OriginalAcquisitionAndInstanceObservation(string group, string schemaPath, string instancePath, int calls)
    {
        string path = Path.Combine(Root(), schemaPath);
        using var source = File.OpenRead(path);
        var resolver = new ExactResolver(group, new Uri(path));
        if (Environment.GetEnvironmentVariable("OPTIONAL_BASELINE") == "1")
        {
            var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, new() { BaseUri = new Uri(path), Resolver = resolver }));
            Assert.Equal("SCHEMA.ResourceDenied", Assert.Single(error.Diagnostics).Code); Assert.Equal(1, resolver.Calls);
            TestContext.Current.TestOutputHelper!.WriteLine($"{group}: schema incomplete-refusal SCHEMA.ResourceDenied; instance schema-unavailable; original expectation preserved");
        }
        else
        {
            var schema = XmlSchema.Compile(source, new() { BaseUri = new Uri(path), Resolver = resolver });
            using var instance = File.OpenRead(Path.Combine(Root(), instancePath));
            var result = schema.Validate(instance); Assert.True(result.IsComplete); Assert.True(result.IsValid); Assert.True(instance.CanRead);
            Assert.Equal(calls, resolver.Calls); Assert.Equal(group is "schD7" or "schG8" ? 1 : 0, resolver.Disposals);
            TestContext.Current.TestOutputHelper!.WriteLine(group == "anyURI_a004_1339"
                ? "original anyURI_a004_1339.i expected INVALID; observed complete VALID: DISAGREEMENT retained. Separate W3C4126 derived VALID passes."
                : $"{group}: original schema and instance VALID match.");
        }
        Assert.True(source.CanRead);
    }
    [Fact]
    public void SeparatelyDerivedW3c4126ValidUsesUnchangedA004Bytes()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "manifest.json")));
        var original = manifest.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "anyURI_a004_1339.i");
        Assert.Equal("invalid", original.GetProperty("expected")[0].GetProperty("validity").GetString());
        Assert.Equal("https://www.w3.org/Bugs/Public/show_bug.cgi?id=4126", manifest.RootElement.GetProperty("derived_a004").GetProperty("source").GetString());
        string path = Path.Combine(Root(), "msData/datatypes/Facets/anyURI/anyURI_a004.xsd");
        using var source = File.OpenRead(path);
        var resolver = new ExactResolver("anyURI_a004_1339", new Uri(path));
        var schema = XmlSchema.Compile(source, new() { BaseUri = new Uri(path), Resolver = resolver });
        using var instance = File.OpenRead(Path.Combine(Root(), "msData/datatypes/Facets/anyURI/anyURI_a004.xml"));
        var result = schema.Validate(instance); Assert.True(result.IsComplete); Assert.True(result.IsValid); Assert.Equal(3, resolver.Calls);
    }
    internal sealed class ExactResolver(string group, Uri principal) : XmlResourceResolver
    {
        internal int Calls, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("Only explicit outcomes are used.");
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls++;
            Assert.Equal(XmlResourceKind.Schema, request.Kind); Assert.Equal(principal, request.Referrer);
            string missing = group switch { "anyURI_a002_1337" => "名前空間URI", "anyURI_a004_1339" => "http://127.0.0.1/must%20not%20resolve.xyzzy", "schD7" => "not-exist.xsd", _ => "not-exist" };
            if (request.SystemId == missing) { Assert.Equal(new Uri(principal, missing), request.Uri); return XmlResourceResolution.Unavailable; }
            string available = group switch { "schD7" => "schD7_c.xsd", "schG8" => "schG8_c.xsd", _ => throw new InvalidOperationException("Unexpected original reference.") };
            Assert.Equal(available, request.SystemId); Assert.Equal(new Uri(principal, available), request.Uri);
            var stream = new NeighborTests.OwnedStream(File.ReadAllBytes(request.Uri.LocalPath)) { OnDispose = () => Disposals++ };
            return XmlResourceResolution.FromResource(new(stream, request.Uri));
        }
    }
}

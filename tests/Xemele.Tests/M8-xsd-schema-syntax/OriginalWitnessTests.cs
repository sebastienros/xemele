using System.Net;
using System.Text;
using Xemele.Validation;
using Xunit;
using BclSchemaSet = System.Xml.Schema.XmlSchemaSet;
namespace Xemele.Tests.M8XsdSchemaSyntax;

public sealed class OriginalWitnessTests
{
    internal static readonly Uri Base = new("https://syntax.test/");
    internal static string Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-schema-syntax"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "tests/fixtures/M8-xsd-schema-syntax", name);
    }
    internal static Dictionary<Uri, byte[]> Imports() => new[] { "idIimp.xsd", "idIxpns.xsd", "idIxpns1.xsd" }
        .ToDictionary(n => new Uri(Base, n), n => File.ReadAllBytes(Fixture(n)));
    [Theory]
    [InlineData("annotF001.xsd", "XSD.SchemaRestriction")]
    [InlineData("groupD001.xsd", "XSD.SchemaRestriction")]
    [InlineData("idI150.xsd", "XSD.Identity")]
    public void OriginalOfficialInvalidSchemaRejectsThroughPublicFacade(string file, string code)
    {
        byte[] original = File.ReadAllBytes(Fixture(file));
        var map = Imports();
        var bcl = new BclSchemaSet { XmlResolver = new BclResolver(map) };
        using var reader = System.Xml.XmlReader.Create(new MemoryStream(original), new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit }, new Uri(Base, file).AbsoluteUri);
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => { bcl.Add(null, reader); bcl.Compile(); });
        var resolver = new Resolver(map);
        using var input = new MemoryStream(original);
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(input, new() { BaseUri = new(Base, file), Resolver = resolver, CancellationToken = TestContext.Current.CancellationToken }));
        Assert.Contains(error.Diagnostics, d => d.Code == code);
        Assert.DoesNotContain(error.Diagnostics, d => d.Code.StartsWith("SCHEMA.", StringComparison.Ordinal));
        Assert.Equal(file == "idI150.xsd" ? 3 : 0, resolver.Requests);
        Assert.All(resolver.Streams, s => Assert.Equal(1, s.Disposals));
        Assert.True(input.CanRead);
    }
    internal sealed class Resolver(Dictionary<Uri, byte[]> map) : XmlResourceResolver
    {
        internal int Requests;
        internal readonly List<OwnedStream> Streams = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(XmlResourceKind.Schema, request.Kind);
            Assert.True(++Requests <= 3);
            Assert.True(map.TryGetValue(request.Uri, out var bytes));
            var stream = new OwnedStream(bytes!); Streams.Add(stream); return new(stream, request.Uri);
        }
    }
    internal sealed class OwnedStream(byte[] bytes) : MemoryStream(bytes)
    { internal int Disposals; protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); } }
    private sealed class BclResolver(Dictionary<Uri, byte[]> map) : System.Xml.XmlResolver
    {
        public override ICredentials? Credentials { set { } }
        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn) => new MemoryStream(map[absoluteUri]);
    }
}

using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdComponentMapping;

public sealed class WildcardWitnessTests
{
    [Theory]
    [InlineData("wildK001.xsd")]
    [InlineData("wildC030.xsd")]
    [InlineData("wildZ010_a.xsd")]
    public void OriginalEmptyNamespaceSchemasPublish(string file)
    {
        // Independent BCL schema oracle; only the pinned original import is allowed.
        var bcl = new System.Xml.Schema.XmlSchemaSet { XmlResolver = new BclResolver() };
        using var reader = System.Xml.XmlReader.Create(OriginalWitnessTests.Fixture(file)); bcl.Add(null, reader); bcl.Compile();
        var resolver = new Resolver(); using var input = File.OpenRead(OriginalWitnessTests.Fixture(file));
        Assert.NotNull(XmlSchema.Compile(input, new() { BaseUri = new("https://mapping.test/" + file), Resolver = resolver }));
        Assert.Equal(file == "wildZ010_a.xsd" ? 1 : 0, resolver.Requests); Assert.True(input.CanRead);
        Assert.All(resolver.Streams, s => Assert.Equal(1, s.Disposals));
    }
    private sealed class BclResolver : System.Xml.XmlResolver
    {
        public override System.Net.ICredentials? Credentials { set { } }
        public override object GetEntity(Uri uri, string? role, Type? type)
        { Assert.EndsWith("wildZ010_b.xsd", uri.AbsoluteUri); return File.OpenRead(OriginalWitnessTests.Fixture("wildZ010_b.xsd")); }
    }
    private sealed class Resolver : XmlResourceResolver
    {
        internal int Requests; internal readonly List<OwnedStream> Streams = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Assert.Equal(XmlResourceKind.Schema, request.Kind);
            Assert.Equal("https://mapping.test/wildZ010_b.xsd", request.Uri.AbsoluteUri); Assert.Equal(1, ++Requests);
            var stream = new OwnedStream(File.ReadAllBytes(OriginalWitnessTests.Fixture("wildZ010_b.xsd"))); Streams.Add(stream); return new(stream, request.Uri);
        }
    }
    private sealed class OwnedStream(byte[] bytes) : MemoryStream(bytes)
    { internal int Disposals; protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); } }
}

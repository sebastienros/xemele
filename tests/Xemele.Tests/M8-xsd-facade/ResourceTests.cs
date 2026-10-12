using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdFacade;
public sealed class ResourceTests
{
    private static readonly Uri Source = new("https://source.test/main.xsd");
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void SourceAndEffectiveBaseRemainDistinctAcrossAllInputs(int form)
    {
        var requests = new List<XmlResourceRequest>(); var streams = new List<OwnedStream>();
        var final = new Uri("https://redirect.test/final.xsd");
        string text = PublicTests.Wrap("<xs:include xml:base='https://base.test/' schemaLocation='child.xsd'/>");
        var options = new SchemaCompileOptions { BaseUri = Source, Resolver = new Resolver(r =>
        {
            requests.Add(r); var stream = new OwnedStream(requests.Count == 1
                ? PublicTests.Wrap("<xs:include schemaLocation='last.xsd'/>")
                : PublicTests.Wrap("<xs:element name='root' type='xs:int'/>"));
            streams.Add(stream); return new(stream, requests.Count == 1 ? final : r.Uri);
        }) };
        using var caller = new OwnedStream(text);
        var schema = form switch
        {
            0 => XmlSchema.Compile(text, options),
            1 => XmlSchema.Compile(XmlDocument.Parse(text, cancellationToken: TestContext.Current.CancellationToken), options),
            _ => XmlSchema.Compile(caller, options)
        };
        PublicTests.AssertResult(schema.Validate("<root>1</root>"), true);
        Assert.Equal(2, requests.Count); Assert.Equal(new Uri("https://base.test/child.xsd"), requests[0].Uri);
        Assert.Equal(Source, requests[0].Referrer); Assert.Equal(final, requests[1].Referrer);
        Assert.Equal(new Uri("https://redirect.test/last.xsd"), requests[1].Uri);
        Assert.All(requests, r => Assert.Equal(XmlResourceKind.Schema, r.Kind));
        Assert.All(streams, s => Assert.Equal(1, s.Disposals)); Assert.Equal(0, caller.Disposals);
    }
    [Fact]
    public void DeniedAndMissingResourcesRejectCompilation()
    {
        string source = PublicTests.Wrap("<xs:include schemaLocation='child.xsd'/>");
        var denied = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, new() { BaseUri = Source }));
        Assert.Contains(denied.Diagnostics, d => d.Code == "SCHEMA.ResourceDenied");
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, new() { BaseUri = Source, Resolver = new Resolver(_ => null) }));
    }
    [Fact]
    public void CallerExceptionsPreserveExactIdentity()
    {
        var sentinel = new SchemaCompileException([new("CALLER.Error", XmlDiagnosticSeverity.Error, "caller", default)]);
        string source = PublicTests.Wrap("<xs:include schemaLocation='child.xsd'/>");
        Assert.Same(sentinel, Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, new() { BaseUri = Source, Resolver = new Resolver(_ => throw sentinel) })));
        using var compileStream = new FaultStream(sentinel);
        Assert.Same(sentinel, Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(compileStream)));
        using var validateStream = new FaultStream(sentinel);
        Assert.Same(sentinel, Assert.Throws<SchemaCompileException>(() => PublicTests.Compile("<xs:element name='root'/>").Validate(validateStream)));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void RedefineRetainsOldDatatypeChecks(bool invalidOld)
    {
        string old = PublicTests.Wrap($"<xs:simpleType name='T'><xs:restriction base='xs:int'><xs:minInclusive value='{(invalidOld ? "bad" : "1")}'/></xs:restriction></xs:simpleType>");
        string main = PublicTests.Wrap("<xs:redefine schemaLocation='old.xsd'><xs:simpleType name='T'><xs:restriction base='T'><xs:maxInclusive value='4'/></xs:restriction></xs:simpleType></xs:redefine><xs:element name='root' type='T'/>");
        var options = new SchemaCompileOptions { BaseUri = Source, Resolver = new Resolver(r => new(new OwnedStream(old), r.Uri)) };
        if (invalidOld) Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(main, options));
        else { var schema = XmlSchema.Compile(main, options); PublicTests.AssertResult(schema.Validate("<root>2</root>"), true); PublicTests.AssertResult(schema.Validate("<root>5</root>"), false); }
    }
    private sealed class Resolver(Func<XmlResourceRequest, XmlResolvedResource?> callback) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return callback(request); } }
    private sealed class OwnedStream(string text) : MemoryStream(Encoding.UTF8.GetBytes(text))
    { internal int Disposals; protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); } }
    private sealed class FaultStream(Exception error) : MemoryStream
    { public override int Read(Span<byte> buffer) => throw error; }
}

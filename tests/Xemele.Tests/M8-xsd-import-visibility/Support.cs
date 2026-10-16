using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdImportVisibility;

internal static class Support
{
    internal static readonly Uri Basis = new("https://imports.test/main.xsd");
    internal static string Wrap(string body, string attributes = "") => $"<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' {attributes}>{body}</xs:schema>";
    internal const string ForeignType = "<xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>";
    internal static readonly string Foreign = Wrap(ForeignType + "<xs:element name='b' type='b:T'/><xs:attribute name='a' type='xs:int'/><xs:group name='g'><xs:sequence><xs:element ref='b:b'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute ref='b:a'/></xs:attributeGroup>", "targetNamespace='urn:b' xmlns:b='urn:b'");
    internal sealed class Resolver(Func<XmlResourceRequest, CancellationToken, XmlResourceResolution> callback) : XmlResourceResolver
    {
        internal readonly List<XmlResourceRequest> Requests = [];
        internal readonly List<OwnedStream> Streams = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) => throw new InvalidOperationException("Explicit outcomes only.");
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal(XmlResourceKind.Schema, request.Kind);
            Requests.Add(request);
            return callback(request, token);
        }
        internal XmlResourceResolution Resolved(string text, Uri final)
        {
            return Resolved(Encoding.UTF8.GetBytes(text), final);
        }
        internal XmlResourceResolution Resolved(byte[] bytes, Uri final)
        {
            var stream = new OwnedStream(bytes);
            Streams.Add(stream);
            return XmlResourceResolution.FromResource(new(stream, final));
        }
        internal void Disposed() => Assert.All(Streams, s => Assert.Equal(1, s.Disposals));
    }
    internal sealed class OwnedStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        internal int Disposals;
        internal Action? AfterRead;
        internal OwnedStream(string text) : this(Encoding.UTF8.GetBytes(text)) { }
        public override int Read(Span<byte> buffer)
        {
            var n = base.Read(buffer);
            AfterRead?.Invoke();
            return n;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }
    internal static Resolver Map(Dictionary<string, string> documents)
    {
        Resolver? resolver = null;
        resolver = new((r, _) => documents.TryGetValue(r.SystemId!, out var text) ? resolver!.Resolved(text, r.Uri) : XmlResourceResolution.Unavailable);
        return resolver;
    }
    internal static SchemaCompileException Reject(string text, Resolver resolver, string code = "XSD.SchemaRestriction", Uri? basis = null)
    {
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(text, new() { BaseUri = basis ?? Basis, Resolver = resolver }));
        Assert.Contains(error.Diagnostics, d => d.Code == code);
        resolver.Disposed();
        return error;
    }
}

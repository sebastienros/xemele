using System.Text;
using Xemele.Validation;
using Xunit;
using Document = Xemele.XmlDocument;

namespace Xemele.Tests.DtdValidation;

public sealed class ResourceAndStandaloneTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void CompileConsumesExternalSubsetBodyAndNestedParameterEntitiesAtFinalUris()
    {
        var resolver = new Resolver();
        resolver.Text["urn:pe:first"] = ("https://offline.invalid/first.dtd", "<?xml encoding='UTF-8'?><!ENTITY % next SYSTEM 'next.dtd'>%next;");
        resolver.Text["https://offline.invalid/next.dtd"] = ("https://offline.invalid/final.dtd", "<!ELEMENT r EMPTY>");
        var schema = DtdSchema.Compile("<!ENTITY % first SYSTEM 'urn:pe:first'>%first;", resolver, cancellationToken: Ct);
        Assert.True(schema.Validate(Document.Parse("<r/>", cancellationToken: Ct)).IsValid);
        Assert.Equal(["urn:pe:first", "https://offline.invalid/next.dtd"], resolver.Requests.Select(r => r.Uri.AbsoluteUri));
        Assert.Null(resolver.Requests[0].Referrer);
        Assert.Equal(new Uri("https://offline.invalid/first.dtd"), resolver.Requests[1].Referrer);
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
        Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!DOCTYPE r [<!ELEMENT r EMPTY>]>", cancellationToken: Ct));
    }

    [Fact]
    public void ResolverRefusalAndMissingAbsoluteBaseRejectWithoutAmbientAccess()
    {
        var missing = Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ENTITY % p SYSTEM 'urn:missing'>%p;", cancellationToken: Ct));
        Assert.StartsWith("DTD.", missing.Diagnostics[0].Code);
        var resolver = new Resolver();
        Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ENTITY % p SYSTEM 'relative.dtd'>%p;", resolver, cancellationToken: Ct));
        Assert.Empty(resolver.Requests);
        Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ENTITY % p SYSTEM 'urn:refused'>%p;", resolver, cancellationToken: Ct));
        Assert.Single(resolver.Requests);
        Assert.Empty(resolver.Streams);
    }

    [Fact]
    public void DuplicateDeclarationInExternalParameterEntityKeepsPhysicalUriAndOffset()
    {
        var resolver = new Resolver();
        resolver.Text["urn:pe:first"] = ("https://offline.invalid/child.dtd", "\n<!ELEMENT r ANY>");
        var exception = Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ELEMENT r EMPTY><!ENTITY % first SYSTEM 'urn:pe:first'>%first;", resolver, cancellationToken: Ct));
        var diagnostic = Assert.Single(exception.Diagnostics);
        Assert.Equal("DTD.DuplicateElement", diagnostic.Code);
        Assert.Equal(new XmlLocation(new Uri("https://offline.invalid/child.dtd"), 2, 1, 1), diagnostic.Location);
        Assert.Equal(1, Assert.Single(resolver.Streams).Disposals);
    }

    [Fact]
    public void ResolverIoAndCancellationExceptionsPropagateAndOwnedSourcesClose()
    {
        var resolver = new Resolver();
        var primary = new IOException("owned io");
        resolver.Text["urn:pe:first"] = ("https://offline.invalid/child.dtd", "<!ELEMENT r EMPTY>");
        resolver.ReadError = primary;
        Assert.Same(primary, Assert.Throws<IOException>(() => DtdSchema.Compile("<!ENTITY % first SYSTEM 'urn:pe:first'>%first;", resolver, cancellationToken: Ct)));
        Assert.Equal(1, Assert.Single(resolver.Streams).Disposals);
        using var cancellation = new CancellationTokenSource();
        var canceled = new Resolver { OnResolve = cancellation.Cancel };
        canceled.Text["urn:pe:first"] = ("https://offline.invalid/child.dtd", "<!ELEMENT r EMPTY>");
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => DtdSchema.Compile("<!ENTITY % first SYSTEM 'urn:pe:first'>%first;", canceled, cancellationToken: cancellation.Token)).CancellationToken);
        Assert.Equal(1, Assert.Single(canceled.Streams).Disposals);
        var callback = new Resolver { OnResolve = () => throw primary };
        Assert.Same(primary, Assert.Throws<IOException>(() => DtdSchema.Compile("<!ENTITY % first SYSTEM 'urn:pe:first'>%first;", callback, cancellationToken: Ct)));
    }

    [Fact]
    public void ExternalResourceAndByteQuotasRefuseAndCloseSources()
    {
        foreach (var limits in new XmlResourceLimits[] { new() { ExternalBytes = 1 }, new() { ExternalResources = 1 } })
        {
            var resolver = new Resolver();
            resolver.Text["urn:pe:first"] = ("https://offline.invalid/first.dtd", "<!ENTITY % next SYSTEM 'urn:pe:next'>%next;");
            resolver.Text["urn:pe:next"] = ("https://offline.invalid/next.dtd", "<!ELEMENT r EMPTY>");
            var exception = Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ENTITY % first SYSTEM 'urn:pe:first'>%first;", resolver, limits, Ct));
            Assert.StartsWith("DTD.", exception.Diagnostics[0].Code);
            Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
        }
    }

    [Fact]
    public void FromDocumentUsesRetainedCombinedModelWithoutResolverOrReparse()
    {
        var resolver = new Resolver();
        resolver.Text["urn:dtd"] = ("https://offline.invalid/external.dtd", "<!ELEMENT r EMPTY><!ATTLIST r a CDATA 'external'>");
        var document = Document.Parse("<!DOCTYPE r SYSTEM 'urn:dtd' [<!ATTLIST r a CDATA 'internal'>]><r/>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse,
            Resolver = resolver
        }, Ct);
        var opens = resolver.Requests.Count;
        resolver.OnResolve = () => throw new InvalidOperationException("No fetch permitted");
        var schema = DtdSchema.FromDocument(document);
        Assert.True(schema.Validate(document).IsValid);
        Assert.Equal("internal", document.RootElement.GetAttribute("a"));
        Assert.Equal(opens, resolver.Requests.Count);
    }

    [Fact]
    public void StandaloneMutatedMissingExternalDefaultAndNormalizationAreInvalid()
    {
        var document = Document.Parse("<?xml version='1.0' standalone='yes'?><r a='first' t='one'/>", cancellationToken: Ct);
        var schema = DtdSchema.Compile("<!ELEMENT r ANY><!ATTLIST r a CDATA 'first' t NMTOKEN #IMPLIED>", cancellationToken: Ct);
        Assert.True(schema.Validate(document).IsValid);
        document.RootElement.RemoveAttribute("a");
        document.RootElement.SetAttribute("t", " one ");
        var result = schema.Validate(document);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Diagnostics.Count(d => d.Code == "DTD.Standalone"));
        Assert.All(result.Diagnostics.Where(d => d.Code == "DTD.Standalone"), d => Assert.Equal(1, d.Location.LineNumber));
        Assert.Contains(result.Diagnostics, d => d.Code == "DTD.AttributeType");
    }

    [Fact]
    public void StandaloneNoAndAbsentAllowMissingExternalDefaultsWithoutMutation()
    {
        var schema = DtdSchema.Compile("<!ELEMENT r EMPTY><!ATTLIST r a CDATA 'first'>", cancellationToken: Ct);
        foreach (var declaration in new[] { "", "<?xml version='1.0' standalone='no'?>" })
        {
            var document = Document.Parse(declaration + "<r/>", cancellationToken: Ct);
            Assert.True(schema.Validate(document).IsValid);
            Assert.Null(document.RootElement.GetAttribute("a"));
        }
    }

    private sealed class Resolver : XmlResourceResolver
    {
        internal readonly Dictionary<string, (string Uri, string Body)> Text = new(StringComparer.Ordinal);
        internal readonly List<XmlResourceRequest> Requests = [];
        internal readonly List<Tracked> Streams = [];
        internal Action? OnResolve;
        internal IOException? ReadError;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            OnResolve?.Invoke();
            if (!Text.TryGetValue(request.Uri.AbsoluteUri, out var source))
            {
                return null;
            }

            var stream = new Tracked(Encoding.UTF8.GetBytes(source.Body), ReadError);
            Streams.Add(stream);
            return new(stream, new(source.Uri));
        }
    }
    private sealed class Tracked(byte[] bytes, IOException? error) : MemoryStream(bytes, false)
    {
        internal int Disposals;
        public override int Read(Span<byte> buffer)
        {
            if (error is not null)
            {
                throw error;
            }

            return base.Read(buffer[..Math.Min(1, buffer.Length)]);
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
}

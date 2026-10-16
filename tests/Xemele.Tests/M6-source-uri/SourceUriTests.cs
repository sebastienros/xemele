using System.Runtime.InteropServices;
using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M6SourceUri;

public sealed class SourceUriTests
{
    private static readonly Uri Primary = new("https://fixture.invalid/input/doc.xml");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static XmlParseOptions Options(Resolver resolver) => new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };

    [Fact]
    public void PrimarySourceResolvesSubsetAndEntityFromFinalDeclarationBase()
    {
        var resolver = new Resolver();
        resolver.Add("https://fixture.invalid/input/root.dtd", "https://fixture.invalid/redirect/root.dtd", "<!ELEMENT r (#PCDATA)><!ENTITY e SYSTEM 'body.ent'>");
        resolver.Add("https://fixture.invalid/redirect/body.ent", "https://fixture.invalid/content/final.ent", "right");
        using var input = new ProbeStream(Encoding.UTF8.GetBytes("<!DOCTYPE r SYSTEM 'root.dtd'><r>&e;</r>"));
        var document = XmlDocument.Load(input, Options(resolver), Primary, Ct);
        Assert.Equal("right", document.RootElement.GetTextContent());
        Assert.True(DtdSchema.FromDocument(document).Validate(document).IsValid);
        Request(resolver.Requests[0], "https://fixture.invalid/input/root.dtd", Primary, "root.dtd", XmlResourceKind.ExternalSubset);
        Request(resolver.Requests[1], "https://fixture.invalid/redirect/body.ent", new("https://fixture.invalid/redirect/root.dtd"), "body.ent", XmlResourceKind.ExternalEntity);
        Assert.Equal(0, input.Disposals);
        Assert.All(resolver.Streams, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public void NamedDtdResolvesNestedParameterEntitiesFromFinalBase()
    {
        var resolver = new Resolver();
        resolver.Add("https://fixture.invalid/input/first.dtd", "https://fixture.invalid/redirect/first.dtd", "<!ENTITY % q SYSTEM 'second.dtd'>%q;");
        resolver.Add("https://fixture.invalid/redirect/second.dtd", "https://fixture.invalid/content/final.dtd", "<!ELEMENT r EMPTY>");
        var schema = DtdSchema.Compile("<!ENTITY % p SYSTEM 'first.dtd'>%p;", resolver, null, Primary, Ct);
        Assert.True(schema.Validate(XmlDocument.Parse("<r/>", cancellationToken: Ct)).IsValid);
        Request(resolver.Requests[0], "https://fixture.invalid/input/first.dtd", Primary, "first.dtd", XmlResourceKind.ExternalEntity);
        Request(resolver.Requests[1], "https://fixture.invalid/redirect/second.dtd", new("https://fixture.invalid/redirect/first.dtd"), "second.dtd", XmlResourceKind.ExternalEntity);
        Assert.All(resolver.Streams, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public void InvalidSourceRejectsBeforeIo()
    {
        var resolver = new Resolver();
        using var input = new ProbeStream([]);
        Assert.Throws<ArgumentNullException>(() => XmlDocument.Load(input, Options(resolver), null!, Ct));
        Assert.Throws<ArgumentException>(() => XmlDocument.Load(input, Options(resolver), new("relative", UriKind.Relative), Ct));
        Assert.Throws<ArgumentNullException>(() => DtdSchema.Compile("%p;", resolver, null, null!, Ct));
        Assert.Throws<ArgumentException>(() => DtdSchema.Compile("%p;", resolver, null, new("relative", UriKind.Relative), Ct));
        Assert.Equal(0, input.Reads);
        Assert.Equal(0, input.Disposals);
        Assert.Empty(resolver.Requests);
    }

    [Fact]
    public void SourceDiagnosticsAndDeclaredEncodingRemainExact()
    {
        using var bad = new ProbeStream(Encoding.UTF8.GetBytes("<r>"));
        Assert.Equal(Primary, Assert.Single(Assert.Throws<XmlParseException>(() => XmlDocument.Load(bad, null, Primary, Ct)).Diagnostics).Location.SourceUri);
        Assert.Equal(Primary, Assert.Single(Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ELEMENT", null, null, Primary, Ct)).Diagnostics).Location.SourceUri);
        using var latin = new ProbeStream(Encoding.Latin1.GetBytes("<?xml version='1.0' encoding='ISO-8859-1'?><r>é</r>"));
        Assert.Equal("é", XmlDocument.Load(latin, null, Primary, Ct).RootElement.GetTextContent());
        var resolver = new Resolver();
        resolver.Add("https://fixture.invalid/input/root.dtd", "https://fixture.invalid/final/error.dtd", "<!ELEMENT");
        using var external = new ProbeStream(Encoding.UTF8.GetBytes("<!DOCTYPE r SYSTEM 'root.dtd'><r/>"));
        Assert.Equal(new Uri("https://fixture.invalid/final/error.dtd"), Assert.Single(Assert.Throws<XmlParseException>(() => XmlDocument.Load(external, Options(resolver), Primary, Ct)).Diagnostics).Location.SourceUri);
        Assert.Equal(1, Assert.Single(resolver.Streams).Disposals);
    }

    [Fact]
    public void OriginalReadResolverExceptionsAndBorrowedOwnershipSurvive()
    {
        var error = new IOException("primary");
        using var input = new ProbeStream([]) { Failure = error };
        Assert.Same(error, Assert.Throws<IOException>(() => XmlDocument.Load(input, null, Primary, Ct)));
        Assert.Equal(0, input.Disposals);
        var resolver = new Resolver { Failure = error };
        Assert.Same(error, Assert.Throws<IOException>(() => DtdSchema.Compile("<!ENTITY % p SYSTEM 'p'>%p;", resolver, null, Primary, Ct)));
        using var xml = new ProbeStream(Encoding.UTF8.GetBytes("<!DOCTYPE r SYSTEM 'p'><r/>"));
        Assert.Same(error, Assert.Throws<IOException>(() => XmlDocument.Load(xml, Options(resolver), Primary, Ct)));
        Assert.Equal(0, xml.Disposals);
    }

    [Fact]
    public void ExactQuotasAndEntryCancellationArePreserved()
    {
        using var exact = new ProbeStream("<r/>"u8.ToArray());
        XmlDocument.Load(exact, new()
        {
            Limits = new()
            {
                DocumentCharacters = 4
            }
        }, Primary, Ct);
        using var over = new ProbeStream("<r/>"u8.ToArray());
        Assert.Throws<XmlParseException>(() => XmlDocument.Load(over, new() { Limits = new() { DocumentCharacters = 3 } }, Primary, Ct));
        DtdSchema.Compile("<!ELEMENT long EMPTY>", null, new()
        {
            TokenCharacters = "<!ELEMENT long EMPTY>".Length
        }, Primary, Ct);
        Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ELEMENT long EMPTY>", null, new() { TokenCharacters = "<!ELEMENT long EMPTY>".Length - 1 }, Primary, Ct));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var input = new ProbeStream([]);
        var resolver = new Resolver();
        Assert.Equal(cancellation.Token, Assert.ThrowsAny<OperationCanceledException>(() => XmlDocument.Load(input, Options(resolver), Primary, cancellation.Token)).CancellationToken);
        Assert.Equal(cancellation.Token, Assert.ThrowsAny<OperationCanceledException>(() => DtdSchema.Compile("%p;", resolver, null, Primary, cancellation.Token)).CancellationToken);
        Assert.Equal(0, input.Reads);
        Assert.Empty(resolver.Requests);
    }

    // Intentional historical optional/default calls establish source compatibility.
#pragma warning disable xUnit1051
    [Fact]
    public void PreviousNullDefaultAndOptionalCallsCompileAndExecute()
    {
        for (var i = 0; i < 5; i++)
        {
            using var input = new MemoryStream("<r/>"u8.ToArray());
            var doc = i switch
            {
                0 => XmlDocument.Load(input),
                1 => XmlDocument.Load(input, null),
                2 => XmlDocument.Load(input, default),
                3 => XmlDocument.Load(input, null, default),
                _ => XmlDocument.Load(input, default, default)
            };
            Assert.Equal("r", doc.RootElement.LocalName);
        }
        Assert.True(DtdSchema.Compile("<!ELEMENT r EMPTY>").Validate(XmlDocument.Parse("<r/>", cancellationToken: Ct)).IsValid);
        DtdSchema.Compile("", null);
        DtdSchema.Compile("", default);
        DtdSchema.Compile("", null, null);
        DtdSchema.Compile("", default, default);
        DtdSchema.Compile("", null, null, default);
        DtdSchema.Compile("", default, default, default);
        using var unnamed = new MemoryStream("<r>"u8.ToArray());
        Assert.Null(Assert.Single(Assert.Throws<XmlParseException>(() => XmlDocument.Load(unnamed)).Diagnostics).Location.SourceUri);
        Assert.Null(Assert.Single(Assert.Throws<XmlParseException>(() => DtdSchema.Compile("<!ELEMENT")).Diagnostics).Location.SourceUri);
        Assert.StartsWith(".NET 10.", RuntimeInformation.FrameworkDescription);
    }

#pragma warning restore xUnit1051

    private static void Request(XmlResourceRequest request, string uri, Uri referrer, string system, XmlResourceKind kind)
    {
        Assert.Equal(new Uri(uri), request.Uri);
        Assert.Equal(referrer, request.Referrer);
        Assert.Equal(system, request.SystemId);
        Assert.Null(request.PublicId);
        Assert.Equal(kind, request.Kind);
    }
    private sealed class ProbeStream(byte[] bytes) : MemoryStream(bytes, false)
    {
        internal int Reads, Disposals; internal Exception? Failure;
        public override int Read(Span<byte> buffer)
        {
            Reads++;
            if (Failure is { } error)
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
    private sealed class Resolver : XmlResourceResolver
    {
        internal readonly Dictionary<string, (string Final, string Body)> Inputs = [];
        internal readonly List<XmlResourceRequest> Requests = []; internal readonly List<ProbeStream> Streams = [];
        internal Exception? Failure;
        internal void Add(string uri, string final, string body) => Inputs.Add(uri, (final, body));
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is { } error)
            {
                throw error;
            }

            if (!Inputs.TryGetValue(request.Uri.AbsoluteUri, out var input))
            {
                return null;
            }

            var stream = new ProbeStream(Encoding.UTF8.GetBytes(input.Body));
            Streams.Add(stream);
            return new(stream, new(input.Final));
        }
    }
}

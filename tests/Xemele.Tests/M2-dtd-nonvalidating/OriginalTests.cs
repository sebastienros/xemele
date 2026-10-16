using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Entities;
using Xunit;
using Model = Xemele.Parsing.Dtd.DtdModel;
namespace Xemele.Tests.DtdNonvalidating;

public sealed class OriginalTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            {
                dir = dir.Parent;
            }

            return dir!.FullName;
        }
    }
    private static string Resources => Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
    private static JsonElement[] Oracles()
    {
        using var file = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "tests/fixtures/M2-dtd-nonvalidating/original-oracles.json")));
        return [.. file.RootElement.EnumerateArray().Select(e => e.Clone())];
    }
    [Theory]
    [InlineData("invalid--005", false)]
    [InlineData("invalid--005", true)]
    [InlineData("invalid--006", false)]
    [InlineData("invalid--006", true)]
    [InlineData("invalid-not-sa-022", false)]
    [InlineData("invalid-not-sa-022", true)]
    [InlineData("ibm-invalid-P49-ibm49i01.xml", false)]
    [InlineData("ibm-invalid-P49-ibm49i01.xml", true)]
    [InlineData("ibm-invalid-P50-ibm50i01.xml", false)]
    [InlineData("ibm-invalid-P50-ibm50i01.xml", true)]
    [InlineData("rmt-e2e-14", false)]
    [InlineData("rmt-e2e-14", true)]
    public void OriginalBytesEveryPrimarySplitDefaultAndCompleteModels(string id, bool pause)
    {
        var expected = Oracles().Single(e => e.GetProperty("id").GetString() == id);
        var files = expected.GetProperty("files").EnumerateArray().ToArray();
        var input = Read(files[0]);
        var subset = Read(files[1]);
        Assert.All(input, b => Assert.InRange(b, (byte)0, (byte)127));
        var text = new UTF8Encoding(false, true).GetString(input);
        long[] maps = [.. Enumerable.Range(0, input.Length).Select(i => (long)i)];
        Uri source = new("https://xemele.invalid/corpus/" + files[0].GetProperty("path").GetString());
        Uri external = new("https://xemele.invalid/corpus/" + files[1].GetProperty("path").GetString());
        for (var split = 0; split <= text.Length; split++)
        {
            var resolver = new Resolver(external, subset);
            var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
            using var core = new XmlParserCore(options, source);
            var sink = new PausingSink(new DomSink(options), pause);
            core.CompleteByteSource(input.Length);
            var first = Feed(core, text.AsSpan(0, split), maps.AsSpan(0, split), false, sink);
            Assert.Null(first.Diagnostic);
            Assert.Equal(ScanStep.NeedInput, first.Step);
            Assert.Equal(split, first.Consumed);
            var last = Feed(core, text.AsSpan(split), maps.AsSpan(split), true, sink);
            Assert.Null(last.Diagnostic);
            Assert.Equal(ScanStep.Completed, last.Step);
            Assert.Equal(text.Length - split, last.Consumed);
            Assert.True(JsonElement.DeepEquals(expected.GetProperty("model"), JsonSerializer.SerializeToElement(Snapshot(sink.Document.Dtd!))), Pretty(Snapshot(sink.Document.Dtd!)));
            Assert.True(JsonElement.DeepEquals(expected.GetProperty("document"), JsonSerializer.SerializeToElement(Tree(sink.Document.RootElement))), Pretty(Tree(sink.Document.RootElement)));
            var request = Assert.Single(resolver.Requests);
            Assert.Equal(external, request.Uri);
            Assert.Equal(source, request.Referrer);
            Assert.Equal(XmlResourceKind.ExternalSubset, request.Kind);
            Assert.Equal(Path.GetFileName(external.LocalPath), request.SystemId);
            Assert.Null(request.PublicId);
            Assert.Equal(1, resolver.Disposals);
            Assert.InRange(resolver.MaximumRead, 1, 4096);
        }
    }
    [Fact]
    public void ExactSixImmutableProfileExpectationsAndAllResourceBytes()
    {
        using var profile = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "tests/corpora/xml/m2-applicability.json")));
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "tests/corpora/xml/inventory.json")));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, "tests/corpora/xml/inventory.json")))), profile.RootElement.GetProperty("inventorySha256").GetString());
        var cases = profile.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(2585, cases.Length);
        var files = profile.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(3386, files.Length);
        foreach (var oracle in Oracles())
        {
            var row = Assert.Single(cases, c => c.GetProperty("id").GetString()!.EndsWith("#" + oracle.GetProperty("id").GetString(), StringComparison.Ordinal));
            Assert.Equal("accepted", Assert.Single(row.GetProperty("expectedOutcomes").EnumerateArray()).GetString());
            Assert.Equal("nonvalidating-validity", row.GetProperty("rule").GetString());
            foreach (var resource in oracle.GetProperty("files").EnumerateArray())
            {
                var registered = Assert.Single(files, f => f.GetProperty("path").GetString() == resource.GetProperty("path").GetString());
                Assert.Equal(registered.GetProperty("sha256").GetString(), resource.GetProperty("sha256").GetString());
                Read(resource);
            }
        }
        Assert.Equal(inventory.RootElement.GetProperty("files").GetRawText(), profile.RootElement.GetProperty("files").GetRawText());
    }
    private static byte[] Read(JsonElement row)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Resources, row.GetProperty("path").GetString()!));
        Assert.Equal(row.GetProperty("size").GetInt32(), bytes.Length);
        Assert.Equal(row.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return bytes;
    }
    private static string Pretty(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
    private static object Location(XmlLocation at) => new { uri = at.SourceUri?.AbsoluteUri, line = at.LineNumber, column = at.LinePosition, offset = at.ByteOffset };
    private static object Particle(DtdParticle p) => new { kind = p.Kind.ToString(), name = p.Name, occurrence = p.Occurrence.ToString(), children = p.Children.Select(Particle).ToArray() };
    internal static object Snapshot(Model m) => new
    {
        elements = m.Elements.Select(e => new { name = e.Name, content = Particle(e.Content), external = e.IsExternal, location = Location(e.Location) }).ToArray(),
        attributes = m.Attributes.Select(a => new { element = a.ElementName, name = a.Name, type = a.Type.ToString(), tokens = a.Tokens, defaultKind = a.DefaultKind.ToString(), defaultValue = a.DefaultValue, external = a.IsExternal, location = Location(a.Location) }).ToArray(),
        general = m.GeneralEntities.Select(Entity).ToArray(),
        parameter = m.ParameterEntities.Select(Entity).ToArray(),
        notations = m.Notations.Select(n => new { name = n.Name, system = n.SystemId, publicId = n.PublicId, location = Location(n.Location) }).ToArray()
    };
    private static object Entity(DtdEntityDeclaration e) => new { name = e.Name, parameter = e.IsParameter, replacement = e.ReplacementText, system = e.SystemId, publicId = e.PublicId, notation = e.NotationName, external = e.IsExternal, location = Location(e.Location) };
    private static object Tree(XmlNode e)
    {
        var attributes = new List<object>();
        var children = new List<object>();
        foreach (var a in e.Attributes)
        {
            attributes.Add(new
            {
                name = a.LocalName,
                ns = a.NamespaceUri,
                value = a.Value
            });
        }

        foreach (var c in e.Children)
        {
            children.Add(c.Kind == XmlNodeKind.Element ? Tree(c) : c.Value);
        }

        return new
        {
            name = e.LocalName,
            ns = e.NamespaceUri,
            attributes,
            children
        };
    }
    private static ScanResult Feed(XmlParserCore core, ReadOnlySpan<char> text, ReadOnlySpan<long> maps, bool final, PausingSink sink)
    {
        var consumed = 0;
        ScanResult r;
        var calls = 0;
        do
        {
            Assert.True(++calls < 10000, "bounded paused progress");
            r = core.Append(text[consumed..], maps[consumed..], final, sink, Ct);
            Assert.InRange(r.Consumed, 0, text.Length - consumed);
            consumed += r.Consumed;
        } while (r.Step == ScanStep.Paused);
        return new(consumed, r.Step, r.Diagnostic);
    }
    private sealed class PausingSink(DomSink inner, bool pause) : IXmlEventSink, IXmlDtdSink
    {
        internal XmlDocument Document => inner.Document;
        private bool Continue(bool result)
        {
            Assert.True(result);
            return !pause;
        }
        public bool StartDocument(XmlLocation at) => Continue(inner.StartDocument(at));
        public bool Declaration(XmlDeclarationView declaration) => Continue(inner.Declaration(declaration));
        public bool DocumentType(XmlDocumentType declaration, Model model, XmlLocation at) => Continue(((IXmlDtdSink)inner).DocumentType(declaration, model, at));
        public bool StartElement(XmlNameView name, bool empty, XmlLocation at) => Continue(inner.StartElement(name, empty, at));
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => Continue(inner.NamespaceDeclaration(declaration));
        public bool Attribute(XmlAttributeView attribute) => Continue(inner.Attribute(attribute));
        public bool StartElementContent() => Continue(inner.StartElementContent());
        public bool EndElement(XmlNameView name, XmlLocation at) => Continue(inner.EndElement(name, at));
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation at) => Continue(inner.StartText(kind, target, at));
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation at) => Continue(inner.TextFragment(text, ended, at));
        public bool EndDocument(XmlLocation at) => Continue(inner.EndDocument(at));
    }
    private sealed class Resolver(Uri expected, byte[] body) : XmlResourceResolver
    {
        internal readonly List<XmlResourceRequest> Requests = []; internal int Disposals, MaximumRead;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            Assert.Equal(expected, request.Uri);
            return new(new OwnedStream(this, body), expected);
        }
        private sealed class OwnedStream(Resolver owner, byte[] bytes) : MemoryStream(bytes, false)
        {
            public override int Read(Span<byte> buffer)
            {
                owner.MaximumRead = Math.Max(owner.MaximumRead, buffer.Length);
                return base.Read(buffer[..Math.Min(1, buffer.Length)]);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    owner.Disposals++;
                }

                base.Dispose(disposing);
            }
        }
    }
}

using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Xemele.Parsing.Input;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using DtdModelType = Xemele.Parsing.Dtd.DtdModel;
using Xunit;

namespace Xemele.Tests.DtdEntityBase;

public sealed class EntityBaseTests
{
    private static readonly Uri Primary = new("https://fixture.invalid/main/document.xml");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static XmlParseOptions Options(Resolver resolver) => new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };

    [Fact]
    public void OriginalE18BytesHaveIndependentExpectedRequestsCallbacksAndCompleteDtdModel()
    {
        const string baseUri = "https://xemele.invalid/corpus/xmlconf/eduni/errata-2e/";
        string resources = Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
        byte[] Read(string name, int length, string hash)
        {
            byte[] value = File.ReadAllBytes(Path.Combine(resources, "xmlconf/eduni/errata-2e", name));
            Assert.Equal(length, value.Length); Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(value))); return value;
        }
        byte[] input = Read("E18.xml", 107, "5968be3ce43799a857ff2cfba9a431b525087a3293230d74a5d7e8b5d2eb169b");
        var resolver = new Resolver();
        var entries = new[]
        {
            ("E18-ent", 28, "e7a9fdebb7317d1073212319dcbc5b5f8579767eb2b6b4d6b9e6d2494ce11a57"),
            ("subdir1/E18-ent", 27, "ecee6a41b9f298da762e1e87c91720d54c6da75ecd9ec40f7d560b2f065c914c"),
            ("subdir1/E18-pe", 76, "0236d72a7cc8455872d2f5886a4a43cf7ad291a616b52ce7ac82559c8bf4f850"),
            ("subdir2/E18-ent", 27, "3d708d346b5e26672c50fee84748a0b377dabe482cf8f0b2c848886724ca1449"),
            ("subdir2/E18-extpe", 31, "62eddd46cd27a7726da7a5e84f00e0e5d3e53b26b89276fd8fdfb2c060da1b46")
        };
        foreach (var (path, length, hash) in entries)
        {
            byte[] value = Read(path, length, hash);
            string text = new UTF8Encoding(false, true).GetString(value);
            Assert.Equal(value, Encoding.UTF8.GetBytes(text));
            resolver.Add(baseUri + path, baseUri + path, text);
        }
        byte[] canonical = Read("out/E18.xml", 39, "6f408fff4e526b0f7ba4571b96a4d2b2249e05106b2e19b7e050db4798179f7a");
        Assert.Equal("<foo>entity from main dir, right!</foo>", Encoding.UTF8.GetString(canonical));
        Uri source = new(baseUri + "E18.xml"), pe = new(baseUri + "subdir1/E18-pe");
        var options = Options(resolver); using var decoder = new XmlInputDecoder(options, source);
        using var core = new XmlParserCore(options, source); var sink = new Sink();
        char[] characters = new char[2]; long[] offsets = new long[2]; int position = 0; bool validated = false;
        while (true)
        {
            var decoded = decoder.Decode(input.AsSpan(position), characters, offsets, true, Ct);
            position += decoded.ConsumedBytes; Assert.Null(decoded.Diagnostic);
            if (!validated) { Assert.Null(decoder.ValidateDeclaration([], new(source, 1, 1, 0))); validated = true; }
            var scanned = core.Append(characters.AsSpan(0, decoded.WrittenCharacters), offsets.AsSpan(0, decoded.WrittenCharacters), false, sink, Ct);
            Assert.Null(scanned.Diagnostic); Assert.Equal(ScanStep.NeedInput, scanned.Step);
            if (decoded.Step == DecodeStep.Completed) break;
            Assert.Equal(DecodeStep.NeedOutput, decoded.Step);
        }
        Assert.Equal(input.Length, position); core.CompleteByteSource(input.Length);
        Assert.Equal(ScanStep.Completed, core.Append([], [], true, sink, Ct).Step);
        Assert.Equal("entity from main dir, right!", sink.Text.ToString());
        Assert.Equal(new[] { "document", "dtd", "text:Text", "start:foo", "content", "text:Text", "end:foo", "text:Text", "complete" }, sink.Events);
        var semantic = new[]
        {
            new SortedDictionary<string, object>(StringComparer.Ordinal) { ["attributes"] = Array.Empty<object>(), ["kind"] = "start", ["localName"] = sink.RootName!, ["namespaceUri"] = sink.RootNamespace! },
            new SortedDictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "text", ["value"] = sink.Text.ToString() },
            new SortedDictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "end", ["localName"] = sink.RootName!, ["namespaceUri"] = sink.RootNamespace! }
        };
        Assert.Equal("3c0ec688b6357710e11084a81ff3476fe0d9017e450b2270d30bad3bc8f98bc2", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(semantic)))));
        Assert.Equal(3, resolver.Requests.Count);
        Request(resolver.Requests[0], baseUri + "subdir1/E18-pe", source, XmlResourceKind.ExternalEntity, "subdir1/E18-pe");
        Request(resolver.Requests[1], baseUri + "subdir2/E18-extpe", pe, XmlResourceKind.ExternalEntity, "../subdir2/E18-extpe");
        Request(resolver.Requests[2], baseUri + "E18-ent", source, XmlResourceKind.ExternalEntity, "E18-ent");
        var model = sink.Model!; Assert.Single(model.Elements); Assert.Empty(model.Attributes); Assert.Empty(model.Notations);
        Assert.Single(model.GeneralEntities); Assert.Equal(3, model.ParameterEntities.Count);
        var element = model.Elements[0]; Assert.Equal("foo", element.Name); Assert.False(element.IsExternal);
        Assert.Equal(new XmlLocation(source, 2, 1, 16), element.Location);
        Assert.Equal(DtdParticleKind.Any, element.Content.Kind); Assert.Null(element.Content.Name);
        Assert.Equal(DtdOccurrence.Once, element.Content.Occurrence); Assert.Empty(element.Content.Children);
        Entity(model.GeneralEntities[0], "ent", false, null, "E18-ent", false, new(source, 1, 1, null));
        Entity(model.ParameterEntities[0], "pe", true, null, "subdir1/E18-pe", false, new(source, 3, 1, 35));
        Entity(model.ParameterEntities[1], "extpe", true, null, "../subdir2/E18-extpe", true, new(pe, 1, 1, 0));
        Entity(model.ParameterEntities[2], "intpe", true, "<!ENTITY ent SYSTEM 'E18-ent'>\n", null, true, new(pe, 2, 1, 48));
        Cleanup(resolver);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InternalReplacementDeclarationsUseContainingFinalSource(bool external, bool nested)
    {
        const string x = "<!ENTITY e SYSTEM 'body.ent'>";
        string subset = "<!ENTITY % x \"" + x + "\"><!ENTITY % y '&#37;x;'>";
        string inclusion = nested ? "%y;" : "%x;";
        string input = external
            ? "<!DOCTYPE r SYSTEM 'https://fixture.invalid/request/ext.dtd' [" + subset + "]><r>&e;</r>"
            : "<!DOCTYPE r [" + subset + inclusion + "]><r>&e;</r>";
        Uri containing = external ? new("https://fixture.invalid/final/ext.dtd") : Primary;
        var resolver = new Resolver();
        if (external) resolver.Add("https://fixture.invalid/request/ext.dtd", containing.AbsoluteUri, "<?xml encoding='UTF-8'?>" + inclusion);
        resolver.Add(new Uri(containing, "body.ent").AbsoluteUri, "https://fixture.invalid/content/body.ent", "right!");
        var sink = Parse(input, resolver);
        Assert.Equal("right!", sink.Text.ToString());
        Assert.Equal(new[] { "document", "dtd", "start:r", "content", "text:Text", "end:r", "complete" }, sink.Events);
        Model(sink.Model!, 1, 2);
        Entity(sink.Model!.GeneralEntities[0], "e", false, null, "body.ent", external, new(containing, 1, 1, null));
        Entity(sink.Model.ParameterEntities[0], "x", true, x, null, false,
            new(Primary, 1, input.IndexOf("<!ENTITY % x", StringComparison.Ordinal) + 1, null));
        Entity(sink.Model.ParameterEntities[1], "y", true, "%x;", null, false,
            new(Primary, 1, input.IndexOf("<!ENTITY % y", StringComparison.Ordinal) + 1, null));
        int index = 0;
        if (external) Request(resolver.Requests[index++], "https://fixture.invalid/request/ext.dtd", Primary, XmlResourceKind.ExternalSubset, "https://fixture.invalid/request/ext.dtd");
        Request(resolver.Requests[index], new Uri(containing, "body.ent").AbsoluteUri, containing, XmlResourceKind.ExternalEntity, "body.ent");
        Assert.Equal(index + 1, resolver.Requests.Count);
        Cleanup(resolver);
    }

    [Fact]
    public void ExternalParameterDeclaredInOneDirectoryAndReferencedInAnotherUsesFirstDeclaration()
    {
        const string input = "<!DOCTYPE r SYSTEM 'https://fixture.invalid/request/ext.dtd'><r>&e;</r>";
        var resolver = CrossDirectory();
        var sink = Parse(input, resolver);
        Assert.Equal("right!", sink.Text.ToString());
        Assert.Equal(new[] { "document", "dtd", "start:r", "content", "text:Text", "end:r", "complete" }, sink.Events);
        var model = sink.Model!; Model(model, 1, 2);
        Uri declaration = new("https://fixture.invalid/declaration/ext.dtd");
        Uri reference = new("https://fixture.invalid/reference/ref.dtd");
        Uri included = new("https://fixture.invalid/actual/payload.dtd");
        Entity(model.ParameterEntities[0], "p", true, null, "payload.dtd", true, new(declaration, 1, 1, 0));
        Entity(model.ParameterEntities[1], "q", true, null, "../reference/ref.dtd", true, new(declaration, 1, 35, 34));
        Entity(model.GeneralEntities[0], "e", false, null, "body.ent", true, new(included, 1, 1, 0));
        Assert.Equal(4, resolver.Requests.Count);
        Request(resolver.Requests[0], "https://fixture.invalid/request/ext.dtd", Primary, XmlResourceKind.ExternalSubset, "https://fixture.invalid/request/ext.dtd");
        Request(resolver.Requests[1], reference.AbsoluteUri, declaration, XmlResourceKind.ExternalEntity, "../reference/ref.dtd");
        Request(resolver.Requests[2], "https://fixture.invalid/declaration/payload.dtd", declaration, XmlResourceKind.ExternalEntity, "payload.dtd");
        Request(resolver.Requests[3], "https://fixture.invalid/actual/body.ent", included, XmlResourceKind.ExternalEntity, "body.ent");
        Cleanup(resolver);
    }

    [Theory]
    [InlineData("denial")]
    [InlineData("quota")]
    [InlineData("cancellation")]
    [InlineData("primary-error")]
    public void CorrectedExternalParameterBoundaryPreservesCleanupAndPrimaryError(string failure)
    {
        var resolver = CrossDirectory();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var primary = new IOException("resolver primary failure");
        resolver.BeforeResolve = request =>
        {
            if (request.Uri.AbsoluteUri != "https://fixture.invalid/declaration/payload.dtd") return;
            if (failure == "denial") resolver.Inputs.Remove(request.Uri.AbsoluteUri);
            if (failure == "cancellation") cancellation.Cancel();
            if (failure == "primary-error") throw primary;
        };
        if (failure == "primary-error") resolver.ThrowOnDispose = true;
        var options = Options(resolver);
        if (failure == "quota") options = options with { Limits = options.Limits with { ExternalResources = 2 } };
        using var core = new XmlParserCore(options, Primary); var sink = new Sink();
        const string input = "<!DOCTYPE r SYSTEM 'https://fixture.invalid/request/ext.dtd'><r>&e;</r>";
        if (failure == "cancellation") Assert.ThrowsAny<OperationCanceledException>(() => core.Append(input, [], true, sink, cancellation.Token));
        else if (failure == "primary-error") Assert.Same(primary, Assert.Throws<IOException>(() => core.Append(input, [], true, sink, cancellation.Token)));
        else
        {
            var result = core.Append(input, [], true, sink, cancellation.Token);
            Assert.Equal(ScanStep.Rejected, result.Step);
            Assert.Equal(failure == "quota" ? "XML.ResourceLimit" : "XML.ResourceDenied", result.Diagnostic!.Code);
            Assert.Equal(new Uri("https://fixture.invalid/declaration/ext.dtd"), result.Diagnostic.Location.SourceUri);
        }
        Assert.Equal(new[] { "document" }, sink.Events); Assert.Null(sink.Model);
        Assert.Equal(failure == "quota" ? 2 : 3, resolver.Requests.Count);
        Cleanup(resolver);
    }

    private static Resolver CrossDirectory()
    {
        var resolver = new Resolver();
        resolver.Add("https://fixture.invalid/request/ext.dtd", "https://fixture.invalid/declaration/ext.dtd",
            "<!ENTITY % p SYSTEM 'payload.dtd'><!ENTITY % q SYSTEM '../reference/ref.dtd'>%q;");
        resolver.Add("https://fixture.invalid/reference/ref.dtd", "https://fixture.invalid/reference/ref.dtd", "<!ENTITY % p SYSTEM 'wrong.dtd'>%p;");
        resolver.Add("https://fixture.invalid/declaration/payload.dtd", "https://fixture.invalid/actual/payload.dtd", "<!ENTITY e SYSTEM 'body.ent'>");
        resolver.Add("https://fixture.invalid/actual/body.ent", "https://fixture.invalid/content/body.ent", "right!");
        return resolver;
    }
    private static Sink Parse(string input, Resolver resolver)
    {
        using var core = new XmlParserCore(Options(resolver), Primary); var sink = new Sink();
        var result = core.Append(input, [], true, sink, Ct);
        Assert.Null(result.Diagnostic); Assert.Equal(ScanStep.Completed, result.Step); Assert.Equal(input.Length, result.Consumed);
        return sink;
    }
    private static void Model(DtdModelType model, int general, int parameter)
    {
        Assert.Empty(model.Elements); Assert.Empty(model.Attributes); Assert.Empty(model.Notations);
        Assert.Equal(general, model.GeneralEntities.Count); Assert.Equal(parameter, model.ParameterEntities.Count);
    }
    private static void Entity(DtdEntityDeclaration entity, string name, bool parameter, string? replacement, string? system, bool external, XmlLocation location)
    {
        Assert.Equal(name, entity.Name); Assert.Equal(parameter, entity.IsParameter); Assert.Equal(replacement, entity.ReplacementText);
        Assert.Equal(system, entity.SystemId); Assert.Null(entity.PublicId); Assert.Null(entity.NotationName);
        Assert.Equal(external, entity.IsExternal); Assert.Equal(location, entity.Location);
    }
    private static void Request(XmlResourceRequest request, string uri, Uri referrer, XmlResourceKind kind, string system)
    {
        Assert.Equal(uri, request.Uri.AbsoluteUri); Assert.Equal(referrer, request.Referrer); Assert.Equal(kind, request.Kind);
        Assert.Equal(system, request.SystemId); Assert.Null(request.PublicId);
    }
    private static void Cleanup(Resolver resolver)
    {
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
        Assert.All(resolver.Streams, stream => Assert.InRange(stream.MaximumRequest, 1, 4096));
    }
    private sealed class Resolver : XmlResourceResolver
    {
        internal readonly Dictionary<string, (string Final, string Body)> Inputs = [];
        internal readonly List<XmlResourceRequest> Requests = [];
        internal readonly List<OwnedStream> Streams = [];
        internal Action<XmlResourceRequest>? BeforeResolve;
        internal bool ThrowOnDispose;
        internal void Add(string uri, string final, string body) => Inputs.Add(uri, (final, body));
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request); BeforeResolve?.Invoke(request); cancellationToken.ThrowIfCancellationRequested();
            if (!Inputs.TryGetValue(request.Uri.AbsoluteUri, out var input)) return null;
            var stream = new OwnedStream(Encoding.UTF8.GetBytes(input.Body), ThrowOnDispose); Streams.Add(stream);
            return new(stream, new(input.Final));
        }
    }
    private sealed class OwnedStream(byte[] bytes, bool throwing) : MemoryStream(bytes, false)
    {
        internal int Disposals, MaximumRequest;
        public override int Read(Span<byte> buffer)
        { MaximumRequest = Math.Max(MaximumRequest, buffer.Length); return base.Read(buffer[..Math.Min(1, buffer.Length)]); }
        protected override void Dispose(bool disposing)
        { if (disposing) { Disposals++; base.Dispose(disposing); if (throwing) throw new IOException("cleanup failure"); } }
    }
    private sealed class Sink : IXmlEventSink, IXmlDtdSink
    {
        internal readonly List<string> Events = [];
        internal readonly StringBuilder Text = new();
        internal DtdModelType? Model;
        internal string? RootName, RootNamespace;
        private int depth;
        public bool StartDocument(XmlLocation location) { Events.Add("document"); return true; }
        public bool Declaration(XmlDeclarationView declaration) { Events.Add("declaration"); return true; }
        public bool DocumentType(XmlDocumentType declaration, DtdModelType model, XmlLocation location) { Events.Add("dtd"); Model = model; return true; }
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
        { if (depth == 0) { RootName = name.LocalName.ToString(); RootNamespace = name.NamespaceUri.ToString(); } depth++; Events.Add("start:" + name.LocalName.ToString()); return true; }
        public bool NamespaceDeclaration(XmlNamespaceView declaration) { Events.Add("namespace"); return true; }
        public bool Attribute(XmlAttributeView attribute) { Events.Add("attribute"); return true; }
        public bool StartElementContent() { Events.Add("content"); return true; }
        public bool EndElement(XmlNameView name, XmlLocation location) { depth--; Events.Add("end:" + name.LocalName.ToString()); return true; }
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) { Events.Add("text:" + kind); return true; }
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) { if (depth > 0) Text.Append(text); return true; }
        public bool EndDocument(XmlLocation location) { Events.Add("complete"); return true; }
    }
}

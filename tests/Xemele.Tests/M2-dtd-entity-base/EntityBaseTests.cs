using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.DtdEntityBase;

public sealed class EntityBaseTests
{
    private static readonly Uri Primary = new("https://fixture.invalid/main/document.xml");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static XmlParseOptions Options(Resolver resolver) => new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };

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
    private static void Model(DtdModel model, int general, int parameter)
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
        internal DtdModel? Model;
        public bool StartDocument(XmlLocation location) { Events.Add("document"); return true; }
        public bool Declaration(XmlDeclarationView declaration) { Events.Add("declaration"); return true; }
        public bool DocumentType(XmlDocumentType declaration, DtdModel model, XmlLocation location) { Events.Add("dtd"); Model = model; return true; }
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) { Events.Add("start:" + name.LocalName.ToString()); return true; }
        public bool NamespaceDeclaration(XmlNamespaceView declaration) { Events.Add("namespace"); return true; }
        public bool Attribute(XmlAttributeView attribute) { Events.Add("attribute"); return true; }
        public bool StartElementContent() { Events.Add("content"); return true; }
        public bool EndElement(XmlNameView name, XmlLocation location) { Events.Add("end:" + name.LocalName.ToString()); return true; }
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) { Events.Add("text:" + kind); return true; }
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) { Text.Append(text); return true; }
        public bool EndDocument(XmlLocation location) { Events.Add("complete"); return true; }
    }
}

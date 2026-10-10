using System.Reflection;
using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class ResourceBoundaryTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static XmlParseOptions Options(XmlResourceResolver? resolver = null) => new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };

    [Fact]
    public void LargeExternalEntityMustEmitBeforeStreamAllowsItsSuffix()
    {
        var resolver = new StreamingResolver();
        var sink = new StreamingSink(resolver);
        using var core = new XmlParserCore(Options(resolver));
        const string input = "<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:large'>]><r>&e;</r>";
        Assert.Equal(ScanStep.Completed, core.Append(input, [], true, sink, Cancellation).Step);
        Assert.True(resolver.SuffixReadAfterCallback); Assert.Equal(20000, sink.Characters);
        Assert.True(resolver.Eof); Assert.Equal(1, resolver.Disposals); Assert.InRange(resolver.MaximumRequest, 1, 4096);
    }

    [Fact]
    public void CumulativeRawExpansionAndByteQuotasHaveIndependentExactBoundaries()
    {
        const string primary = "<!DOCTYPE r SYSTEM 'urn:d'><r>&e;&e;</r>";
        const string declaration = "<!ENTITY e SYSTEM 'urn:e'>";
        const string body = "abc";
        XmlResourceResolver Resolver() => new ProductionTests.OfflineResolver(new()
            { ["urn:d"] = ("urn:d", declaration), ["urn:e"] = ("urn:e", body) });
        var exact = Options(Resolver()) with { Limits = new()
        {
            DocumentCharacters = primary.Length,
            ExpandedCharacters = declaration.Length + 2 * body.Length,
            ExternalBytes = declaration.Length + 2 * body.Length,
            ExternalResources = 3, InternedNames = 2
        } };
        Assert.Equal("abcabc", XmlDocument.Parse(primary, exact, Cancellation).RootElement.GetTextContent());
        foreach (var limits in new[]
        {
            exact.Limits with { DocumentCharacters = primary.Length - 1 },
            exact.Limits with { ExpandedCharacters = declaration.Length + 2 * body.Length - 1 },
            exact.Limits with { ExternalBytes = declaration.Length + 2 * body.Length - 1 },
            exact.Limits with { ExternalResources = 2 },
            exact.Limits with { InternedNames = 1 }
        })
        {
            var resolver = Assert.IsType<ProductionTests.OfflineResolver>(Resolver());
            Assert.False(XmlDocument.TryParse(primary, out _, out var diagnostics, exact with { Limits = limits, Resolver = resolver }, Cancellation));
            Assert.Contains("Limit", Assert.Single(diagnostics).Code);
            Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
        }
    }

    [Fact]
    public void SharedDepthAcrossInternalAndExternalReferencesRejectsBeforeExtraWork()
    {
        const string primary = "<!DOCTYPE r [<!ENTITY outer '&e;'><!ENTITY e SYSTEM 'urn:e'>]><r>&outer;</r>";
        var resolver = new ProductionTests.OfflineResolver(new() { ["urn:e"] = ("urn:e", "&#65;") });
        var exact = Options(resolver) with { Limits = new() { EntityDepth = 3, ExpandedCharacters = 9 } };
        Assert.Equal("A", XmlDocument.Parse(primary, exact, Cancellation).RootElement.GetTextContent());
        resolver = new(new() { ["urn:e"] = ("urn:e", "&#65;") });
        Assert.False(XmlDocument.TryParse(primary, out _, out var diagnostics, exact with { Resolver = resolver, Limits = exact.Limits with { EntityDepth = 2 } }, Cancellation));
        Assert.Equal("XML.ResourceLimit", Assert.Single(diagnostics).Code);
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
    }

    [Theory]
    [InlineData("<!DOCTYPE r SYSTEM 'urn:d'><r/>")]
    [InlineData("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:e'>]><r>&e;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY % e SYSTEM 'urn:e'>%e;]><r/>")]
    public void NullResolverDeniesEachActualExternalBoundary(string source)
    {
        Assert.False(XmlDocument.TryParse(source, out _, out var diagnostics, Options(), Cancellation));
        Assert.Equal("XML.ResourceDenied", Assert.Single(diagnostics).Code);
    }

    [Theory]
    [InlineData("<?xml encoding='UTF-8'?><?xml encoding='UTF-8'?><!ENTITY e 'v'>")]
    [InlineData("<?xml encoding='unknown'?>\u0000")]
    public void ExternalDeclarationValidationAndSecondDeclarationReject(string body)
    {
        var resolver = new ProductionTests.OfflineResolver(new() { ["urn:d"] = ("urn:d", body) });
        Assert.False(XmlDocument.TryParse("<!DOCTYPE r SYSTEM 'urn:d'><r/>", out _, out var diagnostics, Options(resolver), Cancellation));
        Assert.Equal(body.Contains("unknown", StringComparison.Ordinal) ? "INPUT.UnsupportedEncoding" : "DTD.Syntax", Assert.Single(diagnostics).Code);
        Assert.Equal(1, Assert.Single(resolver.Streams).Disposals);
    }

    [Fact]
    public void CallbackExceptionSurvivesThrowingNestedResourceDisposal()
    {
        var disposal = new IOException("cleanup"); var primary = new InvalidOperationException("sink");
        var resolver = new NestedResolver(disposal); using var core = new XmlParserCore(Options(resolver));
        var sink = new ThrowingSink(primary);
        Assert.Same(primary, Assert.Throws<InvalidOperationException>(() => core.Append(NestedInput, [], true, sink, Cancellation)));
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
        Assert.Equal(0, Budget(core).Count(Quota.EntityDepth));
    }

    [Fact]
    public void PausedDisposeDrainsAllAcceptedFramesAndPreservesFirstCleanupException()
    {
        var cleanup = new IOException("innermost cleanup"); var resolver = new NestedResolver(cleanup);
        var core = new XmlParserCore(Options(resolver)); var sink = new ContentTests.RecordingSink(4);
        var result = core.Append(NestedInput, [], true, sink, Cancellation);
        Assert.Equal(ScanStep.Paused, result.Step); Assert.Equal(2, Budget(core).Count(Quota.EntityDepth));
        Assert.Same(cleanup, Assert.Throws<IOException>(core.Dispose));
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
        Assert.Equal(0, Budget(core).Count(Quota.EntityDepth)); core.Dispose();
    }

    private const string NestedInput = "<!DOCTYPE r [<!ENTITY outer SYSTEM 'urn:outer'><!ENTITY inner SYSTEM 'urn:inner'>]><r>&outer;</r>";
    private static OperationBudget Budget(XmlParserCore core) => Assert.IsType<OperationBudget>(typeof(XmlParserCore).GetField("budget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core));

    private sealed class NestedResolver(Exception cleanup) : XmlResourceResolver
    {
        internal readonly List<CleanupStream> Streams = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool inner = request.Uri.AbsoluteUri == "urn:inner";
            var stream = new CleanupStream(Encoding.UTF8.GetBytes(inner ? "long text" : "&inner;"), inner ? cleanup : null);
            Streams.Add(stream); return new(stream, request.Uri);
        }
    }
    private sealed class CleanupStream(byte[] bytes, Exception? exception) : MemoryStream(bytes, false)
    {
        internal int Disposals;
        protected override void Dispose(bool disposing)
        { if (disposing) { Disposals++; base.Dispose(disposing); if (exception is not null) throw exception; } }
    }
    private sealed class ThrowingSink(Exception primary) : IXmlEventSink
    {
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView declaration) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) => true;
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
        public bool Attribute(XmlAttributeView attribute) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => throw primary;
        public bool EndDocument(XmlLocation location) => true;
    }

    private sealed class StreamingResolver : XmlResourceResolver
    {
        internal bool CallbackSeen, SuffixReadAfterCallback, Eof;
        internal int Disposals, MaximumRequest;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) =>
            new(new GateStream(this, Encoding.UTF8.GetBytes("<b>" + new string('x', 20000) + "</b>")), request.Uri);
    }
    private sealed class GateStream(StreamingResolver owner, byte[] bytes) : MemoryStream(bytes, false)
    {
        public override int Read(Span<byte> destination)
        {
            owner.MaximumRequest = Math.Max(owner.MaximumRequest, destination.Length);
            if (Position >= 4096)
            { Assert.True(owner.CallbackSeen, "External suffix may be read only after a real callback."); owner.SuffixReadAfterCallback = true; }
            int count = base.Read(destination); owner.Eof |= count == 0; return count;
        }
        protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); }
    }
    private sealed class StreamingSink(StreamingResolver owner) : IXmlEventSink
    {
        internal int Characters;
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView declaration) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) => true;
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
        public bool Attribute(XmlAttributeView attribute) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location)
        { if (text.Length != 0) { Assert.False(owner.Eof); owner.CallbackSeen = true; Characters += text.Length; } return true; }
        public bool EndDocument(XmlLocation location) => true;
    }
}

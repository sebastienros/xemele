using System.Reflection;
using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class ResumeAndDefaultValidationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void MetadataPauseThenDifferentResumeTokenReachesResolverAndPreservesCancellationIdentity()
    {
        using var first = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        using var second = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var resolver = new CancelOnGeneralResolver(second);
        using var core = new XmlParserCore(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
        var sink = new PauseMetadataSink(); const string input = "<!DOCTYPE r SYSTEM 'urn:d'><r>&e;</r>";
        var paused = core.Append(input, [], true, sink, first.Token);
        Assert.Equal(ScanStep.Paused, paused.Step); Assert.Equal(1, sink.Metadata);
        var exception = Assert.ThrowsAny<OperationCanceledException>(() => core.Append(input.AsSpan(paused.Consumed), [], true, sink, second.Token));
        Assert.Same(resolver.Exception, exception); Assert.Equal(second.Token, exception.CancellationToken);
        Assert.Equal(new[] { first.Token, second.Token }, resolver.Tokens); Assert.Equal(1, resolver.Stream!.Disposals);
        Assert.Equal(0, Budget(core).Count(Quota.EntityDepth));
    }

    [Theory]
    [InlineData("#0;", false)]
    [InlineData("#x110000;", false)]
    [InlineData("#999999999999999999999999999;", false)]
    [InlineData("#;", false)]
    [InlineData("#x;", false)]
    [InlineData("#9;", true)]
    public void UnusedExternalDefaultValidatesReachableNumericReferenceWithoutApplyingOrExpandingIt(string suffix, bool accepted)
    {
        string external = "<!ENTITY e '&#38;" + suffix + "'><!ATTLIST r a CDATA '&e;'>";
        var resolver = new ProductionTests.OfflineResolver(new() { ["urn:d"] = ("urn:d", external) });
        using var core = new XmlParserCore(new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
        var sink = new ContentTests.RecordingSink();
        var result = core.Append("<!DOCTYPE r SYSTEM 'urn:d'><r a='explicit'/>", [], true, sink, Cancellation);
        Assert.Equal(accepted ? ScanStep.Completed : ScanStep.Rejected, result.Step);
        if (!accepted) Assert.Equal("XML.Syntax", result.Diagnostic!.Code);
        // External grammar raw units + one declaration-time '&#38;' scalar.
        // The unused default and its reachable reference are never expanded.
        Assert.Equal(external.Length + 1, Budget(core).Count(Quota.ExpandedCharacters));
        Assert.Equal(1, Assert.Single(resolver.Streams).Disposals);
    }

    [Fact]
    public void UseSiteQuotaRefusalPrecedesHugeReplacementTailValidation()
    {
        string replacement = "xx" + new string('x', 100000) + "&a:b;";
        var entities = new XmlEntityContext(new([], [],
            [new DtdEntityDeclaration("e", false, replacement, null, null, null, false, default)], [], []), Cancellation);
        using var core = new XmlParserCore(new() { Limits = new() { ExpandedCharacters = 1 } }, entities);
        var result = core.Append("<r a='&e;'/>", [], true, new ContentTests.RecordingSink(), Cancellation);
        Assert.Equal("XML.ResourceLimit", result.Diagnostic!.Code);
        Assert.Equal(1, Budget(core).Count(Quota.ExpandedCharacters)); Assert.Equal(0, Budget(core).Count(Quota.EntityDepth));
    }

    [Fact]
    public void UseSiteDepthRefusalPrecedesDeepTailValidation()
    {
        var declarations = new List<DtdEntityDeclaration>();
        for (int i = 0; i < 10000; i++)
            declarations.Add(new("e" + i, false, i == 9999 ? "&a:b;" : "&e" + (i + 1) + ";", null, null, null, false, default));
        using var core = new XmlParserCore(new() { Limits = new() { EntityDepth = 1 } },
            new XmlEntityContext(new([], [], declarations, [], []), Cancellation));
        var result = core.Append("<r a='&e0;'/>", [], true, new ContentTests.RecordingSink(), Cancellation);
        Assert.Equal("XML.ResourceLimit", result.Diagnostic!.Code); Assert.Equal(0, Budget(core).Count(Quota.EntityDepth));
        Assert.Equal(4, Budget(core).Count(Quota.ExpandedCharacters));
    }

    private static OperationBudget Budget(XmlParserCore core) => Assert.IsType<OperationBudget>(typeof(XmlParserCore).GetField("budget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core));
    private sealed class CancelOnGeneralResolver(CancellationTokenSource second) : XmlResourceResolver
    {
        internal readonly List<CancellationToken> Tokens = [];
        internal OperationCanceledException? Exception;
        internal ProductionTests.BoundedStream? Stream;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Tokens.Add(cancellationToken);
            if (request.Kind == XmlResourceKind.ExternalSubset)
            { Stream = new(Encoding.UTF8.GetBytes("<!ENTITY e SYSTEM 'urn:e'>"), 1); return new(Stream, request.Uri); }
            second.Cancel();
            try { cancellationToken.ThrowIfCancellationRequested(); }
            catch (OperationCanceledException exception) { Exception = exception; throw; }
            throw new InvalidOperationException("Incorrect resume token allowed canceled resolver work.");
        }
    }
    private sealed class PauseMetadataSink : IXmlEventSink, IXmlDtdSink
    {
        internal int Metadata;
        public bool DocumentType(XmlDocumentType declaration, Xemele.Parsing.Dtd.DtdModel model, XmlLocation location) { Metadata++; return false; }
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView declaration) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) => true;
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
        public bool Attribute(XmlAttributeView attribute) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => true;
        public bool EndDocument(XmlLocation location) => true;
    }
}

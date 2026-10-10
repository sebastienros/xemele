using System.Reflection;
using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class ProductionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static XmlParseOptions Options(XmlResourceResolver? resolver = null) => new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };

    [Fact]
    public void InternalDtdDefaultsNamespacesTypesAndParsedMarkupReachRealDom()
    {
        const string subset = "\r\n<!ENTITY e 'owned'><!ENTITY markup '<child>&e;</child>'>" +
            "<!ATTLIST r xmlns CDATA 'urn:r' xmlns:p CDATA 'urn:p' p:a NMTOKENS '  &e;  tail  ' id ID #IMPLIED>";
        string source = "<!DOCTYPE r [" + subset + "]><r id='  duplicate  '>&markup;<child id='duplicate'/></r>";
        var document = XmlDocument.Parse(source, Options(), Cancellation);
        Assert.Equal("r", document.DocumentType!.Name); Assert.Equal(subset, document.DocumentType.InternalSubset);
        Assert.Equal("urn:r", document.RootElement.NamespaceUri); Assert.Equal("owned tail", document.RootElement.GetAttribute("a", "urn:p"));
        Assert.Equal("duplicate", document.RootElement.GetAttribute("id")); Assert.Equal("owned", document.RootElement.GetTextContent());
        Assert.Equal("child", document.RootElement.FirstChild.LocalName); Assert.Equal("urn:r", document.RootElement.FirstChild.NamespaceUri);
        Assert.Equal(2, document.RootElement.NamespaceDeclarations.Count);
    }

    [Fact]
    public void DefaultExpandedDuplicateAndNamespaceQuotaRejectBeforeTagCallback()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST r xmlns:p CDATA 'urn:u' xmlns:q CDATA 'urn:u' q:a CDATA 'default'>]><r p:a='explicit'/>";
        Assert.False(XmlDocument.TryParse(source, out _, out var diagnostics, Options(), Cancellation));
        Assert.Equal("XML.Namespace", Assert.Single(diagnostics).Code);
        const string quota = "<!DOCTYPE r [<!ATTLIST r xmlns CDATA 'urn:r' a CDATA 'v'>]><r/>";
        var limits = Options() with { Limits = new() { AttributesPerElement = 2 } };
        Assert.Equal("v", XmlDocument.Parse(quota, limits, Cancellation).RootElement.GetAttribute("a"));
        Assert.False(XmlDocument.TryParse(quota, out _, out diagnostics, limits with { Limits = limits.Limits with { AttributesPerElement = 1 } }, Cancellation));
        Assert.Equal("XML.ResourceLimit", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void ExternalSubsetUsesInternalParametersAndOverridesWithRedirectedNestedBase()
    {
        var resolver = new OfflineResolver(new()
        {
            ["https://original.example/ext.dtd"] = ("https://final.example/dir/ext.dtd", "<?xml encoding='UTF-8'?>\r\n%x;<!ENTITY e 'wrong'><!ENTITY % child SYSTEM 'child.ent'>%child;"),
            ["https://final.example/dir/child.ent"] = ("https://final.example/other/child.ent", "<?xml encoding='UTF-8'?><!ENTITY external SYSTEM 'body.ent'>"),
            ["https://final.example/other/body.ent"] = ("https://content.example/body.ent", "<?xml encoding='UTF-8'?>😀<b>&e;</b>")
        });
        const string source = "<!DOCTYPE r SYSTEM 'https://original.example/ext.dtd' [<!ENTITY % x \"<!ATTLIST r a CDATA 'inside'>\"><!ENTITY e 'internal'>]><r>&external;</r>";
        var document = XmlDocument.Parse(source, Options(resolver), Cancellation);
        Assert.Equal("inside", document.RootElement.GetAttribute("a")); Assert.Equal("😀internal", document.RootElement.GetTextContent());
        Assert.Equal("b", document.RootElement.LastChild.LocalName);
        Assert.Equal(3, resolver.Requests.Count); Assert.Equal("https://final.example/dir/ext.dtd", resolver.Requests[1].Referrer!.AbsoluteUri);
        Assert.Equal("https://final.example/other/child.ent", resolver.Requests[2].Referrer!.AbsoluteUri);
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
        Assert.All(resolver.Streams, stream => Assert.InRange(stream.MaximumRequest, 1, 4096));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<?xml encoding='UTF-8'?>")]
    [InlineData("<!-- [ ] > --><?p ] >?>")]
    public void EmptyNestedParameterSourcesResumeSavedParentSuffix(string first)
    {
        var resolver = new OfflineResolver(new()
        {
            ["urn:external"] = ("urn:external", "<!ENTITY % a SYSTEM 'urn:a'>%a;<!ENTITY e 'saved'>"),
            ["urn:a"] = ("urn:a", first)
        });
        var document = XmlDocument.Parse("<!DOCTYPE r SYSTEM 'urn:external'><r>&e;</r>", Options(resolver), Cancellation);
        Assert.Equal("saved", document.RootElement.GetTextContent()); Assert.Equal(2, resolver.Requests.Count);
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
    }

    [Theory]
    [InlineData("<!ATTLIST r a CDATA 'v'>", "<r/>", true)]
    [InlineData("<!ATTLIST r a NMTOKENS #IMPLIED>", "<r a=' x  y '/>", true)]
    [InlineData("<!ENTITY e 'v'>", "<r>&e;</r>", true)]
    [InlineData("<!ENTITY e 'v'>", "<r a='&e;'/>", true)]
    [InlineData("<!ELEMENT r (child)><!ELEMENT child EMPTY>", "<r> <child/></r>", true)]
    [InlineData("<!ATTLIST r a NMTOKENS #IMPLIED>", "<r a='x y'/>", false)]
    [InlineData("<!ATTLIST r a CDATA 'v'>", "<r a='explicit'/>", false)]
    public void StandaloneChecksDependenciesWithoutTurningValidityIntoFailure(string declarations, string body, bool rejected)
    {
        var resolver = new OfflineResolver(new() { ["urn:external"] = ("urn:external", declarations) });
        string source = "<?xml version='1.0' standalone='yes'?><!DOCTYPE r SYSTEM 'urn:external'>" + body;
        Assert.Equal(!rejected, XmlDocument.TryParse(source, out _, out var diagnostics, Options(resolver), Cancellation));
        if (rejected) Assert.Equal("XML.Standalone", Assert.Single(diagnostics).Code);
        Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals));
    }

    [Fact]
    public void IdBookkeepingAcceptsDuplicatesAndRequiredFixedOrLexicalValidityFailures()
    {
        const string source = "<!DOCTYPE different [<!ELEMENT r EMPTY><!ATTLIST r id ID #IMPLIED required CDATA #REQUIRED fixed CDATA #FIXED 'v'>]><r id='duplicate' fixed='other'><r id='duplicate'/></r>";
        using var core = new XmlParserCore(Options()); var sink = new ContentTests.RecordingSink();
        Assert.Equal(ScanStep.Completed, core.Append(source, [], true, sink, Cancellation).Step);
        var ids = Assert.IsType<Dictionary<string, XmlLocation>>(typeof(XmlParserCore).GetField("ids", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(core));
        Assert.Single(ids); Assert.True(ids.ContainsKey("duplicate"));
    }

    [Fact]
    public void RealDtdMetadataPausesExactlyOnceAtEverySourceSplit()
    {
        const string source = "<!DOCTYPE r [<!ENTITY e '<b>value</b>'>]><r>&e;</r>";
        for (int split = 0; split <= source.Length; split++)
        {
            using var core = new XmlParserCore(Options()); var sink = new MetadataSink();
            int index = 0; bool first = true; int calls = 0;
            while (true)
            {
                Assert.True(++calls < 1000);
                int end = first ? split : source.Length;
                var result = core.Append(source.AsSpan(index, end - index), [], !first, sink, Cancellation);
                index += result.Consumed;
                Assert.NotEqual(ScanStep.Rejected, result.Step);
                if (result.Step == ScanStep.Completed) break;
                if (result.Step == ScanStep.NeedInput) first = false;
            }
            Assert.Equal(1, sink.Metadata); Assert.True(sink.RootAfterMetadata);
        }
    }

    private sealed class MetadataSink : IXmlEventSink, IXmlDtdSink
    {
        internal int Metadata;
        internal bool RootAfterMetadata;
        public bool DocumentType(XmlDocumentType declaration, Xemele.Parsing.Dtd.DtdModel model, XmlLocation location) { Metadata++; return false; }
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView declaration) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) { RootAfterMetadata |= Metadata == 1; return true; }
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
        public bool Attribute(XmlAttributeView attribute) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => true;
        public bool EndDocument(XmlLocation location) => true;
    }

    internal sealed class OfflineResolver(Dictionary<string, (string Final, string Body)> inputs) : XmlResourceResolver
    {
        internal readonly List<XmlResourceRequest> Requests = [];
        internal readonly List<BoundedStream> Streams = [];
        internal int ReadSize = 4096;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Requests.Add(request);
            if (!inputs.TryGetValue(request.Uri.AbsoluteUri, out var input)) return null;
            var stream = new BoundedStream(Encoding.UTF8.GetBytes(input.Body), ReadSize); Streams.Add(stream);
            return new(stream, new(input.Final));
        }
    }

    internal sealed class BoundedStream(byte[] bytes, int readSize) : MemoryStream(bytes, false)
    {
        internal int Disposals, MaximumRequest;
        public override int Read(Span<byte> destination)
        { MaximumRequest = Math.Max(MaximumRequest, destination.Length); return base.Read(destination[..Math.Min(readSize, destination.Length)]); }
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
}

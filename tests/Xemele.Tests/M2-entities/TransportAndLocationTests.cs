using System.Text;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class TransportAndLocationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static XmlParseOptions Options(XmlResourceResolver? resolver = null) => new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };

    [Fact]
    public void PrimaryDoctypeByteMappingAndOwnedRawSubsetSurviveEachReadSize()
    {
        const string source = "<?xml version='1.0'?><!DOCTYPE r [\r\n<!ENTITY e '😀'>\r\n]><r>&e;</r>";
        foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
        {
            byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(source)];
            for (int size = 1; size <= bytes.Length; size++)
            {
                using var stream = new ProductionTests.BoundedStream(bytes, size);
                var document = XmlDocument.Load(stream, Options(), Cancellation);
                Assert.Equal("😀", document.RootElement.GetTextContent()); Assert.Equal("\r\n<!ENTITY e '😀'>\r\n", document.DocumentType!.InternalSubset);
                var entity = Assert.Single(document.Dtd!.GeneralEntities);
                Assert.Equal(2, entity.Location.LineNumber); Assert.Equal(1, entity.Location.LinePosition);
                Assert.Equal(encoding.GetPreamble().Length + encoding.GetByteCount("<?xml version='1.0'?><!DOCTYPE r [\r\n"), entity.Location.ByteOffset);
                Assert.Equal(0, stream.Disposals);
            }
        }
    }

    [Fact]
    public void ExternalEntitySourceUriScalarCursorAndOriginalMappingsSurviveEveryReadSize()
    {
        const string text = "<?xml encoding='UTF-16'?>\r\n😀\r\n</r>";
        var encoding = new UnicodeEncoding(false, true, true);
        byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
        long offset = encoding.GetPreamble().Length + encoding.GetByteCount("<?xml encoding='UTF-16'?>\r\n😀\r\n");
        for (int size = 1; size <= bytes.Length; size++)
        {
            var resolver = new BinaryResolver(bytes, size); using var core = new XmlParserCore(Options(resolver));
            var result = core.Append("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>", [], true, new ContentTests.RecordingSink(), Cancellation);
            Assert.Equal(ScanStep.Rejected, result.Step); Assert.Equal("XML.Syntax", result.Diagnostic!.Code);
            Assert.Equal(new Uri("https://final.example/entity"), result.Diagnostic.Location.SourceUri);
            Assert.Equal(3, result.Diagnostic.Location.LineNumber); Assert.Equal(1, result.Diagnostic.Location.LinePosition); Assert.Equal(offset, result.Diagnostic.Location.ByteOffset);
            Assert.Equal(1, resolver.Stream!.Disposals);
        }
    }

    [Theory]
    [InlineData("<broken")]
    [InlineData("<b>")]
    [InlineData("&unfinished")]
    [InlineData("<?xml ")]
    public void ExternalEofDiagnosticsUseActualAbsoluteByteLengthIncludingBom(string body)
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(body)];
        var resolver = new BinaryResolver(bytes, 1);
        Assert.False(XmlDocument.TryParse("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>", out _, out var diagnostics, Options(resolver), Cancellation));
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(new Uri("https://final.example/entity"), diagnostic.Location.SourceUri); Assert.Equal(bytes.Length, diagnostic.Location.ByteOffset);
        Assert.Equal(1, resolver.Stream!.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyOrBomOnlyExternalSubsetIsCompletedWithoutInventingBody(bool bom)
    {
        var resolver = new BinaryResolver(bom ? [0xEF, 0xBB, 0xBF] : [], 1);
        var document = XmlDocument.Parse("<!DOCTYPE r SYSTEM 'urn:entity'><r/>", Options(resolver), Cancellation);
        Assert.Equal("r", document.RootElement.LocalName); Assert.Empty(document.Dtd!.GeneralEntities);
        Assert.Equal(1, resolver.Stream!.Disposals);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("<b>")]
    [InlineData("<?xml encoding='UTF-8'?>text")]
    public void MalformedExternalBytesReturnStructuredDecoderFailure(string prefix)
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes(prefix), 0xFF];
        var resolver = new BinaryResolver(bytes, 1);
        Assert.False(XmlDocument.TryParse("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>", out _, out var diagnostics, Options(resolver), Cancellation));
        Assert.Equal("INPUT.InvalidEncoding", Assert.Single(diagnostics).Code); Assert.Equal(1, resolver.Stream!.Disposals);
    }

    [Fact]
    public void ValidDecodedBodyPrefixGrammarFailurePrecedesLaterInvalidByte()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes("<?xml encoding='UTF-8'?></r>"), 0xFF];
        var resolver = new BinaryResolver(bytes, 4096);
        Assert.False(XmlDocument.TryParse("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>", out _, out var diagnostics, Options(resolver), Cancellation));
        Assert.Equal("XML.Syntax", Assert.Single(diagnostics).Code); Assert.Equal(1, resolver.Stream!.Disposals);
    }

    [Fact]
    public void BufferedValidPrefixEventuallyPublishesDecoderFailureWithExactSourceOffset()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes("<?xml encoding='UTF-8'?>prefix\r\n😀"), 0xFF];
        var resolver = new BinaryResolver(bytes, 4096); using var core = new XmlParserCore(Options(resolver));
        var sink = new ContentTests.RecordingSink();
        var result = core.Append("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>", [], true, sink, Cancellation);
        Assert.Equal(ScanStep.Rejected, result.Step); Assert.Equal("INPUT.InvalidEncoding", result.Diagnostic!.Code);
        Assert.Equal(new Uri("https://final.example/entity"), result.Diagnostic.Location.SourceUri);
        Assert.Equal(bytes.Length - 1, result.Diagnostic.Location.ByteOffset);
        Assert.Equal(2, result.Diagnostic.Location.LineNumber); Assert.Equal(2, result.Diagnostic.Location.LinePosition);
        Assert.Contains("prefix\n😀", sink.Output.ToString(), StringComparison.Ordinal); Assert.Equal(1, resolver.Stream!.Disposals);
    }

    private sealed class BinaryResolver(byte[] bytes, int size) : XmlResourceResolver
    {
        internal ProductionTests.BoundedStream? Stream;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Stream = new(bytes, size); return new(Stream, new("https://final.example/entity")); }
    }
}

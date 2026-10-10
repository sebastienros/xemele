using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;
using Xemele.Tests.Syntax;
using Xemele.Tests.Entities;
using Xunit;

namespace Xemele.Tests.Versions;

public sealed class VersionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    public static TheoryData<string> Versions => new() { "1.0", "1.1", "1.7", "1.00", "1.10", "1." + new string('9', 257) };

    [Theory, MemberData(nameof(Versions))]
    public void DocumentAllSplitsAndCallbackPausesHaveIndependentExactOracle(string version)
    {
        string declaration = $"<?xml version='{version}' encoding='UTF-8' standalone='no'?>";
        string xml = declaration + "<r/>";
        string[] expected = ["document", $"declaration:{version}:UTF-8:False", "start::r::True", "content", "end::r:", "end-document"];
        for (int split = 0; split <= xml.Length; split++)
        foreach (bool pause in new[] { false, true })
        {
            using var core = new XmlParserCore(new());
            var sink = new SyntaxTests.Sink { Pause = pause };
            ScanResult result = default;
            foreach (var segment in new[] { (Start: 0, Length: split, Final: false), (Start: split, Length: xml.Length - split, Final: true) })
            {
                int consumed = 0, turns = 0;
                do
                {
                    result = core.Append(xml.AsSpan(segment.Start + consumed, segment.Length - consumed), [], segment.Final, sink, Cancellation);
                    Assert.InRange(result.Consumed, 0, segment.Length - consumed);
                    consumed += result.Consumed;
                    Assert.True(++turns < xml.Length + 20);
                } while (result.Step == ScanStep.Paused);
                Assert.Equal(segment.Length, consumed);
            }
            Assert.Equal(ScanStep.Completed, result.Step);
            Assert.Equal(expected, sink.Events);
            Assert.Equal(new XmlLocation(null, 1, 1, null), sink.Locations[1]);
            Assert.Equal(new XmlLocation(null, 1, declaration.Length + 1, null), sink.Locations[2]);
            Assert.Equal(new XmlLocation(null, 1, xml.Length + 1, null), sink.Locations[^1]);
        }
    }

    [Theory, MemberData(nameof(Versions))]
    public void TextAllSplitsOwnExactValuesAndStopBeforeBody(string version)
    {
        string declaration = $"<?xml version='{version}' encoding='X'?>";
        string input = declaration + "tail";
        for (int split = 0; split <= input.Length; split++)
        {
            char[] chars = input.ToCharArray();
            long[] offsets = Enumerable.Range(0, input.Length).Select(i => (long)i + 3).ToArray();
            using var framer = new XmlTextDeclarationFramer(new(), new Uri("urn:version"));
            var result = framer.Append(chars.AsSpan(0, split), offsets.AsSpan(0, split), false, Cancellation);
            int consumed = result.Consumed;
            if (result.Step == TextDeclarationStep.NeedInput)
            {
                Assert.Equal(split, consumed);
                result = framer.Append(chars.AsSpan(split), offsets.AsSpan(split), true, Cancellation);
                consumed += result.Consumed;
            }
            Assert.Equal(TextDeclarationStep.Declared, result.Step);
            Assert.Null(result.Diagnostic);
            Assert.Equal(declaration.Length, consumed);
            var owned = result.Declaration!.Value;
            Array.Fill(chars, 'x'); Array.Fill(offsets, 0); framer.Dispose();
            Assert.Equal(version, owned.Version); Assert.Equal("X", owned.EncodingName);
            Assert.Equal(new XmlLocation(new Uri("urn:version"), 1, 1, 3), owned.Location);
            Assert.Equal(1, result.NextLocation.LineNumber); Assert.Equal(declaration.Length + 1, result.NextLocation.LinePosition);
        }
    }

    [Theory]
    [InlineData("1.")] [InlineData("1.١")] [InlineData("1.０")] [InlineData("1.&#48;")]
    [InlineData("2.0")] [InlineData("1.0x")] [InlineData("1..0")] [InlineData("")]
    public void InvalidVersionsRejectAtEverySplit(string version)
    {
        string doc = $"<?xml version='{version}'?><r/>";
        string text = $"<?xml version='{version}' encoding='UTF-8'?>";
        for (int split = 0; split <= doc.Length; split++)
        {
            using var core = new XmlParserCore(new()); var sink = new SyntaxTests.Sink();
            var result = core.Append(doc.AsSpan(0, split), [], false, sink, Cancellation);
            if (result.Step == ScanStep.NeedInput) result = core.Append(doc.AsSpan(split), [], true, sink, Cancellation);
            Assert.Equal(ScanStep.Rejected, result.Step); Assert.Equal("XML.Syntax", result.Diagnostic!.Code);
            Assert.Equal(new[] { "document" }, sink.Events);
        }
        for (int split = 0; split <= text.Length; split++)
        {
            using var framer = new XmlTextDeclarationFramer(new());
            var result = framer.Append(text.AsSpan(0, split), [], false, Cancellation);
            if (result.Step == TextDeclarationStep.NeedInput) result = framer.Append(text.AsSpan(split), [], true, Cancellation);
            Assert.Equal(TextDeclarationStep.Rejected, result.Step); Assert.Equal("XML.Syntax", result.Diagnostic!.Code);
        }
    }

    [Theory]
    [InlineData("<r>\u0001</r>", "XML.Syntax")]
    [InlineData("<r>&#1;</r>", "XML.Syntax")]
    [InlineData("<r xmlns:p=''/>", "XML.Namespace")]
    public void CompatibleVersionKeepsXml10ContentRules(string body, string code)
    {
        Assert.False(XmlDocument.TryParse("<?xml version='1.1'?>" + body, out _, out var diagnostics, cancellationToken: Cancellation));
        Assert.Equal(code, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void OptionalTextVersionAndLongSingleUnitRetentionRespectQuotas()
    {
        using var absent = new XmlTextDeclarationFramer(new());
        Assert.Null(absent.Append("<?xml encoding='X'?>", [], true, Cancellation).Declaration!.Value.Version);
        string version = "1." + new string('0', 4096);
        string text = $"<?xml version='{version}' encoding='X'?>";
        using var exact = new XmlTextDeclarationFramer(new() { TokenCharacters = text.Length, ExpandedCharacters = text.Length });
        TextDeclarationResult result = default;
        for (int i = 0; i < text.Length; i++) result = exact.Append(text.AsSpan(i, 1), [], i == text.Length - 1, Cancellation);
        Assert.Equal(TextDeclarationStep.Declared, result.Step); Assert.Equal(version, result.Declaration!.Value.Version);
        using var token = new XmlTextDeclarationFramer(new() { TokenCharacters = text.Length - 1 });
        Assert.Equal("XML.ResourceLimit", token.Append(text, [], true, Cancellation).Diagnostic!.Code);
        using var aggregate = new XmlTextDeclarationFramer(new() { ExpandedCharacters = 32, TokenCharacters = null });
        result = aggregate.Append(text, [], true, Cancellation);
        Assert.Equal(32, result.Consumed); Assert.Equal("XML.ResourceLimit", result.Diagnostic!.Code);
        string doc = $"<?xml version='{version}'?><r/>";
        using var docExact = new XmlParserCore(new() { Limits = new() { TokenCharacters = doc.IndexOf("<r/>", StringComparison.Ordinal) } });
        Assert.Equal(ScanStep.Completed, docExact.Append(doc, [], true, new SyntaxTests.Sink(), Cancellation).Step);
    }

    [Theory, MemberData(nameof(Versions))]
    public void ByteExternalSubsetParameterAndGeneralEntityCompositionRetainsDomAndMetadata(string version)
    {
        foreach (int readSize in new[] { 1, 2, 7, 4096 })
        {
            var resolver = new Resolver(version, readSize);
            using var primary = new ProductionTests.BoundedStream(Encoding.UTF8.GetBytes($"<?xml version='{version}'?><!DOCTYPE r SYSTEM 'urn:dtd'><r>&e;</r>"), readSize);
            var document = XmlDocument.Load(primary, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver }, Cancellation);
            Assert.Equal("r", document.RootElement.LocalName); Assert.Equal("kept", document.RootElement.GetAttribute("a"));
            Assert.Equal("😀owned", document.RootElement.GetTextContent()); Assert.Equal("b", document.RootElement.FirstChild.LocalName);
            Assert.Equal("urn:dtd", document.DocumentType!.SystemId); Assert.Equal("r", document.DocumentType.Name);
            Assert.Equal(new[] { "urn:dtd", "urn:parameter", "urn:general" }, resolver.Opened);
            Assert.All(resolver.Streams, stream => Assert.Equal(1, stream.Disposals)); Assert.Equal(0, primary.Disposals);
            Assert.Single(document.Dtd!.GeneralEntities); Assert.Single(document.Dtd.ParameterEntities);
        }
    }

    [Fact]
    public void DocumentVersionQuotaCallbackFaultAndCancellationKeepPrimaryOutcome()
    {
        const string xml = "<?xml version='1.10'?><r/>";
        using var limited = new XmlParserCore(new() { Limits = new() { TokenCharacters = 18 } });
        var sink = new SyntaxTests.Sink();
        var refusal = limited.Append(xml, [], true, sink, Cancellation);
        Assert.Equal("XML.ResourceLimit", refusal.Diagnostic!.Code); Assert.Equal(new[] { "document" }, sink.Events);
        using var core = new XmlParserCore(new());
        var primary = new IOException("declaration callback");
        sink = new SyntaxTests.Sink();
        sink.Hook = () => { if (sink.Events[^1].StartsWith("declaration:", StringComparison.Ordinal)) throw primary; };
        Assert.Same(primary, Assert.Throws<IOException>(() => core.Append(xml, [], true, sink, Cancellation)));
        Assert.Equal("declaration:1.10::", sink.Events[^1]);
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, Cancellation));
        using var canceled = new XmlTextDeclarationFramer(new());
        Assert.Equal(TextDeclarationStep.NeedInput, canceled.Append("<?xml version='1.10", [], false, Cancellation).Step);
        Assert.Throws<OperationCanceledException>(() => canceled.Append("' encoding='X'?>", [], true, new(true)));
        Assert.Throws<InvalidOperationException>(() => canceled.Append([], [], true, Cancellation));
    }

    private sealed class Resolver(string version, int size) : XmlResourceResolver
    {
        public readonly List<string> Opened = [];
        public readonly List<ProductionTests.BoundedStream> Streams = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); string uri = request.SystemId ?? throw new InvalidOperationException("Missing system ID.");
            Opened.Add(uri);
            string body = uri switch { "urn:dtd" => "<!ENTITY % p SYSTEM 'urn:parameter'>%p;", "urn:parameter" => "<!ATTLIST r a CDATA 'kept'><!ENTITY e SYSTEM 'urn:general'>", "urn:general" => "<b>😀owned</b>", _ => throw new InvalidOperationException(uri) };
            var stream = new ProductionTests.BoundedStream(Encoding.UTF8.GetBytes($"<?xml version='{version}' encoding='UTF-8'?>" + body), size);
            Streams.Add(stream); return new(stream, new Uri(uri));
        }
    }
}

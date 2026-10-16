using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.ConformanceGrammar;

public sealed class StandaloneOriginTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Prefix = "<?xml version='1.0' standalone='yes'?><r>";

    // These independent expectations distinguish XML S from the character value
    // produced by CDATA/direct references. Internal parsed replacement is parsed
    // as content again, even if a numeric reference supplied its stored scalar.
    [Theory]
    [InlineData(" ", false)]
    [InlineData("\t", false)]
    [InlineData("\r\n", false)]
    [InlineData("&#32;", true)]
    [InlineData("&#x9;", true)]
    [InlineData("&#10;", true)]
    [InlineData("&#13;", true)]
    [InlineData("<![CDATA[ \t\r\n]]>", true)]
    [InlineData("<![CDATA[]]>", true)]
    [InlineData("<?p \t?>", true)]
    [InlineData("<!-- \t-->", true)]
    [InlineData("&literal;", false)]
    [InlineData("&stored;", false)]
    [InlineData("&reference;", true)]
    [InlineData("&cdata;", true)]
    [InlineData("<child> </child>", true)]
    public void EverySplitAndEveryCallbackPausePreservesWhitespaceOrigin(string body, bool accepted)
    {
        var input = Prefix + body + "</r>";
        for (var split = 0; split <= input.Length; split++)
        {
            var baseline = Read(input, split, -1);
            Assert.Equal(accepted ? ScanStep.Completed : ScanStep.Rejected, baseline.Step);
            Assert.Equal(accepted ? null : "XML.Standalone", baseline.Diagnostic?.Code);
            for (var pause = 0; pause < baseline.Sink.Calls; pause++)
            {
                var resumed = Read(input, split, pause);
                Assert.Equal(baseline.Step, resumed.Step);
                Assert.Equal(baseline.Diagnostic, resumed.Diagnostic);
                Assert.Equal(baseline.Sink.Calls, resumed.Sink.Calls);
                Assert.Equal(baseline.Sink.Events, resumed.Sink.Events);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExternalElementContentDeclarationAloneDoesNotEnableValidation(bool external)
    {
        var input = Prefix + " text <child/> text </r>";
        using var core = new XmlParserCore(new(), Context(external));
        var result = core.Append(input, [], true, new Sink(-1), Cancellation);
        Assert.Equal(external ? ScanStep.Rejected : ScanStep.Completed, result.Step);
    }

    [Fact]
    public void DirectReferenceStillChargesExpansionAndCdataChargesPrimaryAndTokenQuotas()
    {
        const string input = Prefix + "&#32;</r>";
        var options = new XmlParseOptions { Limits = new() { ExpandedCharacters = 1, DocumentCharacters = input.Length } };
        using (var core = new XmlParserCore(options, Context()))
        {
            Assert.Equal(ScanStep.Completed, core.Append(input, [], true, new Sink(-1), Cancellation).Step);
        }
        // A second reference exceeds the unchanged expansion ledger.
        using (var core = new XmlParserCore(options with
        {
            Limits = options.Limits with
            {
                DocumentCharacters = null
            }
        }, Context()))
        {
            Assert.Equal("XML.ResourceLimit", core.Append(Prefix + "&#32;&#32;</r>", [], true, new Sink(-1), Cancellation).Diagnostic?.Code);
        }

        const string cdata = Prefix + "<![CDATA[ \t\n]]></r>";
        using (var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                DocumentCharacters = cdata.Length - 1
            }
        }, Context()))
        {
            Assert.Equal("XML.ResourceLimit", core.Append(cdata, [], true, new Sink(-1), Cancellation).Diagnostic?.Code);
        }
    }

    [Fact]
    public void CdataWhitespaceRetainsLexicalTokenQuota()
    {
        using var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                TokenCharacters = 50
            }
        }, Context());
        var input = Prefix + "<![CDATA[" + new string(' ', 100) + "]]></r>";
        Assert.Equal("XML.ResourceLimit", core.Append(input, [], true, new Sink(-1), Cancellation).Diagnostic?.Code);
    }

    private static XmlEntityContext Context(bool external = true) => new(new(
        [new("r", new(DtdParticleKind.Sequence, null, DtdOccurrence.ZeroOrMore,
            [new(DtdParticleKind.Name, "child", DtdOccurrence.Once, [])]), external, default)], [],
        [Entity("literal", " "), Entity("stored", "\n"), Entity("reference", "&#32;"), Entity("cdata", "<![CDATA[ ]]>")], [], []));
    private static DtdEntityDeclaration Entity(string name, string replacement) => new(name, false, replacement, null, null, null, false, default);

    private static (ScanStep Step, XmlDiagnostic? Diagnostic, Sink Sink) Read(string input, int split, int pause)
    {
        using var core = new XmlParserCore(new(), Context());
        var sink = new Sink(pause);
        var offset = 0;
        var first = true;
        while (true)
        {
            var end = first ? split : input.Length;
            var result = core.Append(input.AsSpan(offset, end - offset), [], !first, sink, Cancellation);
            Assert.InRange(result.Consumed, 0, end - offset);
            offset += result.Consumed;
            if (result.Step is ScanStep.Completed or ScanStep.Rejected)
            {
                return (result.Step, result.Diagnostic, sink);
            }

            if (result.Step == ScanStep.NeedInput)
            {
                Assert.Equal(end, offset);
                first = false;
            }
        }
    }

    private sealed class Sink(int pause) : IXmlEventSink
    {
        internal int Calls;
        internal readonly List<string> Events = [];
        private bool Next(string value)
        {
            Events.Add(value);
            return Calls++ != pause;
        }
        public bool StartDocument(XmlLocation location) => Next("document");
        public bool Declaration(XmlDeclarationView declaration) => Next("declaration");
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) => Next("start:" + name.LocalName.ToString());
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => Next("namespace");
        public bool Attribute(XmlAttributeView attribute) => Next("attribute");
        public bool StartElementContent() => Next("content");
        public bool EndElement(XmlNameView name, XmlLocation location) => Next("end:" + name.LocalName.ToString());
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => Next("text:" + kind);
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => Next("fragment:" + text.ToString() + ":" + ended);
        public bool EndDocument(XmlLocation location) => Next("complete");
    }
}

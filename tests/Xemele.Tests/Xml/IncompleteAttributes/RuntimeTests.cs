using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xunit;
namespace Xemele.Tests.IncompleteAttributes;

public sealed class RuntimeTests
{
    internal const string Prefix = "<!DOCTYPE r [<!ENTITY % p ''>%p;]>";
    internal static XmlParseOptions Options => new() { DtdProcessing = XmlDtdProcessing.Parse };
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("<r a='&x;'/>", "XML.UndeclaredEntity")]
    [InlineData("<!DOCTYPE r []><r a='&x;'/>", "XML.UndeclaredEntity")]
    [InlineData("<!DOCTYPE r [<!ENTITY % p ''>]><r a='&x;'/>", "XML.UndeclaredEntity")]
    [InlineData("<?xml version='1.0' standalone='yes'?>" + Prefix + "<r a='&x;'/>", "XML.UndeclaredEntity")]
    [InlineData(Prefix + "<r a='&x;<'/>", "XML.Syntax")]
    [InlineData(Prefix + "<r a='&x;&#0;'/>", "XML.Syntax")]
    [InlineData(Prefix + "<r a='&x;&a:b;'/>", "XML.Syntax")]
    [InlineData("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e SYSTEM 'urn:e'>]><r a='&x;&e;'/>", "XML.Syntax")]
    [InlineData("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e '&e;'>]><r a='&x;&e;'/>", "XML.EntityCycle")]
    [InlineData(Prefix + "<r xmlns='&x;'/>", "XML.UnresolvedNamespaceValue")]
    [InlineData("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA '&e;'><!ENTITY e 'E'>]><r/>", "DTD.InvalidDefault")]
    public void NegativeControls(string xml, string code)
    {
        Assert.False(XmlDocument.TryParse(xml, out var doc, out var diagnostics, Options, Ct));
        Assert.Null(doc);
        Assert.Equal(code, Assert.Single(diagnostics).Code);
    }
    [Fact]
    public void ExplicitIslandsAndLexicalIdentityAtEveryCharacterSplitAndPause()
    {
        const string xml = Prefix + "<r a='  A &x;&y; B&#x9;' b='ok'/>";
        string[] events = ["document", "doctype", "start", "incomplete:a", "attribute:b:ok", "content", "end", "done"];
        for (var split = 0; split <= xml.Length; split++)
        {
            foreach (var pause in new[] { false, true })
            {
                using var core = new XmlParserCore(Options);
                var sink = new Sink(pause);
                Assert.Equal(ScanStep.NeedInput, Feed(core, xml.AsSpan(0, split), [], false, sink).Step);
                Assert.Equal(ScanStep.Completed, Feed(core, xml.AsSpan(split), [], true, sink).Step);
                Assert.Equal(events, sink.Events);
                var doc = sink.Dom.Document;
                var value = Assert.Single(doc.IncompleteAttributeValues);
                Assert.Empty(doc.IncompleteAttributeDefaults);
                Assert.Equal("  A &x;&y; B&#x9;", value.LexicalValue);
                Assert.Equal(4, value.Parts.Count);
                Assert.Equal("  A ", Assert.IsType<XmlKnownAttributeText>(value.Parts[0]).Text);
                Assert.Equal("x", Assert.IsType<XmlUnknownAttributeReference>(value.Parts[1]).Name);
                Assert.Equal("y", Assert.IsType<XmlUnknownAttributeReference>(value.Parts[2]).Name);
                Assert.Equal(" B\t", Assert.IsType<XmlKnownAttributeText>(value.Parts[3]).Text);
                Assert.Equal(xml.IndexOf("&x;", StringComparison.Ordinal) + 1, value.Parts[1].Location.LinePosition);
                Assert.Equal(2, doc.RootElement.Attributes.Count);
                Assert.Null(value.Default);
                Assert.False(value.IsTokenized);
                Assert.Equal("ok", doc.RootElement.GetAttribute("b"));
                Assert.Throws<InvalidOperationException>(() => value.Attribute.Value);
                Assert.Throws<InvalidOperationException>(() => doc.RootElement.GetAttribute("a"));
            }
        }
    }
    [Fact]
    public void DefaultsAreAnnouncedUnusedAndOverriddenWithoutRechargingApplication()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e 'K&x;'>" +
            "<!ATTLIST unused a NMTOKENS ' A &e; Z '><!ATTLIST r a CDATA '&x;' b CDATA '&x;'>]><r b='complete'/>";
        var doc = XmlDocument.Parse(xml, Options, Ct);
        Assert.Equal(3, doc.IncompleteAttributeDefaults.Count);
        var applied = Assert.Single(doc.IncompleteAttributeValues);
        Assert.Same(doc.IncompleteAttributeDefaults[1], applied.Default);
        Assert.Same(applied.Default!.Parts, applied.Parts);
        Assert.Equal("complete", doc.RootElement.GetAttribute("b"));
        Assert.True(doc.IncompleteAttributeDefaults[0].IsTokenized);
        Assert.Equal("&x;", applied.LexicalValue);
        using var core = new XmlParserCore(Options);
        var sink = new Sink(true);
        Assert.Equal(ScanStep.Completed, Feed(core, xml, [], true, sink).Step);
        Assert.Equal(new[] { "document", "doctype", "default:0", "default:1", "default:2", "start", "attribute:b:complete", "incomplete:a", "content", "end", "done" }, sink.Events);
    }
    [Fact]
    public void PhysicalCrLfLiteralAndNestedDeclarationMappings()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e 'K&#38;x;'>]><r a=' A\r\n&e; Z '/>";
        var doc = XmlDocument.Parse(xml, Options, Ct);
        var value = Assert.Single(doc.IncompleteAttributeValues);
        Assert.Equal(" A\r\n&e; Z ", value.LexicalValue);
        var gap = Assert.Single(value.Parts.OfType<XmlUnknownAttributeReference>());
        Assert.Equal(xml.IndexOf("&#38;", StringComparison.Ordinal) + 1, gap.Location.LinePosition);
        Assert.Null(gap.Location.ByteOffset);
        Assert.Equal("&x;", gap.LexicalReference);
    }
    internal static ScanResult Feed(XmlParserCore core, ReadOnlySpan<char> input, ReadOnlySpan<long> maps, bool final, Sink sink)
    {
        var offset = 0;
        ScanResult result;
        do
        {
            result = core.Append(input[offset..], maps.IsEmpty ? [] : maps[offset..], final, sink, Ct);
            offset += result.Consumed;
        }
        while (result.Step == ScanStep.Paused);
        Assert.Equal(input.Length, offset);
        return result;
    }
    internal sealed class Sink(bool pause) : IXmlEventSink, IXmlDtdSink, IXmlIncompleteAttributeSink
    {
        internal readonly DomSink Dom = new(Options);
        internal readonly List<string> Events = [];
        internal string? ThrowOn, CancelOn;
        internal Exception? Fault;
        internal CancellationTokenSource? Cancellation;
        private bool Next(string value, bool result)
        {
            Assert.True(result);
            Events.Add(value);
            if (value == ThrowOn)
            {
                throw Fault!;
            }

            if (value == CancelOn)
            {
                Cancellation!.Cancel();
            }

            return !pause;
        }
        public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView v) => Next("default:" + v.DeclarationSequence, Dom.IncompleteAttributeDefault(v));
        public bool IncompleteAttribute(XmlIncompleteAttributeView v) => Next("incomplete:" + v.Name.LocalName.ToString(), Dom.IncompleteAttribute(v));
        public bool DocumentType(XmlDocumentType t, Xemele.Parsing.Dtd.DtdModel m, XmlLocation l) => Next("doctype", Dom.DocumentType(t, m, l));
        public bool StartDocument(XmlLocation l) => Next("document", Dom.StartDocument(l));
        public bool Declaration(XmlDeclarationView v) => Next("declaration", Dom.Declaration(v));
        public bool StartElement(XmlNameView v, bool e, XmlLocation l) => Next("start", Dom.StartElement(v, e, l));
        public bool NamespaceDeclaration(XmlNamespaceView v) => Next("namespace", Dom.NamespaceDeclaration(v));
        public bool Attribute(XmlAttributeView v) => Next("attribute:" + v.Name.LocalName.ToString() + ":" + v.Value.ToString(), Dom.Attribute(v));
        public bool StartElementContent() => Next("content", Dom.StartElementContent());
        public bool EndElement(XmlNameView v, XmlLocation l) => Next("end", Dom.EndElement(v, l));
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> t, XmlLocation l) => Next("text", Dom.StartText(k, t, l));
        public bool TextFragment(ReadOnlySpan<char> t, bool e, XmlLocation l) => Next("fragment", Dom.TextFragment(t, e, l));
        public bool EndDocument(XmlLocation l) => Next("done", Dom.EndDocument(l));
    }
}

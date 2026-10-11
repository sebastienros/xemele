using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.AttributeMetadata;

public sealed class AttributeMetadataTests(ITestOutputHelper output)
{
    private static XmlParseOptions Options => new() { DtdProcessing = XmlDtdProcessing.Parse };
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ExistingConstructorsDefaultToAuthoredWithoutInferringFromLocationOrSequence()
    {
        var name = new XmlNameView("a", "", "");
        Assert.False(new XmlAttributeView(name, "same", default).IsDefaulted);
        Assert.True(new XmlAttributeView(name, "same", default, true).IsDefaulted);
        Assert.False(new XmlIncompleteAttributeView(name, "&x;", false, default, [], 0).IsDefaulted);
        Assert.True(new XmlIncompleteAttributeView(name, "&x;", false, default, [], null, true).IsDefaulted);
    }

    [Fact]
    public void EqualAuthoredOverrideAndInjectedNamespacePreserveOrderLocationsAndProvenanceAtEverySplit()
    {
        const string xml = "<!DOCTYPE r [<!ATTLIST item a NMTOKENS ' A  B ' p:b CDATA 'v' xmlns:p CDATA 'urn:p'>]>" +
            "<r><item a='A B'/><item/></r>";
        var firstAt = At(xml.IndexOf("<item a", StringComparison.Ordinal));
        var authoredAt = At(xml.IndexOf("a='A B'", StringComparison.Ordinal));
        var secondAt = At(xml.IndexOf("<item/>", StringComparison.Ordinal));
        var expected = new[]
        {
            new Attribute("a", "", "", "A B", false, false, authoredAt, null, "", []),
            new Attribute("b", "p", "urn:p", "v", true, false, firstAt, null, "", []),
            new Attribute("a", "", "", "A B", true, false, secondAt, null, "", []),
            new Attribute("b", "p", "urn:p", "v", true, false, secondAt, null, "", [])
        };
        string[] events = ["document", "doctype", "start:r", "content", "start:item", "namespace:p:urn:p",
            "attribute:a", "attribute:b", "content", "end:item", "start:item", "namespace:p:urn:p",
            "attribute:a", "attribute:b", "content", "end:item", "end:r", "done"];
        for (int split = 0; split <= xml.Length; split++)
        foreach (bool pause in new[] { false, true })
        {
            var sink = Parse(xml, split, pause);
            Assert.Equal(events, sink.Events);
            Assert.Equal(expected.Length, sink.Attributes.Count);
            for (int i = 0; i < expected.Length; i++) AssertAttribute(expected[i], sink.Attributes[i]);
            Assert.Equal(new[] { firstAt, secondAt }, sink.NamespaceLocations);
        }
    }

    [Fact]
    public void IncompleteAuthoredOverrideAndDefaultKeepExactPartsAndSequencesWithoutReplayAtEverySplit()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST item p:a NMTOKENS ' A &x; B&#9;' xmlns:p CDATA 'urn:p'>]>" +
            "<r><item p:a=' A &x; B&#9;'/><item/><item p:a='complete'/></r>";
        var sinkWhole = Parse(xml, xml.Length, false);
        Assert.Equal(1, sinkWhole.Defaults);
        Assert.Equal(3, sinkWhole.Attributes.Count);
        var authored = sinkWhole.Attributes[0]; var applied = sinkWhole.Attributes[1]; var complete = sinkWhole.Attributes[2];
        Assert.False(authored.Defaulted); Assert.True(authored.Incomplete); Assert.Null(authored.Sequence);
        Assert.True(applied.Defaulted); Assert.True(applied.Incomplete); Assert.Equal(0, applied.Sequence);
        Assert.False(complete.Defaulted); Assert.False(complete.Incomplete); Assert.Equal("complete", complete.Value);
        Assert.Equal(" A &x; B&#9;", authored.Lexical); Assert.Equal(authored.Lexical, applied.Lexical);
        Assert.Equal("", authored.Value); Assert.Equal("", applied.Value);
        Assert.Equal(At(xml.IndexOf(" A &x;", xml.IndexOf("<r>", StringComparison.Ordinal), StringComparison.Ordinal)), authored.Location);
        Assert.Equal(At(xml.IndexOf(" A &x;", StringComparison.Ordinal)), applied.Location);
        foreach (var attribute in sinkWhole.Attributes)
        { Assert.Equal("a", attribute.Local); Assert.Equal("p", attribute.Prefix); Assert.Equal("urn:p", attribute.Uri); }
        foreach (var attribute in new[] { authored, applied })
        {
            Assert.True(attribute.Tokenized);
            Assert.Equal(3, attribute.Parts.Length);
            Assert.Equal(" A ", Assert.IsType<XmlKnownAttributeText>(attribute.Parts[0]).Text);
            var unknown = Assert.IsType<XmlUnknownAttributeReference>(attribute.Parts[1]);
            Assert.Equal("x", unknown.Name); Assert.Equal("&x;", unknown.LexicalReference);
            Assert.Equal(attribute.Location.LinePosition + 3, unknown.Location.LinePosition);
            Assert.Equal(" B\t", Assert.IsType<XmlKnownAttributeText>(attribute.Parts[2]).Text);
        }
        Assert.Equal(sinkWhole.DefaultParts, applied.Parts);
        for (int split = 0; split <= xml.Length; split++)
        foreach (bool pause in new[] { false, true })
        {
            var sink = Parse(xml, split, pause);
            Assert.Equal(sinkWhole.Events, sink.Events); Assert.Equal(1, sink.Defaults);
            Assert.Equal(sinkWhole.Attributes.Count, sink.Attributes.Count);
            for (int i = 0; i < sink.Attributes.Count; i++) AssertAttribute(sinkWhole.Attributes[i], sink.Attributes[i]);
        }
    }

    [Fact]
    public void AttributeSlotsDoNotRetainDefaultFlagAcrossSiblingTags()
    {
        const string xml = "<!DOCTYPE r [<!ATTLIST item a CDATA 'v'>]><r><item/><item a='v'/><other a='v'/><item/></r>";
        var sink = Parse(xml, xml.Length, true);
        Assert.Equal(new[] { true, false, false, true }, sink.Attributes.Select(a => a.Defaulted));
    }

    [Fact]
    public void ByteMappedLocationsSurviveCompleteAndIncompleteDefaultDelivery()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA 'v' b CDATA '&x;'>]><r c='v'/>";
        long[] offsets = Enumerable.Range(0, xml.Length).Select(i => (long)i * 2).ToArray();
        using var core = new XmlParserCore(Options); var sink = new Sink(true);
        Feed(core, xml, offsets, true, sink);
        Assert.Equal(3, sink.Attributes.Count);
        Assert.False(sink.Attributes[0].Defaulted); Assert.True(sink.Attributes[1].Defaulted); Assert.True(sink.Attributes[2].Defaulted);
        Assert.Equal(2L * xml.IndexOf("c='v'", StringComparison.Ordinal), sink.Attributes[0].Location.ByteOffset);
        Assert.Equal(2L * xml.IndexOf("<r c", StringComparison.Ordinal), sink.Attributes[1].Location.ByteOffset);
        Assert.Equal(2L * xml.IndexOf("&x;", StringComparison.Ordinal), sink.Attributes[2].Location.ByteOffset);
    }

    [Fact]
    public void WarmBorrowedCompleteAndIncompleteEventsAllocateZeroWithPositiveControl()
    {
        Measure("complete authored", "<r>", "<item a='v'/>", false);
        Measure("incomplete default", "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST item a CDATA ' A &x; B '>]><r>", "<item/>", true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var owned = new byte[64];
        long positive = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(owned);
        Assert.True(positive >= 64); output.WriteLine($"Positive allocation control: {positive} B");
    }

    private void Measure(string label, string prefix, string item, bool incomplete)
    {
        using var core = new XmlParserCore(incomplete ? Options : new()); var sink = new CounterSink(incomplete);
        Assert.Equal(ScanStep.NeedInput, core.Append(prefix, [], false, sink, Ct).Step);
        for (int i = 0; i < 128; i++) AppendItem(core, sink, item);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) AppendItem(core, sink, item);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, bytes); Assert.Equal(1152, sink.Attributes);
        Assert.Equal(incomplete ? 1 : 0, sink.Defaults);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, Ct).Step);
        output.WriteLine($"{label}: 1024 operations, 128 warmups, {bytes} B total; setup and ownership excluded");
    }

    private static void AppendItem(XmlParserCore core, CounterSink sink, string item)
    {
        var result = core.Append(item, [], false, sink, default);
        if (result.Step != ScanStep.NeedInput || result.Consumed != item.Length || result.Diagnostic is not null)
            throw new InvalidOperationException("Borrowed append failed.");
    }

    private static XmlLocation At(int offset) => new(null, 1, offset + 1, null);
    private static Sink Parse(string xml, int split, bool pause)
    {
        using var core = new XmlParserCore(Options); var sink = new Sink(pause);
        Assert.Equal(ScanStep.NeedInput, Feed(core, xml.AsSpan(0, split), [], false, sink).Step);
        Assert.Equal(ScanStep.Completed, Feed(core, xml.AsSpan(split), [], true, sink).Step);
        return sink;
    }
    private static ScanResult Feed(XmlParserCore core, ReadOnlySpan<char> input, ReadOnlySpan<long> maps, bool final, Sink sink)
    {
        int offset = 0, calls = 0; ScanResult result;
        do
        {
            result = core.Append(input[offset..], maps.IsEmpty ? [] : maps[offset..], final, sink, Ct);
            Assert.InRange(result.Consumed, 0, input.Length - offset); offset += result.Consumed;
            Assert.True(++calls < input.Length * 10 + 100);
        } while (result.Step == ScanStep.Paused);
        Assert.Equal(input.Length, offset); return result;
    }
    private static void AssertAttribute(Attribute expected, Attribute actual)
    {
        Assert.Equal(expected with { Parts = actual.Parts }, actual);
        Assert.Equal(expected.Parts, actual.Parts);
    }
    private sealed record Attribute(string Local, string Prefix, string Uri, string Value, bool Defaulted, bool Incomplete,
        XmlLocation Location, int? Sequence, string Lexical, XmlAttributeValuePart[] Parts, bool Tokenized = false);
    private sealed class Sink(bool pause) : IXmlEventSink, IXmlIncompleteAttributeSink, IXmlDtdSink
    {
        internal readonly List<string> Events = [];
        internal readonly List<Attribute> Attributes = [];
        internal readonly List<XmlLocation> NamespaceLocations = [];
        internal XmlAttributeValuePart[] DefaultParts = [];
        internal int Defaults;
        private bool Event(string name) { Events.Add(name); return !pause; }
        public bool Attribute(XmlAttributeView v)
        {
            Attributes.Add(new(v.Name.LocalName.ToString(), v.Name.Prefix.ToString(), v.Name.NamespaceUri.ToString(), v.Value.ToString(),
                v.IsDefaulted, false, v.Location, null, "", [])); return Event("attribute:" + v.Name.LocalName.ToString());
        }
        public bool IncompleteAttribute(XmlIncompleteAttributeView v)
        {
            Attributes.Add(new(v.Name.LocalName.ToString(), v.Name.Prefix.ToString(), v.Name.NamespaceUri.ToString(), "",
                v.IsDefaulted, true, v.ValueLocation, v.DefaultDeclarationSequence, v.LexicalValue.ToString(), v.Parts.ToArray(), v.IsTokenized));
            return Event("incomplete:" + v.Name.LocalName.ToString());
        }
        public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView v)
        { Defaults++; DefaultParts = v.Parts.ToArray(); return Event("default:" + v.DeclarationSequence); }
        public bool DocumentType(XmlDocumentType t, Xemele.Parsing.Dtd.DtdModel m, XmlLocation l) => Event("doctype");
        public bool StartDocument(XmlLocation l) => Event("document");
        public bool Declaration(XmlDeclarationView v) => Event("declaration");
        public bool StartElement(XmlNameView v, bool empty, XmlLocation l) => Event("start:" + v.LocalName.ToString());
        public bool NamespaceDeclaration(XmlNamespaceView v)
        { NamespaceLocations.Add(v.Location); return Event("namespace:" + v.Prefix.ToString() + ":" + v.NamespaceUri.ToString()); }
        public bool StartElementContent() => Event("content");
        public bool EndElement(XmlNameView v, XmlLocation l) => Event("end:" + v.LocalName.ToString());
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> t, XmlLocation l) => Event("text");
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) => Event("fragment");
        public bool EndDocument(XmlLocation l) => Event("done");
    }
    private sealed class CounterSink(bool incomplete) : IXmlEventSink, IXmlIncompleteAttributeSink, IXmlDtdSink
    {
        internal int Attributes, Defaults;
        public bool Attribute(XmlAttributeView v)
        {
            if (incomplete || v.IsDefaulted || !v.Name.LocalName.SequenceEqual("a") || !v.Value.SequenceEqual("v"))
                throw new InvalidOperationException("Complete borrowed metadata oracle.");
            Attributes++; return true;
        }
        public bool IncompleteAttribute(XmlIncompleteAttributeView v)
        {
            if (!incomplete || !v.IsDefaulted || v.DefaultDeclarationSequence != 0 || !v.Name.LocalName.SequenceEqual("a") ||
                !v.LexicalValue.SequenceEqual(" A &x; B ") || v.IsTokenized || v.Parts.Length != 3 ||
                v.Parts[0] is not XmlKnownAttributeText { Text: " A " } ||
                v.Parts[1] is not XmlUnknownAttributeReference { Name: "x", LexicalReference: "&x;" } ||
                v.Parts[2] is not XmlKnownAttributeText { Text: " B " })
                throw new InvalidOperationException("Incomplete borrowed metadata oracle.");
            Attributes++; return true;
        }
        public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView v) { Defaults++; return true; }
        public bool DocumentType(XmlDocumentType t, Xemele.Parsing.Dtd.DtdModel m, XmlLocation l) => true;
        public bool StartDocument(XmlLocation l) => true;
        public bool Declaration(XmlDeclarationView v) => true;
        public bool StartElement(XmlNameView v, bool empty, XmlLocation l) => true;
        public bool NamespaceDeclaration(XmlNamespaceView v) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView v, XmlLocation l) => true;
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> t, XmlLocation l) => true;
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) => true;
        public bool EndDocument(XmlLocation l) => true;
    }
}

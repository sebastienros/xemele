using System.Text;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.Syntax;

public sealed class SyntaxTests
{
    internal class Sink : IXmlEventSink
    {
        internal readonly List<string> Events = [];
        internal readonly List<XmlLocation> Locations = [];
        internal readonly List<(string Text, XmlLocation Location)> Fragments = [];
        internal readonly List<string> Tokens = [];
        internal readonly StringBuilder Text = new();
        internal bool Pause;
        internal int CallbackCount;
        internal Action? Hook;
        private bool Record(string e, XmlLocation? location = null)
        {
            Events.Add(e); if (location is { } l) Locations.Add(l);
            CallbackCount++; Hook?.Invoke(); return !Pause;
        }
        public bool StartDocument(XmlLocation location) => Record("document", location);
        public bool Declaration(XmlDeclarationView d) => Record($"declaration:{d.Version}:{d.EncodingName}:{d.Standalone}", d.Location);
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) => Record($"start:{n.Prefix}:{n.LocalName}:{n.NamespaceUri}:{empty}", l);
        public bool NamespaceDeclaration(XmlNamespaceView n) => Record($"namespace:{n.Prefix}:{n.NamespaceUri}", n.Location);
        public bool Attribute(XmlAttributeView a) => Record($"attribute:{a.Name.Prefix}:{a.Name.LocalName}:{a.Name.NamespaceUri}:{a.Value}", a.Location);
        public bool StartElementContent() => Record("content");
        public bool EndElement(XmlNameView n, XmlLocation l) => Record($"end:{n.Prefix}:{n.LocalName}:{n.NamespaceUri}", l);
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation l)
        { Text.Clear(); return Record($"text:{kind}:{target}", l); }
        public bool TextFragment(ReadOnlySpan<char> text, bool end, XmlLocation l)
        {
            Fragments.Add((new(text), l)); Text.Append(text);
            if (end) Tokens.Add(Text.ToString());
            return Record($"fragment:{text}:{end}", l);
        }
        public bool EndDocument(XmlLocation l) => Record("end-document", l);
    }
    private static (ScanResult Result, Sink Sink) Parse(string xml, int split = -1, bool pause = false, XmlResourceLimits? limits = null)
    {
        using var core = new XmlParserCore(new() { Limits = limits ?? new() }); var sink = new Sink { Pause = pause };
        int first = split < 0 ? xml.Length : split;
        ScanResult r = default;
        foreach ((int start, int length, bool final) in new[] { (0, first, split < 0), (first, xml.Length - first, true) })
        {
            if (split < 0 && start == first) break;
            int consumed = 0, iterations = 0;
            do
            {
                r = core.Append(xml.AsSpan(start + consumed, length - consumed), [], final, sink, TestContext.Current.CancellationToken);
                Assert.InRange(r.Consumed, 0, length - consumed); consumed += r.Consumed;
                Assert.True(++iterations < xml.Length * 10 + 100);
            } while (r.Step == ScanStep.Paused);
            if (r.Step is ScanStep.Completed or ScanStep.Rejected) break;
            Assert.Equal(length, consumed);
        }
        return (r, sink);
    }
    [Fact]
    public void AllSplitsAndAllCallbacksPreserveGrammarEventsAndLocations()
    {
        const string xml = "<?xml version='1.0' encoding='UTF-8' standalone='yes'?>\r\n<!--hi--><?p x?><r p:a='&#9; &amp;😀' xmlns:p='u' xmlns='d'>A\r\n😀&#x1F600;&lt;<![CDATA[x]]]]>y<p:c xmlns='e'/></r> \n";
        var whole = Parse(xml); Assert.Equal(ScanStep.Completed, whole.Result.Step);
        Assert.Contains("attribute:p:a:u:\t &😀", whole.Sink.Events);
        Assert.Contains("start::r:d:False", whole.Sink.Events);
        Assert.Contains("start:p:c:u:True", whole.Sink.Events);
        Assert.Contains("x]]", whole.Sink.Tokens);
        for (int split = 0; split <= xml.Length; split++)
        {
            foreach (bool pause in new[] { false, true })
            {
                var result = Parse(xml, split, pause);
                Assert.Equal(ScanStep.Completed, result.Result.Step);
                Assert.Equal(whole.Sink.Events, result.Sink.Events);
                Assert.Equal(whole.Sink.Locations, result.Sink.Locations);
            }
        }
    }
    [Theory]
    [InlineData("", "XML.Syntax")]
    [InlineData("<r>", "XML.Syntax")]
    [InlineData("<r/><s/>", "XML.Syntax")]
    [InlineData("x<r/>", "XML.Syntax")]
    [InlineData("<r/>x", "XML.Syntax")]
    [InlineData("<r></s>", "XML.Syntax")]
    [InlineData("<1/>", "XML.Syntax")]
    [InlineData("<:r/>", "XML.Syntax")]
    [InlineData("<r:/>", "XML.Syntax")]
    [InlineData("<a:b:c/>", "XML.Syntax")]
    [InlineData("<r a='x'a='y'/>", "XML.Syntax")]
    [InlineData("<r a='<'/>", "XML.Syntax")]
    [InlineData("<r a=x/>", "XML.Syntax")]
    [InlineData("<r a='x' a='y'/>", "XML.Namespace")]
    [InlineData("<p:r/>", "XML.Namespace")]
    [InlineData("<r xmlns:p='u' xmlns:q='u' p:a='' q:a=''/>", "XML.Namespace")]
    [InlineData("<r xmlns:p=''/>", "XML.Namespace")]
    [InlineData("<r xmlns:xml='u'/>", "XML.Namespace")]
    [InlineData("<r xmlns:p='http://www.w3.org/XML/1998/namespace'/>", "XML.Namespace")]
    [InlineData("<r xmlns='http://www.w3.org/2000/xmlns/'/>", "XML.Namespace")]
    [InlineData("<xmlns:r/>", "XML.Namespace")]
    [InlineData("<r xmlns='u' xmlns='v'/>", "XML.Namespace")]
    [InlineData("<!DOCTYPE r><r/>", "XML.DtdProhibited")]
    [InlineData("<!DOCTYPE r SYSTEM 'file:///never'><r/>", "XML.DtdProhibited")]
    [InlineData("<r>&unknown;</r>", "XML.UndeclaredEntity")]
    [InlineData("<r>&#0;</r>", "XML.Syntax")]
    [InlineData("<r>&#xD800;</r>", "XML.Syntax")]
    [InlineData("<r>&#x110000;</r>", "XML.Syntax")]
    [InlineData("<r>&#999999999999999;</r>", "XML.Syntax")]
    [InlineData("<r>&#X20;</r>", "XML.Syntax")]
    [InlineData("<r>&#x;</r>", "XML.Syntax")]
    [InlineData("<r>&#-1;</r>", "XML.Syntax")]
    [InlineData("<r>&amp</r>", "XML.Syntax")]
    [InlineData("<r>]]></r>", "XML.Syntax")]
    [InlineData("<!--a--b--><r/>", "XML.Syntax")]
    [InlineData("<!--a---><r/>", "XML.Syntax")]
    [InlineData("<![CDATA[x]]><r/>", "XML.Syntax")]
    [InlineData("<?XML x?><r/>", "XML.Syntax")]
    [InlineData("<r><?p?x?></r>", "XML.Syntax")]
    [InlineData("<?p??><r/>", "XML.Syntax")]
    [InlineData(" <?xml version='1.0'?><r/>", "XML.Syntax")]
    [InlineData("<?xml encoding='UTF-8' version='1.0'?><r/>", "XML.Syntax")]
    [InlineData("<?xml version='1.1'?><r/>", "XML.Syntax")]
    [InlineData("<?xml version='1.0' standalone='yes' encoding='UTF-8'?><r/>", "XML.Syntax")]
    [InlineData("<?xml version='1.&#48;'?><r/>", "XML.Syntax")]
    [InlineData("<r>\u0001</r>", "XML.Syntax")]
    public void InvalidSyntaxRejectsAtEverySplitWithoutReplayingCallbacks(string xml, string code)
    {
        for (int split = 0; split <= xml.Length; split++)
        {
            var r = Parse(xml, split, true);
            Assert.Equal(ScanStep.Rejected, r.Result.Step);
            Assert.Equal(code, r.Result.Diagnostic!.Code);
            Assert.DoesNotContain("end-document", r.Sink.Events);
        }
    }
    [Fact]
    public void UnicodeAndNamespaceScopeAreStrict()
    {
        foreach (string invalid in new[] { "<r>\uD800</r>", "<r>\uDC00</r>", "<r>\uD800x</r>", "<\uDBFF\uDFFF/>" })
            for (int i = 0; i <= invalid.Length; i++) Assert.Equal(ScanStep.Rejected, Parse(invalid, i).Result.Step);
        var r = Parse("<r xmlns='u' xmlns:p='v' a='a' xml:lang='en'><c xmlns='' p:a='b'/><p:c/></r>");
        Assert.Equal(ScanStep.Completed, r.Result.Step);
        Assert.Contains("attribute::a::a", r.Sink.Events);
        Assert.Contains("start::c::True", r.Sink.Events);
        Assert.Contains("start:p:c:v:True", r.Sink.Events);
        Assert.Contains("attribute:xml:lang:http://www.w3.org/XML/1998/namespace:en", r.Sink.Events);
        Assert.Equal(ScanStep.Completed, Parse("<𐀀:a xmlns:𐀀='u'/>").Result.Step);
    }
    [Fact]
    public void MalformedStartTagsNeverExposePartialElementsOrAttributes()
    {
        foreach (string xml in new[] { "<r good='a' bad='<'/>", "<r p:x='a'/>", "<r a='a' a='b'/>", "<r xmlns:xml='bad'/>" })
        {
            var r = Parse(xml); Assert.Equal(ScanStep.Rejected, r.Result.Step);
            Assert.Equal(new[] { "document" }, r.Sink.Events);
        }
    }
    [Fact]
    public void QuotasAllowExactThresholdAndRejectOneOver()
    {
        Assert.Equal(ScanStep.Completed, Parse("<r/>", limits: new() { DocumentCharacters = 4 }).Result.Step);
        Assert.Equal("XML.ResourceLimit", Parse("<r/>", limits: new() { DocumentCharacters = 3 }).Result.Diagnostic!.Code);
        Assert.Equal(ScanStep.Completed, Parse("<r><c/></r>", limits: new() { Depth = 2 }).Result.Step);
        Assert.Equal("XML.ResourceLimit", Parse("<r><c/></r>", limits: new() { Depth = 1 }).Result.Diagnostic!.Code);
        Assert.Equal(ScanStep.Completed, Parse("<r a='' xmlns='u'/>", limits: new() { AttributesPerElement = 2 }).Result.Step);
        Assert.Equal("XML.ResourceLimit", Parse("<r a='' xmlns='u'/>", limits: new() { AttributesPerElement = 1 }).Result.Diagnostic!.Code);
        Assert.Equal(ScanStep.Completed, Parse("<r>&amp;&#x1F600;</r>", limits: new() { ExpandedCharacters = 2 }).Result.Step);
        Assert.Equal("XML.ResourceLimit", Parse("<r>&amp;&#x1F600;</r>", limits: new() { ExpandedCharacters = 1 }).Result.Diagnostic!.Code);
        Assert.Equal(ScanStep.Completed, Parse("<r><r/></r>", limits: new() { InternedNames = 1 }).Result.Step);
        Assert.Equal("XML.ResourceLimit", Parse("<r><c/></r>", limits: new() { InternedNames = 1 }).Result.Diagnostic!.Code);
        Assert.Equal(ScanStep.Completed, Parse("<r>abcd</r>", limits: new() { TokenCharacters = 4 }).Result.Step);
        Assert.Equal("XML.ResourceLimit", Parse("<r>abcde</r>", limits: new() { TokenCharacters = 4 }).Result.Diagnostic!.Code);
        Assert.Equal(ScanStep.Completed, Parse("<r>\r\n😀</r>", limits: new() { DocumentCharacters = 10 }).Result.Step);
    }
    [Fact]
    public void ByteMappingsCarryAcrossSurrogatesCrLfAndExplicitEof()
    {
        using var core = new XmlParserCore(new()); var sink = new Sink { Pause = true };
        const string xml = "<r>😀\r\nx</r>";
        long[] offsets = [3,4,5,6,6,10,11,12,13,14,15,16];
        int consumed = 0;
        while (consumed < xml.Length)
        {
            var r = core.Append(xml.AsSpan(consumed, 1), offsets.AsSpan(consumed, 1), false, sink, TestContext.Current.CancellationToken);
            consumed += r.Consumed;
        }
        core.CompleteByteSource(17);
        ScanResult end;
        do { end = core.Append([], [], true, sink, TestContext.Current.CancellationToken); } while (end.Step == ScanStep.Paused);
        Assert.Equal(ScanStep.Completed, end.Step);
        Assert.Equal(new XmlLocation(null, 2, 6, 17), sink.Locations[^1]);
        Assert.Contains(sink.Fragments, x => x.Text == "😀" && x.Location.LinePosition == 4 && x.Location.ByteOffset == 6);
        Assert.Contains(sink.Fragments, x => x.Text == "\n" && x.Location.ByteOffset == 10);
        Assert.Contains(sink.Fragments, x => x.Text == "x" && x.Location.LineNumber == 2 && x.Location.LinePosition == 1 && x.Location.ByteOffset == 12);
    }
    [Fact]
    public void MappingMisuseAndTerminalLifecycleAreRejected()
    {
        using var core = new XmlParserCore(new()); var sink = new Sink();
        Assert.Throws<ArgumentException>(() => core.Append("<r/>", new long[] {0}, false, sink, TestContext.Current.CancellationToken));
        using (var decreasing = new XmlParserCore(new()))
            Assert.Throws<ArgumentException>(() => decreasing.Append("<r/>", new long[] {0,2,1,3}, false, new Sink(), TestContext.Current.CancellationToken));
        using (var pair = new XmlParserCore(new()))
            Assert.Throws<ArgumentException>(() => pair.Append("😀", new long[] {0,1}, false, new Sink(), TestContext.Current.CancellationToken));
        core.Append("<r/>", new long[] {0,1,2,3}, false, sink, TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => core.Append(" ", [], false, sink, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.CompleteByteSource(2));
        core.CompleteByteSource(4);
        Assert.Throws<InvalidOperationException>(() => core.CompleteByteSource(4));
        Assert.Equal(ScanStep.Completed, core.Append([], [], true, sink, TestContext.Current.CancellationToken).Step);
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, TestContext.Current.CancellationToken));
        core.Dispose(); core.Dispose(); Assert.Throws<ObjectDisposedException>(() => core.Append([], [], true, sink, TestContext.Current.CancellationToken));
        using var empty = new XmlParserCore(new()); empty.CompleteByteSource(3);
        var r = empty.Append([], [], true, new Sink(), TestContext.Current.CancellationToken);
        Assert.Equal(ScanStep.Rejected, r.Step); Assert.Equal(3, r.Diagnostic!.Location.ByteOffset);
    }
    [Fact]
    public void ExceptionsCancellationAndReentrancyFaultWithoutConversion()
    {
        var expected = new XmlParseException([new("callback", XmlDiagnosticSeverity.Error, "callback", default)]);
        using var core = new XmlParserCore(new()); var sink = new Sink { Hook = () => throw expected };
        Assert.Same(expected, Assert.Throws<XmlParseException>(() => core.Append("<r/>", [], true, sink, TestContext.Current.CancellationToken)));
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, TestContext.Current.CancellationToken));
        using var reentrant = new XmlParserCore(new()); var reSink = new Sink();
        reSink.Hook = () => reentrant.Append([], [], false, reSink, TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => reentrant.Append("<r/>", [], true, reSink, TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => reentrant.Append([], [], true, reSink, TestContext.Current.CancellationToken));
        using var cts = new CancellationTokenSource(); using var cancelCore = new XmlParserCore(new()); var cancelSink = new Sink { Hook = cts.Cancel };
        var ex = Assert.Throws<OperationCanceledException>(() => cancelCore.Append("<r>abcdef</r>", [], true, cancelSink, cts.Token));
        Assert.Equal(cts.Token, ex.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => cancelCore.Append([], [], true, cancelSink, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void SinkAndFinalIdentityAndEndDocumentPauseAreOnceOnly()
    {
        using var core = new XmlParserCore(new()); var sink = new Sink { Pause = true };
        var r = core.Append("<r/>", [], true, sink, TestContext.Current.CancellationToken); Assert.Equal(0, r.Consumed);
        Assert.Throws<InvalidOperationException>(() => core.Append("<r/>", [], true, new Sink(), TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => core.Append("<r/>", [], false, sink, TestContext.Current.CancellationToken));
        int consumed = 0;
        do { r = core.Append("<r/>".AsSpan(consumed), [], true, sink, TestContext.Current.CancellationToken); consumed += r.Consumed; } while (!sink.Events.Contains("end-document"));
        Assert.Equal(ScanStep.Paused, r.Step); Assert.Equal(4, consumed);
        Assert.Equal(ScanStep.Completed, core.Append([], [], true, sink, TestContext.Current.CancellationToken).Step);
        Assert.Equal(1, sink.Events.Count(x => x == "end-document"));
        using var cancel = new XmlParserCore(new()); var s = new Sink { Hook = null, Pause = true };
        do { r = cancel.Append("<r/>".AsSpan(consumed = s.Events.Count == 0 ? 0 : consumed), [], true, s, TestContext.Current.CancellationToken); consumed += r.Consumed; } while (!s.Events.Contains("end-document"));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => cancel.Append([], [], true, s, cts.Token));
    }
}

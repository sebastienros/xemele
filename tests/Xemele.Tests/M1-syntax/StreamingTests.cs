using System.Reflection;
using System.Text;
using System.Xml;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.Syntax;

public sealed class StreamingTests
{
    internal sealed class CountingSink : IXmlEventSink
    {
        internal long Units, Calls;
        internal bool Continue = true;
        internal Action? Hook = null;
        private bool Count() { Calls++; Hook?.Invoke(); return Continue; }
        public bool StartDocument(XmlLocation l) => Count();
        public bool Declaration(XmlDeclarationView d) => Count();
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) => Count();
        public bool NamespaceDeclaration(XmlNamespaceView n) => Count();
        public bool Attribute(XmlAttributeView a) => Count();
        public bool StartElementContent() => Count();
        public bool EndElement(XmlNameView n, XmlLocation l) => Count();
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l) => Count();
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) { Units += t.Length; return Count(); }
        public bool EndDocument(XmlLocation l) => Count();
    }
    [Fact]
    public void LongTextStreamsBeforeFinalizationAndBorrowsNoSourceOwner()
    {
        using var core = new XmlParserCore(new()); var sink = new CountingSink();
        var cancellation = TestContext.Current.CancellationToken;
        core.Append("<r>", [], false, sink, cancellation);
        char[] chunk = new string('x', 4096).ToCharArray();
        for (int i = 0; i < 128; i++)
        {
            var r = core.Append(chunk, [], false, sink, cancellation);
            Assert.Equal(ScanStep.NeedInput, r.Step); Assert.Equal(chunk.Length, r.Consumed);
            Assert.Equal((i + 1L) * chunk.Length, sink.Units);
            chunk.AsSpan().Fill(i % 2 == 0 ? 'y' : 'x');
        }
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancellation).Step);
        Assert.Equal(128L * 4096, sink.Units);
        Assert.True(((char[])typeof(XmlParserCore).GetField("token", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core)!).Length <= 128);
    }
    [Fact]
    public void WarmRepeatedTagsAndStreamingUnicodeDispatchAllocateZero()
    {
        using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
        var sink = new CountingSink(); var cancellation = TestContext.Current.CancellationToken;
        const string chunk = "<c a='v' xmlns:p='u' p:a='w'/>α😀&amp;\r\n";
        core.Append("<r>", [], false, sink, cancellation);
        for (int i = 0; i < 100; i++) core.Append(chunk, [], false, sink, cancellation);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            var r = core.Append(chunk, [], false, sink, cancellation);
            if (r.Step != ScanStep.NeedInput || r.Consumed != chunk.Length) throw new Exception("Warm grammar rejected.");
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancellation).Step);
    }
    [Fact]
    public void LocationOverflowIsStructuredAndSourceScalarQuotaPrecedesNormalization()
    {
        using var core = new XmlParserCore(new()); var sink = new CountingSink(); var cancellation = TestContext.Current.CancellationToken;
        core.Append("<r>", [], false, sink, cancellation);
        object tracker = typeof(XmlParserCore).GetField("position", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core)!;
        tracker.GetType().GetField("column", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tracker, int.MaxValue);
        var r = core.Append("x", [], false, sink, cancellation);
        Assert.Equal(ScanStep.Rejected, r.Step); Assert.Equal("XML.LocationLimit", r.Diagnostic!.Code);
        using var quota = new XmlParserCore(new() { Limits = new() { DocumentCharacters = 4 } });
        quota.Append("<r>\r", [], false, new CountingSink(), cancellation);
        // A separate sink must not substitute for the first operation's identity.
        Assert.Throws<InvalidOperationException>(() => quota.Append("\n", [], false, new CountingSink(), cancellation));
        using var exact = new XmlParserCore(new() { Limits = new() { DocumentCharacters = 4 } }); var s = new CountingSink();
        exact.Append("<r>\r", [], false, s, cancellation);
        r = exact.Append("\n", [], false, s, cancellation);
        Assert.Equal("XML.ResourceLimit", r.Diagnostic!.Code); Assert.Equal(1, s.Units);
    }
    [Fact]
    public void HeldCdataAndPiFragmentsPointAtTheirOwnOriginalScalars()
    {
        using var core = new XmlParserCore(new()); var sink = new SyntaxTests.Sink();
        const string xml = "<r><![CDATA[]]]]><?p ???x?></r>";
        Assert.Equal(ScanStep.Completed, core.Append(xml, [], true, sink, TestContext.Current.CancellationToken).Step);
        var emitted = sink.Fragments.Where(x => x.Text.Length > 0).ToArray();
        Assert.Equal("]", emitted[0].Text); Assert.Equal(13, emitted[0].Location.LinePosition);
        Assert.Equal("]", emitted[1].Text); Assert.Equal(14, emitted[1].Location.LinePosition);
        Assert.Equal("?", emitted[2].Text); Assert.Equal(22, emitted[2].Location.LinePosition);
        Assert.Equal("?", emitted[3].Text); Assert.Equal(23, emitted[3].Location.LinePosition);
        Assert.Equal("?x", emitted[4].Text); Assert.Equal(24, emitted[4].Location.LinePosition);
    }
    [Fact]
    public void LexicalTokenLimitsCountNormalizedScalarsIncludingDelimiters()
    {
        var cancellation = TestContext.Current.CancellationToken;
        foreach (string lexical in new[] { "<!--x-->", "<![CDATA[x]]>", "<?p x?>", "<c a='v'/>", "<?xml version='1.0'?>" })
        {
            string source = lexical.StartsWith("<?xml", StringComparison.Ordinal) ? lexical + "<r/>" : "<r>" + lexical + "</r>";
            using var exact = new XmlParserCore(new() { Limits = new() { TokenCharacters = lexical.Length } });
            Assert.Equal(ScanStep.Completed, exact.Append(source, [], true, new CountingSink(), cancellation).Step);
            using var over = new XmlParserCore(new() { Limits = new() { TokenCharacters = lexical.Length - 1 } });
            Assert.Equal("XML.ResourceLimit", over.Append(source, [], true, new CountingSink(), cancellation).Diagnostic!.Code);
        }
        const string normalized = "<r a='\r\n😀'/>";
        using var normal = new XmlParserCore(new() { Limits = new() { TokenCharacters = normalized.Length - 2 } });
        Assert.Equal(ScanStep.Completed, normal.Append(normalized, [], true, new CountingSink(), cancellation).Step);
        using var fail = new XmlParserCore(new() { Limits = new() { TokenCharacters = normalized.Length - 3 } });
        Assert.Equal("XML.ResourceLimit", fail.Append(normalized, [], true, new CountingSink(), cancellation).Diagnostic!.Code);
    }
    [Fact]
    public void AcceptedTagDrainsBeforeConsumingSuffixAndLocationsRetainSourceUri()
    {
        var uri = new Uri("file:///source.xml"); using var core = new XmlParserCore(new(), uri);
        var sink = new SyntaxTests.Sink { Pause = true }; var cancellation = TestContext.Current.CancellationToken;
        const string tag = "<r p:a='v' xmlns:p='u'/>";
        Assert.Equal(0, core.Append(tag + " ", [], true, sink, cancellation).Consumed);
        var first = core.Append(tag + " ", [], true, sink, cancellation);
        Assert.Equal(tag.Length, first.Consumed);
        Assert.StartsWith("start:", sink.Events[^1]);
        for (int i = 0; i < 4; i++) Assert.Equal(0, core.Append(" ", [], true, sink, cancellation).Consumed);
        Assert.StartsWith("end:", sink.Events[^1]);
        ScanResult r; int consumed = 0;
        do { r = core.Append(" ".AsSpan(consumed), [], true, sink, cancellation); consumed += r.Consumed; } while (r.Step == ScanStep.Paused);
        Assert.Equal(ScanStep.Completed, r.Step); Assert.Equal(1, consumed);
        Assert.All(sink.Locations, l => Assert.Equal(uri, l.SourceUri));
        Assert.Throws<ArgumentException>(() => new XmlParserCore(new(), new Uri("relative", UriKind.Relative)));
        Assert.Throws<ArgumentNullException>(() => new XmlParserCore(null!));
    }
    [Fact]
    public void CancellationAtEntryAndWhileValidatingMappingsFaultsTheCore()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var core = new XmlParserCore(new()); var sink = new CountingSink();
        Assert.Throws<OperationCanceledException>(() => core.Append("<r/>", new long[] {0,1,2,3}, true, sink, cts.Token));
        Assert.Equal(0, sink.Calls);
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], false, sink, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void PiEmptyCloseRequiresImmediateGreaterThanAndWhitespaceNeighborsStayValid()
    {
        foreach (string xml in new[] { "<?p?><r/>", "<r><?p ?x?></r>" })
            for (int split = 0; split <= xml.Length; split++)
            {
                using var core = new XmlParserCore(new()); var sink = new CountingSink { Continue = false };
                int offset = 0; ScanResult r;
                do { r = core.Append(xml.AsSpan(offset, split - offset), [], false, sink, TestContext.Current.CancellationToken); offset += r.Consumed; } while (r.Step == ScanStep.Paused);
                Assert.Equal(split, offset);
                do { r = core.Append(xml.AsSpan(offset), [], true, sink, TestContext.Current.CancellationToken); offset += r.Consumed; } while (r.Step == ScanStep.Paused);
                Assert.Equal(ScanStep.Completed, r.Step); Assert.Equal(xml.Length, offset);
            }
    }
    [Fact]
    public void LargeMappedPauseInputValidatesLinearWorkAndNeverTrustsMutableTail()
    {
        const int size = 100000;
        string xml = "<r>" + new string('x', size) + "</r>";
        long[] offsets = Enumerable.Range(0, xml.Length).Select(i => (long)i).ToArray();
        using var core = new XmlParserCore(new()); var sink = new CountingSink { Continue = false };
        var cancellation = TestContext.Current.CancellationToken;
        core.CompleteByteSource(xml.Length); int consumed = 0; ScanResult r;
        do
        {
            r = core.Append(xml.AsSpan(consumed), offsets.AsSpan(consumed), true, sink, cancellation);
            consumed += r.Consumed;
        } while (r.Step == ScanStep.Paused);
        Assert.Equal(ScanStep.Completed, r.Step); Assert.Equal(xml.Length, consumed); Assert.Equal(size, sink.Units);
        long checks = (long)typeof(XmlParserCore).GetField("mappingChecks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core)!;
        Assert.InRange(checks, xml.Length, xml.Length + sink.Calls + 1);
        using var changing = new XmlParserCore(new()); var s = new CountingSink { Continue = false };
        long[] map = [0,1,2,3];
        Assert.Equal(0, changing.Append("<r/>", map, false, s, cancellation).Consumed);
        map[2] = -1;
        Assert.Throws<ArgumentException>(() => changing.Append("<r/>", map, false, s, cancellation));
        Assert.Throws<InvalidOperationException>(() => changing.Append([], [], false, s, cancellation));
        using var beyond = new XmlParserCore(new()); beyond.CompleteByteSource(3);
        Assert.Throws<ArgumentException>(() => beyond.Append("<r/>", new long[] {0,1,2,4}, true, new CountingSink(), cancellation));
    }
    [Fact]
    public void InternalAbiMatchesApprovedViewsAndCallbackShape()
    {
        Type[] views = [typeof(XmlNameView), typeof(XmlAttributeView), typeof(XmlNamespaceView), typeof(XmlDeclarationView)];
        Assert.All(views, type => { Assert.True(type.IsByRefLike); Assert.False(type.IsPublic); });
        string[] methods = ["StartDocument", "Declaration", "StartElement", "NamespaceDeclaration", "Attribute", "StartElementContent", "EndElement", "StartText", "TextFragment", "EndDocument"];
        Assert.Equal(methods.Order(), typeof(IXmlEventSink).GetMethods().Select(m => m.Name).Order());
        Assert.All(typeof(IXmlEventSink).GetMethods(), m => Assert.Equal(typeof(bool), m.ReturnType));
        var append = typeof(XmlParserCore).GetMethod("Append")!;
        Assert.Equal(typeof(ScanResult), append.ReturnType);
        Assert.Equal(GenericParameterAttributes.ReferenceTypeConstraint, append.GetGenericArguments()[0].GenericParameterAttributes);
        Assert.Equal(new[] { typeof(IXmlEventSink) }, append.GetGenericArguments()[0].GetGenericParameterConstraints());
        Assert.False(typeof(XmlParserCore).IsPublic);
        Assert.Equal(new[] { "NeedInput", "Paused", "Completed", "Rejected" }, Enum.GetNames<ScanStep>());
    }
    [Fact]
    public void GeneratedSupportedXmlAgreesWithIndependentBclSemanticOracle()
    {
        // Test-only oracle; the runtime never uses System.Xml. The sink remains
        // independent of DOM and decoder and owns copies only in these tests.
        var random = new Random(1771);
        for (int trial = 0; trial < 100; trial++)
        {
            var xml = new StringBuilder("<r xmlns='u' xmlns:p='v'>");
            int children = random.Next(1, 20);
            for (int n = 0; n < children; n++) xml.Append("<p:c a='x&#9;y'>").Append(n).Append("α😀&amp;\r\n<![CDATA[x<y]]><!--ok--><?p data?></p:c>");
            xml.Append("</r>");
            string source = xml.ToString(); var expected = new StringBuilder();
            using (var reader = XmlReader.Create(new StringReader(source), new() { DtdProcessing = DtdProcessing.Prohibit }))
                while (reader.Read()) if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace) expected.Append(reader.Value);
            using var core = new XmlParserCore(new()); var sink = new OracleSink(); int offset = 0;
            while (offset < source.Length)
            {
                int length = Math.Min(random.Next(1, 40), source.Length - offset);
                var r = core.Append(source.AsSpan(offset, length), [], offset + length == source.Length, sink, TestContext.Current.CancellationToken);
                Assert.Equal(length, r.Consumed); Assert.NotEqual(ScanStep.Rejected, r.Step); offset += length;
                if (offset == source.Length) Assert.Equal(ScanStep.Completed, r.Step);
            }
            Assert.Equal(expected.ToString(), sink.Text.ToString()); Assert.Equal(children + 1, sink.Elements);
        }
    }
    private sealed class OracleSink : IXmlEventSink
    {
        internal readonly StringBuilder Text = new(); internal int Elements; private XmlTextKind kind;
        public bool StartDocument(XmlLocation l) => true;
        public bool Declaration(XmlDeclarationView d) => true;
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) { Elements++; return true; }
        public bool NamespaceDeclaration(XmlNamespaceView n) => true;
        public bool Attribute(XmlAttributeView a) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView n, XmlLocation l) => true;
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l) { kind = k; return true; }
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) { if (kind is XmlTextKind.Text or XmlTextKind.CData) Text.Append(t); return true; }
        public bool EndDocument(XmlLocation l) => true;
    }
}

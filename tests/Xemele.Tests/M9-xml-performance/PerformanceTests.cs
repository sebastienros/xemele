using System.Text;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.XmlPerformance;

public sealed class PerformanceTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(4095)] [InlineData(4096)] [InlineData(8193)]
    public void BulkTextPreservesUnicodeNormalizationAcrossDecoderPages(int length)
    {
        string xml = "<r>" + new string('x', length) + "α日本語😀&amp;\r\n<![CDATA[tail]]>\u007f</r>";
        var expected = new System.Xml.XmlDocument { PreserveWhitespace = true };
        using var reader = System.Xml.XmlReader.Create(new StringReader(xml), new System.Xml.XmlReaderSettings { IgnoreWhitespace = false });
        expected.Load(reader);
        var actual = XmlDocument.Parse(Encoding.UTF8.GetBytes(xml), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected.DocumentElement!.InnerText, actual.RootElement.GetTextContent());
        Assert.Equal(actual.RootElement.GetTextContent(), XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken).RootElement.GetTextContent());
    }
    [Theory]
    [InlineData(1)] [InlineData(31)] [InlineData(4096)]
    public void BulkQuotaStopsAtTheFirstExcessScalar(int limit)
    {
        string xml = "<r>" + new string('x', limit) + "y</r>";
        Assert.False(XmlDocument.TryParse(xml, out _, out var diagnostics, new() { Limits = new() { TokenCharacters = limit } }, TestContext.Current.CancellationToken));
        if (limit >= 3) Assert.Equal(limit + 4, diagnostics[0].Location.LinePosition);
        Assert.Equal("XML.ResourceLimit", diagnostics[0].Code);
    }
    [Fact]
    public void PausedBorrowedRunConsumesOnlyItsAcceptedPrefixAndRevalidatesTheTail()
    {
        using var core = new XmlParserCore(new()); var sink = new BulkSink { Capture = true, PauseRun = true };
        char[] input = "<r>abcdef</r>".ToCharArray(); long[] offsets = Enumerable.Range(0, input.Length).Select(x => (long)x).ToArray();
        var cancel = TestContext.Current.CancellationToken;
        var paused = core.Append(input, offsets, false, sink, cancel);
        Assert.Equal(ScanStep.Paused, paused.Step); Assert.Equal(9, paused.Consumed);
        Assert.Equal("abcdef", sink.Captured.ToString());
        input.AsSpan(0, paused.Consumed).Fill('#'); offsets[^1] = -1;
        core.CompleteByteSource(input.Length);
        Assert.Throws<ArgumentException>(() => core.Append(input.AsSpan(paused.Consumed), offsets.AsSpan(paused.Consumed), true, sink, cancel));
        Assert.Equal("abcdef", sink.Captured.ToString());
    }
    [Fact]
    public void PreparedBorrowedTextDispatchAllocatesZero()
    {
        using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
        var sink = new BulkSink(); var cancel = TestContext.Current.CancellationToken; string run = new('x', 4096);
        core.Append("<r>", [], false, sink, cancel);
        for (int i = 0; i < 20; i++) core.Append(run, [], false, sink, cancel);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            var result = core.Append(run, [], false, sink, cancel);
            if (result.Step != ScanStep.NeedInput || result.Consumed != run.Length) throw new InvalidOperationException("Unexpected borrowed dispatch state");
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated); Assert.Equal(1020L * 4096, sink.Units);
        Assert.True(sink.Calls < 1100);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancel).Step);
    }
    [Fact]
    public void PreparedRepeatedTagAndNameDispatchAllocatesZero()
    {
        using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
        var sink = new BulkSink(); var cancel = TestContext.Current.CancellationToken;
        const string tag = "<n a='value'/>";
        core.Append("<r>", [], false, sink, cancel);
        for (int i = 0; i < 20; i++) core.Append(tag, [], false, sink, cancel);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            if (core.Append(tag, [], false, sink, cancel).Consumed != tag.Length) throw new InvalidOperationException("Incomplete tag dispatch");
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancel).Step);
    }
    [Fact]
    public void PageBoundariesKeepAllSlotsAndBoundsStable()
    {
        var arena = new SlotArena<int>(); arena.EnsureCapacity(4097);
        for (int i = 0; i < 5000; i++) arena.Add(i * 7);
        foreach (int i in new[] { 0, 3, 1023, 1024, 2047, 2048, 4095, 4096, 4999 }) Assert.Equal(i * 7, arena[i]);
        Assert.Equal(5000, arena.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = arena[5000]);
    }
    [Theory]
    [InlineData(4294967294L)] [InlineData(4294967295L)] [InlineData(4294967296L)] [InlineData(long.MaxValue)]
    public void CompactCoordinatesAndSparseOverflowRemainLossless(long offset)
    {
        var document = new XmlDocument(); var root = document.CreateElement("r");
        var origin = new Uri("urn:source"); document.RecordSourceOrigin(new(origin, 0, 0, null));
        var expected = new XmlLocation(origin, int.MaxValue, int.MaxValue, offset);
        document.RecordSourceLocation(root, expected); document.MarkValidationSourceComplete();
        Assert.Equal(expected, document.OriginalLocation(root));
        Assert.Equal(new(origin, 0, 0, null), document.OriginalLocation(document.DocumentNode));
    }
    [Fact]
    public void OwnedDomSurvivesInputMutationAndScratchReuse()
    {
        byte[] input = Encoding.UTF8.GetBytes("<r a='v'>α😀&amp;</r>");
        var first = XmlDocument.Parse(input, cancellationToken: TestContext.Current.CancellationToken); input.AsSpan().Fill((byte)'!');
        for (int i = 0; i < 32; i++) _ = XmlDocument.Parse("<r>" + new string('z', 5000) + "</r>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("α😀&", first.RootElement.GetTextContent()); Assert.Equal("v", first.RootElement.GetAttribute("a"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TrustedUtf8AndUtf16TagRunsKeepExactMemberOffsets(bool utf16)
    {
        string xml = "<r>α😀&amp;<n first='ordinary' second='x&amp;z'\n third='v'/></r>";
        Encoding encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        byte[] preamble = utf16 ? encoding.GetPreamble() : [];
        byte[] bytes = [..preamble, ..encoding.GetBytes(xml)];
        var doc = XmlDocument.Parse(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var node = doc.RootElement.Children[1];
        foreach (var attribute in node.Attributes)
        {
            int index = xml.IndexOf(attribute.LocalName + "=", StringComparison.Ordinal);
            int line = xml.AsSpan(0, index).Count('\n') + 1;
            int beginning = xml.LastIndexOf('\n', index) + 1, column = 1;
            foreach (var rune in xml.AsSpan(beginning, index - beginning).EnumerateRunes()) column++;
            Assert.Equal(new XmlLocation(null, line, column, preamble.Length + encoding.GetByteCount(xml.AsSpan(0, index))), doc.OriginalLocation(attribute));
        }
        Assert.Equal("x&z", node.GetAttribute("second"));
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void PredefinedReferenceExpansionQuotaKeepsTheFailingReferenceLocation(int expansions)
    {
        const string xml = "<r>x&amp;&lt;&quot;&apos;</r>";
        Assert.False(XmlDocument.TryParse(xml, out _, out var diagnostics,
            new() { Limits = new() { ExpandedCharacters = expansions } }, TestContext.Current.CancellationToken));
        int[] columns = [10, 14, 20];
        Assert.Equal(columns[expansions - 1], diagnostics[0].Location.LinePosition);
    }
    [Fact]
    public void RepeatedOwnedNamesAndEqualDistinctCallerStringsRespectInternQuota()
    {
        var doc = new XmlDocument(new() { InternedNames = 3 });
        var first = doc.CreateElement(new string("n".AsSpan()), new string("urn:n".AsSpan()), new string("p".AsSpan()));
        var repeated = doc.CreateElement(new string("n".AsSpan()), new string("urn:n".AsSpan()), new string("p".AsSpan()));
        Assert.Same(first.LocalName, repeated.LocalName);
        Assert.Same(first.NamespaceUri, repeated.NamespaceUri);
        Assert.Throws<InvalidOperationException>(() => doc.CreateElement("fourth"));
    }
    [Fact]
    public void CollidingNameCachesPreserveExactNamesAndDistinctNameQuota()
    {
        string[] names = [..Enumerable.Range(0, 32).Select(i => "item" + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture)), "名前α", "名前β", "a", "A"];
        string xml = "<r>" + string.Concat(Enumerable.Range(0, 4).SelectMany(_ => names.Select(n => "<" + n + "/>"))) + "</r>";
        var document = XmlDocument.Parse(xml, new() { Limits = new() { InternedNames = names.Length + 1 } }, TestContext.Current.CancellationToken);
        for (int i = 0; i < names.Length * 4; i++)
        {
            Assert.Equal(names[i % names.Length], document.RootElement.Children[i].LocalName);
            if (i >= names.Length) Assert.Same(document.RootElement.Children[i % names.Length].LocalName, document.RootElement.Children[i].LocalName);
        }
        Assert.False(XmlDocument.TryParse(xml, out _, out var diagnostics, new() { Limits = new() { InternedNames = names.Length } }, TestContext.Current.CancellationToken));
        Assert.Equal("XML.ResourceLimit", diagnostics[0].Code);
    }
    [Fact]
    public void PreparedCollidingNamesDispatchAllocatesZero()
    {
        string[] names = [..Enumerable.Range(0, 32).Select(i => "item" + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture))];
        string tags = string.Concat(names.Select(n => "<" + n + "/>"));
        using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
        var sink = new BulkSink(); var cancel = TestContext.Current.CancellationToken;
        core.Append("<r>", [], false, sink, cancel); sink.ExpectedNames = names;
        for (int i = 0; i < 20; i++) core.Append(tags, [], false, sink, cancel);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) if (core.Append(tags, [], false, sink, cancel).Consumed != tags.Length) throw new InvalidOperationException("Incomplete colliding-name dispatch");
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancel).Step);
    }
    [Fact]
    public void CompletePrimaryTagPreflightKeepsFirstExcessByteAndScalarLocations()
    {
        byte[] bytes = "<r><n a='v'/></r>"u8.ToArray();
        for (int maximum = 1; maximum < bytes.Length; maximum++)
        {
            Assert.False(XmlDocument.TryParse(bytes.AsSpan(), out _, out var diagnostics,
                new() { Limits = new() { DocumentCharacters = maximum } }, TestContext.Current.CancellationToken));
            Assert.Equal("XML.ResourceLimit", diagnostics[0].Code);
            Assert.Equal(maximum + 1, diagnostics[0].Location.LinePosition);
            Assert.Equal(maximum, diagnostics[0].Location.ByteOffset);
        }
        var document = XmlDocument.Parse(bytes.AsSpan(), new() { Limits = new() { DocumentCharacters = bytes.Length } }, TestContext.Current.CancellationToken);
        Assert.Equal(new XmlLocation(null, 1, 4, 3), document.OriginalLocation(document.RootElement.Children[0]));
    }
    private sealed class BulkSink : IXmlBulkTextSink
    {
        internal string[]? ExpectedNames; private int nameIndex;
        internal bool Capture, PauseRun; internal long Units, Calls; internal readonly StringBuilder Captured = new();
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView value) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
        {
            if (ExpectedNames is { } expected && !name.LocalName.SequenceEqual(expected[nameIndex++ % expected.Length])) throw new InvalidOperationException("Incorrect interned name.");
            return true;
        }
        public bool NamespaceDeclaration(XmlNamespaceView value) => true;
        public bool Attribute(XmlAttributeView value) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location)
        {
            Units += text.Length; Calls++; if (Capture) Captured.Append(text);
            if (PauseRun && text.Length >= 2) { PauseRun = false; return false; } return true;
        }
        public bool EndDocument(XmlLocation location) => true;
    }
}

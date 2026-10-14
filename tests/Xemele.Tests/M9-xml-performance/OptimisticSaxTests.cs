using System.Text;
using Xemele.Streaming;
using Xunit;

namespace Xemele.Tests.XmlPerformance;

// The optimistic SAX parser may only deliver events for input the streaming parser accepts, and the events
// (apart from how text is split into callbacks) must be identical. It must decline without delivering anything.
public sealed class OptimisticSaxTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] AcceptedXml =
    [
        "<a/>", "<a></a>", "<a>t</a>", "<a b='1' c=\"2\"/>",
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<a><b>x&amp;y&lt;</b><!--c--><?pi body?><![CDATA[d]]></a>\n",
        "<r xmlns='u' xmlns:p='v'><p:a p:x='1' y='2'><b/></p:a><c xmlns=''/></r>",
        "<a xml:lang='en'><b xml:space='preserve'/></a>",
        "  \n<!-- x -->\n<a>\n  <b>é€😀</b>\n  <c d='é\t\n&quot;'/>\n</a>\n<!-- e -->",
        "<a>&#65;&#x1F600;&#xe9;&#9;</a>", "<a b='&#65;&#10;&#x20;&#x1F600;'/>",
        "\uFEFF<a/>", "\uFEFF<?xml version='1.0'?>\n<a>é</a>",
        "<a xmlns:p='u' xmlns:q='u'><p:x q:x='1'/></a>", "<a  b = '1'\n c\t=\"2\" />", "<a>x\ny\n  é\nz</a>",
        "<a>\r\n<b/>\r\n</a>", "<?xml version='1.0'\r\n?>\r\n<a\r\n  b='x\r\ny\rz'\r\n/>\r\n", "\r\n<!--c\r\nd--><a>é\r\n€\r😀\r\n<?p x\r\ny?><![CDATA[\r\n\r\r\n]]></a>\r\n",
        "\r\r\n\n\r<a/>\r\n\r\n", "<a>\r</a>", "<a/>\r",
        "<r>" + string.Concat(Enumerable.Repeat("\r\n<i k='v\r\nw'>é\r\n<!--\r\n--></i>\r", 700)) + "</r>\r\n",
        "<a><?p?><!----><![CDATA[]]><![CDATA[ x\n é ]]></a>", "<?xml version='1.0'?> <?pi x?> <a/> <!--t--> ",
        "<a xmlns='u'><b xmlns='u'/><c/></a>", "<?xml version='1.0' encoding='utf-8'?><a/>",
    ];

    public static TheoryData<string> Accepted()
    {
        var data = new TheoryData<string>();
        foreach (string xml in AcceptedXml) data.Add(xml);
        return data;
    }

    public static TheoryData<string> Declined() => new()
    {
        "", "<a>]]></a>", "<a b='<'/>", "<a b='1' b='2'/>", "<a:b/>", "<a:b:c/>", "<a>&foo;</a>", "<a>&#0;</a>", "<a>&#xD800;</a>",
        "<a>&#x110000;</a>", "<a>&#;</a>", "<a/><b/>", "<a><b></a>", "<a>&#13;\r\n&#10;</a>", "\uFEFF\uFEFF<a/>",
        "<a>\uFFFF</a>", "<a>\u0001</a>", "<!DOCTYPE a><a/>", "<a xmlns:xmlns='x'/>", "<a b='1'c='2'/>", "<a 1='x'/>",
        "<?xml version='1.0' encoding='ISO-8859-1'?><a/>", "<a xmlns:p='u' xmlns:q='u'><p:x p:x='1' q:x='2'/></a>",
    };

    [Theory, MemberData(nameof(Accepted))]
    public void AcceptedBytesDeliverTheSameEventsAsTheStreamingParser(string xml)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        var fast = new Recorder();
        Assert.True(OptimisticSax.TryParse(data, fast, new XmlParseOptions(), Ct));
        var slow = new Recorder();
        XmlSaxParser.Parse(new MemoryStream(data), slow, cancellationToken: Ct);
        Assert.Equal(slow.Normalized(), fast.Normalized());
    }

    [Theory, MemberData(nameof(Accepted))]
    public void AcceptedCharactersDeliverTheSameEventsAsTheStreamingParser(string xml)
    {
        var fast = new Recorder();
        if (xml.StartsWith('\uFEFF'))
        {
            Assert.False(OptimisticSax.TryParse(xml.AsSpan(), fast, new XmlParseOptions(), Ct));
            Assert.Empty(fast.Events);
            return;
        }
        Assert.True(OptimisticSax.TryParse(xml.AsSpan(), fast, new XmlParseOptions(), Ct));
        Assert.Equal(SlowCharacters(xml, new XmlParseOptions()).Normalized(), fast.Normalized());
    }

    [Theory, MemberData(nameof(Accepted))]
    public void PublicEntryPointsAgreeWithTheStreamingParser(string xml)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        var slow = new Recorder();
        XmlSaxParser.Parse(new MemoryStream(data), slow, cancellationToken: Ct);

        var bytes = new Recorder();
        XmlSaxParser.Parse(data, bytes, cancellationToken: Ct);
        Assert.Equal(slow.Normalized(), bytes.Normalized());

        var diagnostics = new Recorder();
        var result = XmlSaxParser.ParseWithDiagnostics(data, diagnostics, cancellationToken: Ct);
        Assert.True(result.IsWellFormed);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(slow.Normalized(), diagnostics.Normalized());

        if (xml.StartsWith('\uFEFF')) return;
        var chars = new Recorder();
        XmlSaxParser.Parse(xml, chars, cancellationToken: Ct);
        Assert.Equal(SlowCharacters(xml, new XmlParseOptions()).Normalized(), chars.Normalized());
    }

    [Theory, MemberData(nameof(Declined))]
    public void DeclinedInputDeliversNothingAndTheStreamingParserDecides(string xml)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        var fast = new Recorder();
        Assert.False(OptimisticSax.TryParse(data, fast, new XmlParseOptions(), Ct));
        Assert.Empty(fast.Events);

        var slow = new Recorder();
        Exception? slowError = Record.Exception(() => XmlSaxParser.Parse(new MemoryStream(data), slow, cancellationToken: Ct));
        var publicRecorder = new Recorder();
        Exception? publicError = Record.Exception(() => XmlSaxParser.Parse(data, publicRecorder, cancellationToken: Ct));
        Assert.Equal(slowError?.GetType(), publicError?.GetType());
        Assert.Equal(slow.Normalized(), publicRecorder.Normalized());
    }

    [Fact]
    public void LoneSurrogatesAreDeclinedForCharacterInput()
    {
        foreach (string xml in new[] { "<a>" + (char)0xD800 + "</a>", "<a>" + (char)0xDC00 + "x</a>", "<a b='" + (char)0xD83D + "'/>" })
        {
            var fast = new Recorder();
            Assert.False(OptimisticSax.TryParse(xml.AsSpan(), fast, new XmlParseOptions(), Ct));
            Assert.Empty(fast.Events);
            Assert.Throws<XmlParseException>(() => XmlSaxParser.Parse(xml, new Recorder(), cancellationToken: Ct));
        }
    }

    [Fact]
    public void LargeTextAndAttributesMatchTheStreamingParser()
    {
        var builder = new StringBuilder("<r xmlns:p='urn:p'>");
        builder.Append('x', 20000).Append("&amp;").Append(string.Concat(Enumerable.Repeat("é€", 6000))).Append("<![CDATA[").Append('y', 10000).Append("]]>");
        for (int i = 0; i < 20; i++) builder.Append($"<p:e{i} a{i}='{new string('v', 300)}&lt;&#x1F600;' p:b='1'/>");
        builder.Append("</r>");
        string xml = builder.ToString();
        byte[] data = Encoding.UTF8.GetBytes(xml);
        var fast = new Recorder();
        Assert.True(OptimisticSax.TryParse(data, fast, new XmlParseOptions(), Ct));
        var slow = new Recorder();
        XmlSaxParser.Parse(new MemoryStream(data), slow, cancellationToken: Ct);
        Assert.Equal(slow.Normalized(), fast.Normalized());
        var chars = new Recorder();
        Assert.True(OptimisticSax.TryParse(xml.AsSpan(), chars, new XmlParseOptions(), Ct));
        Assert.Equal(SlowCharacters(xml, new XmlParseOptions()).Normalized(), chars.Normalized());
    }

    [Fact]
    public void ManyMembersAndDeepNestingMatchTheStreamingParser()
    {
        var members = new StringBuilder();
        for (int i = 0; i < 31; i++) members.Append($" a{i}='{i}'");
        string xml = "<r xmlns:p='u'" + members + ">" + string.Concat(Enumerable.Repeat("<p:d>", 150)) + string.Concat(Enumerable.Repeat("</p:d>", 150)) + "</r>";
        byte[] data = Encoding.UTF8.GetBytes(xml);
        var fast = new Recorder();
        Assert.True(OptimisticSax.TryParse(data, fast, new XmlParseOptions(), Ct));
        var slow = new Recorder();
        XmlSaxParser.Parse(new MemoryStream(data), slow, cancellationToken: Ct);
        Assert.Equal(slow.Normalized(), fast.Normalized());

        string tooMany = "<r" + members + " z='1' y='2'/>";
        var overflow = new Recorder();
        Assert.False(OptimisticSax.TryParse(Encoding.UTF8.GetBytes(tooMany), overflow, new XmlParseOptions(), Ct));
        Assert.Empty(overflow.Events);
    }

    [Theory]
    [InlineData("<a><b><c><d/></c></b></a>", 3, 0, 0, 0)]
    [InlineData("<a b='1' c='2' d='3'/>", 0, 2, 0, 0)]
    [InlineData("<a>0123456789012345678901234567890</a>", 0, 0, 20, 0)]
    [InlineData("<a><b/><c/><d/></a>", 0, 0, 0, 3)]
    [InlineData("<a><?abcdefghijklmnop?></a>", 0, 0, 8, 0)]
    [InlineData("<a><?abcdefghijklmnop x?></a>", 0, 0, 8, 0)]
    [InlineData("<a></a                >", 0, 0, 8, 0)]
    [InlineData("<a/>                    ", 0, 0, 8, 0)]
    [InlineData("                    <a/>", 0, 0, 8, 0)]
    [InlineData("<a/>  <!--x-->                    <?p?>", 0, 0, 8, 0)]
    public void LimitedInputBehavesLikeTheStreamingParser(string xml, int depth, int attributes, int tokens, int names)
    {
        var limits = new XmlResourceLimits();
        if (depth > 0) limits = limits with { Depth = depth };
        if (attributes > 0) limits = limits with { AttributesPerElement = attributes };
        if (tokens > 0) limits = limits with { TokenCharacters = tokens };
        if (names > 0) limits = limits with { InternedNames = names };
        var options = new XmlParseOptions { Limits = limits };
        byte[] data = Encoding.UTF8.GetBytes(xml);

        var slow = new Recorder();
        Exception? slowError = Record.Exception(() => XmlSaxParser.Parse(new MemoryStream(data), slow, options, Ct));
        var publicRecorder = new Recorder();
        Exception? publicError = Record.Exception(() => XmlSaxParser.Parse(data, publicRecorder, options, Ct));
        Assert.Equal(slowError?.GetType(), publicError?.GetType());
        Assert.NotNull(slowError);
        Assert.Equal(slow.Normalized(), publicRecorder.Normalized());
    }

    [Fact]
    public void ManyDistinctNamesStayOnTheFastPath()
    {
        string xml = "<r>" + string.Concat(Enumerable.Range(0, 20_000).Select(n => $"<n{n} a{n}='1'/>")) + "</r>";
        var options = new XmlParseOptions { Limits = new() { InternedNames = null } };
        var recorder = new Recorder();
        Assert.True(OptimisticSax.TryParse(Encoding.UTF8.GetBytes(xml), recorder, options, Ct));
        Assert.NotEmpty(recorder.Events);
    }

    [Fact]
    public void CancellationIsHonoredBeforeAnyEventIsDelivered()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var recorder = new Recorder();
        Assert.Throws<OperationCanceledException>(() => XmlSaxParser.Parse("<a/>", recorder, cancellationToken: source.Token));
        Assert.Throws<OperationCanceledException>(() => XmlSaxParser.Parse("<a/>"u8, recorder, cancellationToken: source.Token));
        Assert.Empty(recorder.Events);
    }

    [Fact]
    public void RecoveryAndInvalidOptionsAreNotAffected()
    {
        Assert.Throws<ArgumentException>(() => XmlSaxParser.Parse("<a/>", new Recorder(), new XmlParseOptions { Recover = true }, Ct));
        var recorder = new Recorder();
        var result = XmlSaxParser.ParseWithDiagnostics("<a><b></a>", recorder, new XmlParseOptions { Recover = true }, Ct);
        Assert.False(result.IsWellFormed);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Throws<ArgumentNullException>(() => XmlSaxParser.Parse("<a/>", new Recorder(), new XmlParseOptions { Limits = null! }, Ct));
    }

    [Fact]
    public void HandlerFailureSurfacesOnceAndEventsAreNeverDuplicated()
    {
        var handler = new Recorder { ThrowAtStartElement = "b" };
        Assert.Throws<InvalidOperationException>(() => XmlSaxParser.Parse("<a><b/><c/></a>", handler, cancellationToken: Ct));
        Assert.Equal(1, handler.Events.Count(e => e == "SD"));
        Assert.DoesNotContain(handler.Events, e => e.StartsWith("S:c"));
    }

    [Theory]
    [InlineData("StartDocument")]
    [InlineData("Declaration")]
    [InlineData("Comment")]
    [InlineData("ProcessingInstruction")]
    [InlineData("StartElement")]
    [InlineData("Text")]
    [InlineData("StartCData")]
    [InlineData("EndCData")]
    [InlineData("EndElement")]
    public void CancellationInsideAnyCallbackStopsDeliveryLikeTheStreamingParser(string cancelIn)
    {
        const string xml = "<?xml version=\"1.0\"?><!--c--><?pi d?><a x=\"1\">t<![CDATA[c]]><b/></a>";
        foreach (int mode in new[] { 0, 1, 2 })
        {
            using var source = new CancellationTokenSource();
            var handler = new CancelingHandler(source, cancelIn);
            Action parse = mode switch
            {
                0 => () => XmlSaxParser.Parse(xml, handler, cancellationToken: source.Token),
                1 => () => XmlSaxParser.Parse(Encoding.UTF8.GetBytes(xml), handler, cancellationToken: source.Token),
                _ => () => XmlSaxParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)), handler, cancellationToken: source.Token),
            };
            Assert.Throws<OperationCanceledException>(parse);
            Assert.True(handler.Canceled);
            // The fast path stops at the next callback boundary: nothing past the canceling event follows it.
            if (mode != 2) Assert.Equal(cancelIn, handler.Delivered[^1]);
        }
    }

    [Fact]
    public void CancellationInsideEndDocumentDoesNotFailACompletedParse()
    {
        const string xml = "<a/>";
        foreach (int mode in new[] { 0, 1, 2 })
        {
            using var source = new CancellationTokenSource();
            var handler = new CancelingHandler(source, "EndDocument");
            if (mode == 0) XmlSaxParser.Parse(xml, handler, cancellationToken: source.Token);
            else if (mode == 1) XmlSaxParser.Parse(Encoding.UTF8.GetBytes(xml), handler, cancellationToken: source.Token);
            else XmlSaxParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml)), handler, cancellationToken: source.Token);
            Assert.True(handler.Canceled);
        }
    }

    private sealed class CancelingHandler(CancellationTokenSource source, string cancelIn) : XmlSaxHandler
    {
        public readonly List<string> Delivered = [];
        public bool Canceled;

        private void Hit(string name)
        {
            Delivered.Add(name);
            if (name == cancelIn) { Canceled = true; source.Cancel(); }
        }

        public override void StartDocument() => Hit("StartDocument");
        public override void EndDocument() => Hit("EndDocument");
        public override void Declaration(XmlDeclaration declaration) => Hit("Declaration");
        public override void Comment(ReadOnlySpan<char> text) => Hit("Comment");
        public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) => Hit("ProcessingInstruction");
        public override void StartElement(XmlStartElement element) => Hit("StartElement");
        public override void EndElement(XmlEndElement element) => Hit("EndElement");
        public override void Text(ReadOnlySpan<char> text) => Hit("Text");
        public override void StartCData() => Hit("StartCData");
        public override void EndCData() => Hit("EndCData");
    }

    private static Recorder SlowCharacters(string xml, XmlParseOptions options)
    {
        var recorder = new Recorder();
        using var pump = new XmlStreamingPump(options, characterInput: true);
        var sink = new XmlSaxParser.HandlerSink(recorder);
        pump.SupplyCharacters(xml.AsMemory(), true);
        StreamingStep step;
        do { step = pump.Advance(sink, Ct); } while (step == StreamingStep.Paused);
        Assert.Equal(StreamingStep.Completed, step);
        return recorder;
    }

    private sealed class Recorder : IXmlSaxHandler
    {
        public readonly List<string> Events = [];
        public string? ThrowAtStartElement;

        public List<string> Normalized()
        {
            var merged = new List<string>();
            foreach (string e in Events)
            {
                if (e.StartsWith("T:", StringComparison.Ordinal) && merged.Count > 0 && merged[^1].StartsWith("T:", StringComparison.Ordinal)) merged[^1] += e[2..];
                else merged.Add(e);
            }
            return merged;
        }

        private static string Where(XmlLocation l) => $"{l.LineNumber},{l.LinePosition},{l.ByteOffset},{l.SourceUri}";

        public void StartDocument() => Events.Add("SD");
        public void EndDocument() => Events.Add("ED");
        public void StartElement(XmlStartElement e)
        {
            if (ThrowAtStartElement is not null && e.LocalName.SequenceEqual(ThrowAtStartElement)) throw new InvalidOperationException("handler");
            var sb = new StringBuilder($"S:{e.LocalName}|{e.Prefix}|{e.NamespaceUri}|{e.IsEmptyElement}|{Where(e.Location)} A={e.Attributes.Count}");
            foreach (var a in e.Attributes) sb.Append($" [{a.LocalName}|{a.Prefix}|{a.NamespaceUri}|{a.Value}|{a.IsDefaulted}|{a.IsValueComplete}|{a.ValueParts.Length}|{Where(a.Location)}]");
            sb.Append(" N=").Append(e.NamespaceDeclarations.Count);
            foreach (var n in e.NamespaceDeclarations) sb.Append($" [{n.Prefix}|{n.NamespaceUri}|{Where(n.Location)}]");
            Events.Add(sb.ToString());
        }
        public void EndElement(XmlEndElement e) => Events.Add($"E:{e.LocalName}|{e.Prefix}|{e.NamespaceUri}|{Where(e.Location)}");
        public void Text(ReadOnlySpan<char> text) => Events.Add("T:" + text.ToString());
        public void Comment(ReadOnlySpan<char> text) => Events.Add("C:" + text.ToString());
        public void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) => Events.Add($"P:{target.ToString()}|{data.ToString()}");
        public void StartCData() => Events.Add("CD");
        public void EndCData() => Events.Add("/CD");
        public void Declaration(XmlDeclaration d) => Events.Add($"D:{d.Version.ToString()}|{d.EncodingName.ToString()}|{d.Standalone}|{Where(d.Location)}");
        public void StartDtd(XmlDocumentTypeView d) => Events.Add("DTD");
        public void DtdDeclaration(XmlDtdDeclaration d) => Events.Add("DTDD");
        public void EndDtd() => Events.Add("/DTD");
        public void SkippedEntity(XmlSkippedEntity e) => Events.Add("SKIP");
        public void IncompleteAttributeDefault(XmlIncompleteAttributeDefault d) => Events.Add("INC");
        public void Diagnostic(XmlDiagnostic d) => Events.Add("DIAG");
    }
}

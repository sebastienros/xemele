using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xunit;

namespace Xemele.Tests.XmlPerformance;

// The optimistic whole-buffer parser may only accept input the streaming parser also accepts,
// and must then build an identical document. These tests compare both on accepted and bail-out inputs.
public sealed class OptimisticParserTests
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
        "<?xml version='1.0'\r\n?>\r\n<a\r\n  b='x\r\ny\rz'\r\n/>\r\n", "\r\n<!--c\r\nd--><a>é\r\n€\r😀\r\n<?p x\r\ny?><![CDATA[\r\n\r\r\n]]></a>\r\n",
        "\r\r\n\n\r<a/>\r\n\r\n", "<a>\r</a>", "<a/>\r",
    ];

    public static TheoryData<string> Accepted()
    {
        var data = new TheoryData<string>();
        foreach (string xml in AcceptedXml) data.Add(xml);
        return data;
    }

    public static TheoryData<string> Rejected() => new()
    {
        "", "<a>]]></a>", "<a b='<'/>", "<a b='1' b='2'/>", "<a:b/>", "<a:b:c/>", "<a>&foo;</a>", "<a>&#0;</a>", "<a>&#xD800;</a>",
        "<a>&#x110000;</a>", "<a>&#;</a>", "<a/><b/>", "<a><b></a>", "<a>&#13;\r\n&#10;</a>", "\uFEFF\uFEFF<a/>",
        "<a>\uFFFF</a>", "<a>\u0001</a>", "<!DOCTYPE a><a/>", "<a xmlns:xmlns='x'/>", "<a b='1'c='2'/>", "<a 1='x'/>",
    };

    [Theory, MemberData(nameof(Accepted))]
    public void AcceptedInputBuildsTheSameDocumentAsTheStreamingParser(string xml)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        Assert.True(XmlDocument.TryParseUtf8Optimistic(data, Snapshot(), Ct, out var fast));
        AssertSame(fast!, Slow(data));
    }

    [Theory, MemberData(nameof(Accepted))]
    public void CharacterInputBuildsTheSameDocumentAsTheStreamingParser(string xml)
    {
        if (xml.StartsWith('\uFEFF'))
        {
            Assert.False(XmlDocument.TryParseUtf16Optimistic(xml, Snapshot(), Ct, out _));
            return;
        }
        Assert.True(XmlDocument.TryParseUtf16Optimistic(xml, Snapshot(), Ct, out var fast));
        using var core = new Xemele.Parsing.Syntax.XmlParserCore(Snapshot());
        var sink = new DomSink(Snapshot(), cancellationToken: Ct);
        var result = core.Append(xml.AsSpan(), [], true, sink, Ct);
        Assert.Null(sink.Diagnostic ?? result.Diagnostic);
        AssertSame(fast!, sink.Document);
    }

    [Fact]
    public void LoneSurrogatesAreRejectedForCharacterInput()
    {
        // Attribute metadata cannot carry lone surrogates, so the inputs are built at run time.
        foreach (string xml in new[] { "<a>" + (char)0xD800 + "</a>", "<a>" + (char)0xDC00 + "x</a>", "<a b='" + (char)0xD83D + "'/>" })
        {
            Assert.False(XmlDocument.TryParseUtf16Optimistic(xml, Snapshot(), Ct, out _));
            Assert.Throws<XmlParseException>(() => XmlDocument.Parse(xml, cancellationToken: Ct));
        }
    }

    [Theory, MemberData(nameof(Rejected))]
    public void RejectedInputFallsBackToTheStreamingParser(string xml)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        bool fast = XmlDocument.TryParseUtf8Optimistic(data, Snapshot(), Ct, out _);
        bool accepted = XmlDocument.TryParse(data, out _, out _, cancellationToken: Ct);
        if (fast) Assert.True(accepted);
        Assert.False(fast);
    }

    [Fact]
    public void MutatedInputNeverDivergesFromTheStreamingParser()
    {
        var random = new Random(4242);
        string[] tokens = ["<", ">", "&", "&amp;", "&#65;", "]]>", "<!--", "<![CDATA[", "<?", "?>", "xmlns=", "xmlns:p=", "'", "\"", "=", " ", "\n", "\r", "/", ":", "é", "😀", "p:", "<a>", "</a>"];
        var seeds = new List<string>();
        seeds.AddRange(AcceptedXml);
        int accepted = 0;
        for (int round = 0; round < 40; round++)
            foreach (string seed in seeds)
            {
                var bytes = new List<byte>(Encoding.UTF8.GetBytes(seed));
                for (int edit = random.Next(1, 4); edit > 0; edit--)
                {
                    int at = random.Next(bytes.Count + 1);
                    if (random.Next(2) == 0 && at < bytes.Count) bytes.RemoveAt(at);
                    else bytes.InsertRange(at, Encoding.UTF8.GetBytes(tokens[random.Next(tokens.Length)]));
                }
                byte[] data = [.. bytes];
                if (!XmlDocument.TryParseUtf8Optimistic(data, Snapshot(), Ct, out var fast)) continue;
                accepted++;
                AssertSame(fast!, Slow(data));
            }
        Assert.True(accepted > 20);
    }

    private static readonly string[] DoctypeXml =
    [
        "<!DOCTYPE a><a/>",
        "<!DOCTYPE a [<!ENTITY e 'v'>]><a>&e;</a>",
        "<!DOCTYPE book [<!ENTITY e \"entity value &amp; safe data\">]><book><part id=\"1\">&e; &#945; &e; text</part></book>",
        "<?xml version='1.0'?>\n<!-- c -->\n<!DOCTYPE a [\n  <!ENTITY e 'v'>\n  <!ENTITY f 'é😀 &lt;x&gt; &apos;q&quot;'>\n]>\n<a b='&e;' c=\"&f;\">&f;&e;x</a>\n",
        "<!DOCTYPE a [<!ENTITY e ''>]><a>&e;</a>", "<!DOCTYPE a [<!ENTITY e ''>]><a>&e;foo</a>", "<!DOCTYPE a [<!ENTITY e ''>]><a>x&e;y</a>",
        "<!DOCTYPE a [<!ENTITY e ''>]><a>x<b/>&e;&e;<c/>&e;</a>", "<!DOCTYPE a [<!ENTITY e ''>]><a b='&e;'>&e;&amp;&e;</a>",
        "<!DOCTYPE a [<!ENTITY e 'a\tb\nc'>]><a b='&e;'>&e;</a>", "<!DOCTYPE a [<!ENTITY e 'x'>]><a><b>&e;</b>&e;<!--c-->&e;</a>",
        "<!DOCTYPE a [<!ENTITY e 'x'><!ENTITY e 'y'>]><a>&e;</a>", "<!DOCTYPE a [<!ENTITY e 'x'>]>\n\n<a>\n&e;\n&e;</a>",
        "\uFEFF<!DOCTYPE a [<!ENTITY e 'é'>]><a>é&e;</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a>&e;&lt;&#65;&e;</a>",
        "<!DOCTYPE a [<!ENTITY e '&#65;&#x1F600;&#10;'>]><a b='&e;'>&e;</a>", "<!DOCTYPE a [<!ENTITY e '<'>]><a/>",
        "<!DOCTYPE a [<!-- c --><?pi x?><!ENTITY e 'v'>]><a>&e;</a>",
        "<!DOCTYPE a [<!ENTITY e 'a &amp; b'>]><a x='&e;'>&e;</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a xmlns:p='u'><p:b p:c='&e;'/></a>",
        "<!DOCTYPE a [<!ENTITY e 'v'>]><a>]&e;</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a>&e;]&e;</a>",
    ];

    private static readonly string[] DoctypeBail =
    [
        "<!DOCTYPE a SYSTEM 'x.dtd'><a/>", "<!DOCTYPE a PUBLIC 'p' 'x.dtd'><a/>", "<!DOCTYPE a [<!ELEMENT a (#PCDATA)>]><a/>",
        "<!DOCTYPE a [<!ATTLIST a b CDATA 'd'>]><a/>", "<!DOCTYPE a [<!NOTATION n SYSTEM 'x'>]><a/>", "<!DOCTYPE a [<!ENTITY % p 'v'>]><a/>",
        "<!DOCTYPE a [<!ENTITY e SYSTEM 'x.ent'>]><a>&e;</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a>&u;</a>", "<!DOCTYPE a><a>&e;</a>",
        "<!DOCTYPE é [<!ENTITY ü 'v'>]><é>&ü;</é>", "<!DOCTYPE a [<!ENTITY f 'v'><!ENTITY e '&f;'>]><a>&e;</a>",
        "<!DOCTYPE a [<!ENTITY e '&#60;b/>'>]><a>&e;</a>", "<!DOCTYPE a [<!ENTITY e '<b/>'>]><a>&e;</a>", "<!DOCTYPE a [<!ENTITY e ']]'>]><a>&e;&gt;</a>",
        "<!DOCTYPE a [<!ENTITY e '>'>]><a>&e;</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a>&e;</a><!DOCTYPE b>", "<!DOCTYPE a [<!ENTITY e 'v'>]><!DOCTYPE b><a/>",
        "<!DOCTYPE a [<!ENTITY e 'v'>]><a>&e</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a b='&e'/>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a b='&u;'/>",
        "<!DOCTYPE a [<!ENTITY e 'x\r\ny'>]><a>&e;</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a>\r\n&e;</a>", "<!DOCTYPE a [<!ENTITY e 'v'>]>\r\n<a/>",
        "<!DOCTYPE a [<!ENTITY e 'v'>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a b='<&e;'/>", "<!DOCTYPE>", "<!DOCTYPE a [",
        "<!DOCTYPE a [<!ENTITY e 'v'>]><a b='&e;&e;' b='x'/>", "<!DOCTYPE a [<!ENTITY e 'v'>]>x<a/>", "<!DOCTYPE a [<!ENTITY e 'v'>]><a/>&e;",
        "<!DOCTYPE a [<!ENTITY e 'v'>]><b/>&e;", "<!doctype a><a/>", "<!DOCTYPE a [<!ENTITY e 'v'>]]><a/>",
    ];

    private static void AssertDtdSame(string xml, XmlParseOptions options, out bool accepted)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        accepted = XmlDocument.TryParseUtf8Optimistic(data, options, Ct, out var fast);
        bool charAccepted = !xml.StartsWith('\uFEFF') && XmlDocument.TryParseUtf16Optimistic(xml, options, Ct, out var fastChars);
        if (!accepted && !charAccepted) return;
        var slowDoc = SlowOrNull(data, options);
        Assert.True(slowDoc is not null, "fast path accepted input that the streaming parser rejects: " + xml);
        if (accepted) AssertSame(fast!, slowDoc!);
        if (charAccepted)
        {
            XmlDocument.TryParseUtf16Optimistic(xml, options, Ct, out var chars);
            using var core = new Xemele.Parsing.Syntax.XmlParserCore(options);
            var sink = new DomSink(options, cancellationToken: Ct);
            var result = core.Append(xml.AsSpan(), [], true, sink, Ct);
            Assert.Null(sink.Diagnostic ?? result.Diagnostic);
            AssertSame(chars!, sink.Document);
        }
    }

    private static XmlDocument? SlowOrNull(byte[] data, XmlParseOptions options)
    {
        using var adapter = new ByteDomAdapter(options, Ct);
        adapter.Append(data);
        if (adapter.Diagnostic is not null) return null;
        adapter.Complete();
        return adapter.Diagnostic is null ? adapter.Document : null;
    }

    public static TheoryData<string> DoctypeAccepted()
    {
        var data = new TheoryData<string>();
        foreach (string xml in DoctypeXml) data.Add(xml);
        return data;
    }

    public static TheoryData<string> DoctypeBailing()
    {
        var data = new TheoryData<string>();
        foreach (string xml in DoctypeBail) data.Add(xml);
        return data;
    }

    [Theory, MemberData(nameof(DoctypeAccepted))]
    public void DoctypeInputBuildsTheSameDocumentAsTheStreamingParser(string xml)
    {
        AssertDtdSame(xml, DtdSnapshot(), out bool accepted);
        Assert.True(accepted || xml.StartsWith('\uFEFF'), xml);
    }

    [Theory, MemberData(nameof(DoctypeBailing))]
    public void UnsupportedDoctypeInputBailsOrMatchesTheStreamingParser(string xml)
    {
        AssertDtdSame(xml, DtdSnapshot(), out bool accepted);
        Assert.False(accepted, xml);
    }

    [Theory, MemberData(nameof(DoctypeAccepted))]
    public void DoctypeIsRefusedWhenDtdProcessingIsProhibited(string xml)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        Assert.False(XmlDocument.TryParseUtf8Optimistic(data, Snapshot(), Ct, out _));
        if (xml.Contains("<!DOCTYPE")) Assert.False(XmlDocument.TryParse(data, out _, out _, null, Ct));
    }

    [Fact]
    public void EntityLimitsMatchTheStreamingParserAtEveryEdge()
    {
        string[] documents =
        [
            "<!DOCTYPE a [<!ENTITY e 'abcdef'>]><a b='&e;&e;'>&e;&e;&e;</a>",
            "<!DOCTYPE a [<!ENTITY e 'a &amp; b'>]><a b='&e;'>&e;&e;</a>",
            "<!DOCTYPE a [<!ENTITY e ''>]><a b='&e;'>&e;x&e;</a>",
            "<!DOCTYPE a [<!ENTITY e 'é😀'><!ENTITY f 'long long value'>]><a><b c='&f;'>&e;&f;</b>&amp;&f;</a>",
        ];
        int accepted = 0, rejected = 0;
        foreach (string xml in documents)
            for (int value = 1; value <= 140; value++)
                foreach (int which in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
                {
                    var limits = new XmlResourceLimits();
                    switch (which)
                    {
                        case 0: limits = limits with { ExpandedCharacters = value }; break;
                        case 1: limits = limits with { TokenCharacters = value }; break;
                        case 2: limits = limits with { EntityDepth = value % 4 + 1 }; break;
                        case 3: limits = limits with { InternedNames = value % 12 + 1 }; break;
                        case 4: limits = limits with { DocumentCharacters = value }; break;
                        case 5: limits = limits with { Depth = value % 4 + 1 }; break;
                        case 6: limits = limits with { ExpandedCharacters = value, TokenCharacters = value / 2 + 1 }; break;
                        default: limits = limits with { EntityDepth = 1, ExpandedCharacters = value + 40 }; break;
                    }
                    var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Limits = limits };
                    byte[] data = Encoding.UTF8.GetBytes(xml);
                    XmlParseOptions snapshot;
                    try { snapshot = new OperationBudget(options, Ct).Options; }
                    catch (ArgumentException) { continue; }
                    AssertDtdSame(xml, snapshot, out bool fast);
                    if (fast) accepted++; else rejected++;
                }
        Assert.True(accepted > 100 && rejected > 100, $"{accepted}/{rejected}");
    }

    [Fact]
    public void LargeDoctypeDocumentsMatchTheStreamingParser()
    {
        var builder = new StringBuilder("<!DOCTYPE r [<!ENTITY e 'entity value &amp; safe data'><!ENTITY n ''>]>\n<r>\n");
        for (int row = 0; row < 3000; row++)
            builder.Append("<p id=\"").Append(row).Append("\" t='&e;'>&e; &#945; &n;&e; text é</p>\n");
        builder.Append("</r>");
        AssertDtdSame(builder.ToString(), DtdSnapshot(), out bool accepted);
        Assert.True(accepted);
    }

    [Fact]
    public void MutatedDoctypeInputNeverDivergesFromTheStreamingParser()
    {
        var random = new Random(777);
        string[] tokens = ["<", ">", "&", "&e;", "&f;", "&amp;", "&#65;", "]]>", "]", "<!ENTITY g 'x'>", "<!ENTITY e 'y'>", "<!DOCTYPE", "[", "]>", "'", "\"", " ", "\n", "\r", "é", "😀", "%", "&u;", "<a>", "</a>"];
        int accepted = 0;
        for (int round = 0; round < 60; round++)
            foreach (string seed in DoctypeXml)
            {
                var bytes = new List<byte>(Encoding.UTF8.GetBytes(seed));
                for (int edit = random.Next(1, 4); edit > 0; edit--)
                {
                    int at = random.Next(bytes.Count + 1);
                    if (random.Next(2) == 0 && at < bytes.Count) bytes.RemoveAt(at);
                    else bytes.InsertRange(at, Encoding.UTF8.GetBytes(tokens[random.Next(tokens.Length)]));
                }
                byte[] data = [.. bytes];
                if (!XmlDocument.TryParseUtf8Optimistic(data, DtdSnapshot(), Ct, out var fast)) continue;
                accepted++;
                var slow = SlowOrNull(data, DtdSnapshot());
                Assert.NotNull(slow);
                AssertSame(fast!, slow!);
            }
        Assert.True(accepted > 50);
    }

    [Fact]
    public void ResourceLimitsAreHonoredByTheFastPath()
    {
        byte[] data = Encoding.UTF8.GetBytes("<a><b><c><d><e/></d></c></b></a>");
        var limits = new XmlResourceLimits { Depth = 3 };
        Assert.False(XmlDocument.TryParse(data, out _, out var diagnostics, new XmlParseOptions { Limits = limits }, Ct));
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void StreamLoadMatchesSpanParseAndConsumesTheStream()
    {
        byte[] data = Encoding.UTF8.GetBytes("<r><a x='1'>t</a></r>");
        using var stream = new MemoryStream(data.ToArray());
        var loaded = XmlDocument.Load(stream, null, Ct);
        Assert.Equal(stream.Length, stream.Position);
        AssertSame(loaded, XmlDocument.Parse(data, null, Ct));
        using var plain = new NonExposingStream(data);
        AssertSame(XmlDocument.Load(plain, null, Ct), loaded);
        Assert.Equal(plain.Length, plain.Position);
    }

    [Fact]
    public void StreamLoadHonorsTheStartingPositionAndFallsBackAfterBailOut()
    {
        byte[] data = Encoding.UTF8.GetBytes("XX<!DOCTYPE r [<!ENTITY e 'v'>]><r>&e;</r>");
        using var stream = new MemoryStream(data);
        stream.Position = 2;
        var loaded = XmlDocument.Load(stream, new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse }, Ct);
        Assert.Equal("v", loaded.RootElement.GetTextContent());
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void StreamLoadReportsErrorsFromTheStartingPosition()
    {
        using var stream = new NonExposingStream(Encoding.UTF8.GetBytes("<a><b></a>"));
        Assert.Throws<XmlParseException>(() => XmlDocument.Load(stream, null, Ct));
    }

    private static XmlParseOptions Snapshot() => new OperationBudget(null, Ct).Options;

    private static XmlParseOptions DtdSnapshot(XmlResourceLimits? limits = null) =>
        new OperationBudget(new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Limits = limits ?? new() }, Ct).Options;

    private static XmlDocument Slow(byte[] data, XmlParseOptions? options = null)
    {
        using var adapter = new ByteDomAdapter(options ?? Snapshot(), Ct);
        adapter.Append(data);
        Assert.Null(adapter.Diagnostic);
        adapter.Complete();
        Assert.Null(adapter.Diagnostic);
        return adapter.Document;
    }

    private static void AssertSame(XmlDocument a, XmlDocument b)
    {
        Assert.Equal(b.Nodes.Count, a.Nodes.Count);
        Assert.Equal(b.AttributeSlots.Count, a.AttributeSlots.Count);
        Assert.Equal(b.NamespaceSlots.Count, a.NamespaceSlots.Count);
        Assert.Equal(b.DocumentType, a.DocumentType);
        Assert.Equal(b.Dtd?.GeneralEntities, a.Dtd?.GeneralEntities);
        Assert.Equal(b.ValidationStandalone, a.ValidationStandalone);
        Assert.Equal(b.ValidationDeclarationLocation, a.ValidationDeclarationLocation);
        Assert.Equal(b.MutationVersion, a.MutationVersion);
        Assert.Equal(b.ContentRevision, a.ContentRevision);
        for (int i = 0; i < a.Nodes.Count; i++)
        {
            var x = a.Nodes[i]; var y = b.Nodes[i];
            Assert.Equal((y.Kind, y.Local, y.Uri, y.Prefix, y.Value), (x.Kind, x.Local, x.Uri, x.Prefix, x.Value));
            Assert.Equal((y.Parent, y.First, y.Last, y.Previous, y.Next, y.Count, y.FirstAttribute, y.LastAttribute, y.AttributeCount, y.FirstNamespace, y.LastNamespace, y.NamespaceCount),
                (x.Parent, x.First, x.Last, x.Previous, x.Next, x.Count, x.FirstAttribute, x.LastAttribute, x.AttributeCount, x.FirstNamespace, x.LastNamespace, x.NamespaceCount));
            Assert.Equal(b.OriginalLocation(new XmlNode(b, i)), a.OriginalLocation(new XmlNode(a, i)));
        }
        for (int i = 0; i < a.AttributeSlots.Count; i++)
        {
            var x = a.AttributeSlots[i]; var y = b.AttributeSlots[i];
            Assert.Equal((y.Element, y.Previous, y.Next, y.Local, y.Uri, y.Prefix, y.Value), (x.Element, x.Previous, x.Next, x.Local, x.Uri, x.Prefix, x.Value));
            Assert.Equal(b.OriginalLocation(new XmlAttribute(b, i)), a.OriginalLocation(new XmlAttribute(a, i)));
        }
        for (int i = 0; i < a.NamespaceSlots.Count; i++)
        {
            var x = a.NamespaceSlots[i]; var y = b.NamespaceSlots[i];
            Assert.Equal((y.Element, y.Previous, y.Next, y.Local, y.Uri, y.Prefix, y.Value), (x.Element, x.Previous, x.Next, x.Local, x.Uri, x.Prefix, x.Value));
            Assert.Equal(b.OriginalLocation(new XmlNamespaceDeclaration(b, i)), a.OriginalLocation(new XmlNamespaceDeclaration(a, i)));
        }
    }

    [Theory]
    [InlineData("<a><?abcdefghijklmnop?></a>")]
    [InlineData("<a><?abcdefghijklmnop x?></a>")]
    [InlineData("<a></a                >")]
    [InlineData("<a/>                    ")]
    [InlineData("                    <a/>")]
    [InlineData("<a/>  <!--x-->                    <?p?>")]
    public void TokenLimitMatchesTheStreamingParserForTargetsAndCloseTags(string xml)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        var options = new XmlParseOptions { Limits = new() { TokenCharacters = 8 } };
        var slow = Record.Exception(() => XmlDocument.Load(new ForwardOnlyStream(data), options, cancellationToken: Ct));
        Assert.NotNull(slow);
        Assert.Equal(slow.GetType(), Record.Exception(() => XmlDocument.Parse(data, options, cancellationToken: Ct))?.GetType());
        Assert.False(XmlDocument.TryParse(data, out _, out _, options, Ct));
    }

    [Fact]
    public void LongProcessingInstructionTargetsParse()
    {
        string target = new('t', 200_000);
        string xml = "<a><?" + target + " body?></a>";
        var options = new XmlParseOptions { Limits = new() { TokenCharacters = null, InternedNames = null } };
        var document = XmlDocument.Parse(Encoding.UTF8.GetBytes(xml), options, cancellationToken: Ct);
        Assert.Contains(target, document.ToXmlString(cancellationToken: Ct));
    }

    [Fact]
    public void CancellationDuringTheFinalReadOfASeekableStreamIsHonored()
    {
        using var source = new CancellationTokenSource();
        byte[] data = Encoding.UTF8.GetBytes("<a><b/></a>");
        Assert.Throws<OperationCanceledException>(() => XmlDocument.Load(new CancelingStream(data, source, seekable: true), cancellationToken: source.Token));
        using var other = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => XmlDocument.Load(new CancelingStream(data, other, seekable: false), cancellationToken: other.Token));
    }

    private sealed class ForwardOnlyStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancelingStream(byte[] data, CancellationTokenSource source, bool seekable) : Stream
    {
        private readonly MemoryStream inner = new(data, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => seekable ? inner.Length : throw new NotSupportedException();
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, count);
            if (inner.Position == inner.Length) source.Cancel();
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class NonExposingStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data, writable: false);
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 7));
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

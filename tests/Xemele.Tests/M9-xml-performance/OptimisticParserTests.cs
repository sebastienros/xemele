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
        "<a>&#x110000;</a>", "<a>&#;</a>", "<a/><b/>", "<a><b></a>", "<a>\r\n<b/>\r\n</a>", "\uFEFF\uFEFF<a/>",
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

    private static XmlDocument Slow(byte[] data)
    {
        using var adapter = new ByteDomAdapter(Snapshot(), Ct);
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

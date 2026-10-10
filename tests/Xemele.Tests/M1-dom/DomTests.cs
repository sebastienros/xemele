using System.Text;
using System.Reflection;
using System.Xml.Linq;
using Xemele.Parsing.Dom;
using Xunit;

namespace Xemele.Tests.Dom;

public sealed class DomTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Body = "<!--before--><?p data?><r xmlns='urn:r' xmlns:q='urn:q' a='a&#x9;b\r\nc' q:a='β'><q:c/>α😀&amp;\r\n<![CDATA[x<y]]><!--end--><?tail?></r><!--after-->";

    private static void Structure(XmlDocument document)
    {
        var root = document.RootElement;
        Assert.Equal("r", root.LocalName); Assert.Equal("urn:r", root.NamespaceUri); Assert.Equal("", root.Prefix);
        Assert.Equal(4, document.DocumentNode.Children.Count);
        Assert.Equal(XmlNodeKind.Comment, document.DocumentNode.FirstChild.Kind);
        Assert.Equal("before", document.DocumentNode.FirstChild.Value);
        Assert.Equal("p", document.DocumentNode.Children[1].LocalName);
        Assert.Equal("data", document.DocumentNode.Children[1].Value);
        Assert.Equal("after", document.DocumentNode.LastChild.Value);
        Assert.Equal(2, root.NamespaceDeclarations.Count);
        Assert.Equal("", root.NamespaceDeclarations[0].Prefix); Assert.Equal("urn:r", root.NamespaceDeclarations[0].Value);
        Assert.Equal("q", root.NamespaceDeclarations[1].Prefix); Assert.Equal("urn:q", root.NamespaceDeclarations[1].Value);
        Assert.Equal(2, root.Attributes.Count); Assert.Equal("a\tb c", root.GetAttribute("a")); Assert.Equal("β", root.GetAttribute("a", "urn:q"));
        Assert.Equal(5, root.Children.Count);
        Assert.Equal("c", root.FirstChild.LocalName); Assert.Equal("q", root.FirstChild.Prefix); Assert.Equal("urn:q", root.FirstChild.NamespaceUri);
        Assert.Equal(XmlNodeKind.Text, root.Children[1].Kind); Assert.Equal("α😀&\n", root.Children[1].Value);
        Assert.Equal(XmlNodeKind.CData, root.Children[2].Kind); Assert.Equal("x<y", root.Children[2].Value);
        Assert.Equal(XmlNodeKind.Comment, root.Children[3].Kind); Assert.Equal("end", root.Children[3].Value);
        Assert.Equal(XmlNodeKind.ProcessingInstruction, root.LastChild.Kind); Assert.Equal("tail", root.LastChild.LocalName); Assert.Equal("", root.LastChild.Value);
        Assert.Equal("α😀&\nx<y", root.GetTextContent());
    }

    [Fact]
    public void CharacterFactoriesPreserveOwnedStructureAndIgnoreOuterWhitespace()
    {
        string source = " \r\n" + Body + "\t";
        Structure(XmlDocument.Parse(source, cancellationToken: Cancellation));
        char[] mutable = source.ToCharArray();
        var document = XmlDocument.Parse(mutable.AsSpan(), cancellationToken: Cancellation);
        mutable.AsSpan().Fill('!'); Structure(document);
        Assert.True(XmlDocument.TryParse(source, out document, out var diagnostics, cancellationToken: Cancellation));
        Assert.Empty(diagnostics); Structure(document!);
        // Character input has no fabricated byte encoding to conflict with.
        Structure(XmlDocument.Parse("<?xml version='1.0' encoding='unknown'?>" + Body, cancellationToken: Cancellation));
    }

    [Fact]
    public void ByteAndStreamFactoriesCoverEverySmallReadSizeAndEncoding()
    {
        foreach (var (encoding, label, bom) in new[]
        {
            (new UTF8Encoding(false, true) as Encoding, "UTF-8", false),
            (new UTF8Encoding(true, true), "utf-8", true),
            (new UnicodeEncoding(false, true, true), "UTF-16", true),
            (new UnicodeEncoding(true, true, true), "UTF-16", true),
            (new UnicodeEncoding(false, false, true), "UTF-16LE", false),
            (new UnicodeEncoding(true, false, true), "UTF-16BE", false)
        })
        {
            string source = $"<?xml version='1.0' encoding='{label}'?>" + Body;
            byte[] bytes = Encode(source, encoding, bom);
            Structure(XmlDocument.Parse(bytes.AsSpan(), cancellationToken: Cancellation));
            Assert.True(XmlDocument.TryParse(bytes.AsSpan(), out var document, out var diagnostics, cancellationToken: Cancellation));
            Assert.Empty(diagnostics); Structure(document!);
            for (int read = 1; read <= 64; read++)
            {
                using var stream = new ChunkStream(bytes, read);
                Structure(XmlDocument.Load(stream, cancellationToken: Cancellation));
                Assert.False(stream.WasDisposed); Assert.Equal(0, stream.FlushCalls); Assert.InRange(stream.MaximumRequest, 1, 4096);
            }
        }
    }

    internal static byte[] Encode(string source, Encoding encoding, bool bom)
    {
        byte[] data = encoding.GetBytes(source);
        return bom ? [.. encoding.GetPreamble(), .. data] : data;
    }

    [Theory]
    [InlineData("", "XML.Syntax")]
    [InlineData("<r>", "XML.Syntax")]
    [InlineData("<r/><s/>", "XML.Syntax")]
    [InlineData("<r a='x' a='y'/>", "XML.Namespace")]
    [InlineData("<p:r/>", "XML.Namespace")]
    [InlineData("<!DOCTYPE r><r/>", "XML.DtdProhibited")]
    [InlineData("<r>&unknown;</r>", "XML.UndeclaredEntity")]
    [InlineData("<r>&#0;</r>", "XML.Syntax")]
    public void StrictRejectionNeverAdvertisesAPartialDocument(string input, string code)
    {
        Assert.False(XmlDocument.TryParse(input, out var document, out var diagnostics, cancellationToken: Cancellation));
        Assert.Null(document); Assert.Single(diagnostics); Assert.Equal(code, diagnostics[0].Code);
        Assert.Null(diagnostics[0].Location.ByteOffset);
        var exception = Assert.Throws<XmlParseException>(() => XmlDocument.Parse(input, cancellationToken: Cancellation));
        Assert.Equal(diagnostics[0], Assert.Single(exception.Diagnostics));
        if (!input.Contains('\ud800'))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            Assert.False(XmlDocument.TryParse(bytes.AsSpan(), out document, out diagnostics, cancellationToken: Cancellation));
            Assert.Null(document); Assert.Equal(code, Assert.Single(diagnostics).Code); Assert.NotNull(diagnostics[0].Location.ByteOffset);
        }
    }

    [Fact]
    public void UnpairedSurrogatesAreBuiltAtRuntimeToAvoidAttributeMetadataReplacement()
    {
        foreach (char unit in new[] { '\ud800', '\udc00' })
        {
            string input = "<r>" + unit + "</r>";
            Assert.False(XmlDocument.TryParse(input, out var document, out var diagnostics, cancellationToken: Cancellation));
            Assert.Null(document); Assert.Equal("XML.Syntax", Assert.Single(diagnostics).Code);
        }
    }

    [Theory]
    [InlineData("ISO-8859-1", "INPUT.UnsupportedEncoding")]
    [InlineData("UTF-16", "INPUT.EncodingMismatch")]
    [InlineData("UTF-16LE", "INPUT.EncodingMismatch")]
    [InlineData("UTF-8x", "INPUT.UnsupportedEncoding")]
    public void DeclarationRejectionPrecedesMalformedBodyBytes(string label, string code)
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes($"<?xml version='1.0' encoding='{label}'?>"), 0xFF];
        Assert.False(XmlDocument.TryParse(bytes.AsSpan(), out var document, out var diagnostics, cancellationToken: Cancellation));
        Assert.Null(document); Assert.Equal(code, Assert.Single(diagnostics).Code);
        for (int read = 1; read <= bytes.Length + 1; read++)
        {
            using var stream = new ChunkStream(bytes, read);
            var exception = Assert.Throws<XmlParseException>(() => XmlDocument.Load(stream, cancellationToken: Cancellation));
            Assert.Equal(code, Assert.Single(exception.Diagnostics).Code); Assert.False(stream.WasDisposed);
        }
    }

    [Fact]
    public void DeclarationAbsenceSignaturesBomsAndPiPrefixAreDistinct()
    {
        foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            Assert.Equal("r", XmlDocument.Parse(Encode("<r/>", encoding, true).AsSpan(), cancellationToken: Cancellation).RootElement.LocalName);
        foreach (Encoding encoding in new Encoding[] { new UnicodeEncoding(false, false, true), new UnicodeEncoding(true, false, true) })
        {
            Assert.False(XmlDocument.TryParse(Encode("<r/>", encoding, false).AsSpan(), out _, out var diagnostics, cancellationToken: Cancellation));
            Assert.Equal("INPUT.EncodingMismatch", Assert.Single(diagnostics).Code);
        }
        Assert.Equal("xml-stylesheet", XmlDocument.Parse("<?xml-stylesheet a?><r/>"u8, cancellationToken: Cancellation).DocumentNode.FirstChild.LocalName);
        foreach (byte[] bytes in new byte[][] { [], [0xEF, 0xBB, 0xBF], [0xFF, 0xFE], [0xFE, 0xFF] })
        {
            Assert.False(XmlDocument.TryParse(bytes.AsSpan(), out _, out var diagnostics, cancellationToken: Cancellation));
            Assert.Equal("XML.Syntax", Assert.Single(diagnostics).Code); Assert.Equal(bytes.Length, diagnostics[0].Location.ByteOffset);
        }
    }

    [Fact]
    public void OriginalOffsetsAndNormalizedScalarLocationsSurviveAllReads()
    {
        const string source = "<r>😀\r\nβ</x>";
        foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
        {
            byte[] bytes = Encode(source, encoding, true);
            long expected = encoding.GetPreamble().Length + encoding.GetByteCount("<r>😀\r\nβ");
            for (int size = 1; size <= bytes.Length + 1; size++)
            {
                using var stream = new ChunkStream(bytes, size);
                var diagnostic = Assert.Single(Assert.Throws<XmlParseException>(() => XmlDocument.Load(stream, cancellationToken: Cancellation)).Diagnostics);
                Assert.Equal("XML.Syntax", diagnostic.Code); Assert.Equal(2, diagnostic.Location.LineNumber); Assert.Equal(2, diagnostic.Location.LinePosition); Assert.Equal(expected, diagnostic.Location.ByteOffset);
            }
        }
        byte[] truncated = Encoding.UTF8.GetBytes("<r>😀");
        Assert.False(XmlDocument.TryParse(truncated.AsSpan(), out _, out var diagnostics, cancellationToken: Cancellation));
        Assert.Equal(truncated.Length, Assert.Single(diagnostics).Location.ByteOffset);
    }

    [Fact]
    public void OptionsArgumentIoAndCancellationExceptionsPropagate()
    {
        Assert.Throws<ArgumentNullException>(() => XmlDocument.Parse((string)null!, cancellationToken: Cancellation));
        Assert.Throws<ArgumentNullException>(() => XmlDocument.Load(null!, cancellationToken: Cancellation));
        Assert.Throws<ArgumentOutOfRangeException>(() => XmlDocument.Parse("<r/>", new() { Limits = new() { Depth = 0 } }, Cancellation));
        var failure = new IOException("identity");
        using var stream = new ChunkStream("<r/>"u8.ToArray(), 1) { Failure = failure };
        Assert.Same(failure, Assert.Throws<IOException>(() => XmlDocument.Load(stream, cancellationToken: Cancellation)));
        Assert.False(stream.WasDisposed);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var exception = Assert.Throws<OperationCanceledException>(() => XmlDocument.TryParse("<r/>", out _, out _, cancellationToken: cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        using var duringRead = new CancellationTokenSource();
        var sameFailure = new OperationCanceledException("read identity", duringRead.Token);
        using var cancelledStream = new ChunkStream("<r/>"u8.ToArray(), 1) { BeforeRead = () => throw sameFailure };
        Assert.Same(sameFailure, Assert.Throws<OperationCanceledException>(() => XmlDocument.Load(cancelledStream, cancellationToken: Cancellation)));
        Assert.False(cancelledStream.WasDisposed);
        using var cooperativeStream = new ChunkStream("<r/>"u8.ToArray(), 1) { BeforeRead = duringRead.Cancel };
        exception = Assert.Throws<OperationCanceledException>(() => XmlDocument.Load(cooperativeStream, cancellationToken: duringRead.Token));
        Assert.Equal(duringRead.Token, exception.CancellationToken);
    }

    [Fact]
    public void DeepConstructionAndWideUniqueMembersUseLinearBuilderWork()
    {
        const int depth = 12000;
        var options = new XmlParseOptions { Limits = new() { Depth = null, AttributesPerElement = null } };
        string source = string.Concat(Enumerable.Repeat("<r>", depth)) + "x" + string.Concat(Enumerable.Repeat("</r>", depth));
        using var adapter = new ByteDomAdapter(options, Cancellation);
        adapter.Append(Encoding.UTF8.GetBytes(source)); adapter.Complete(); Assert.Null(adapter.Diagnostic);
        var sink = typeof(ByteDomAdapter).GetField("sink", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
        var builder = (XmlDocument.DomBuilder)typeof(DomSink).GetField("builder", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sink)!;
        Assert.Equal(depth + 1, builder.ChildChecks);
        Assert.Equal("x", adapter.Document.RootElement.GetTextContent());
        var leaf = adapter.Document.RootElement;
        for (int i = 1; i < depth; i++) leaf = leaf.FirstChild;
        Assert.Equal(XmlNodeKind.Text, leaf.FirstChild.Kind);
        string wide = "<r " + string.Join(" ", Enumerable.Range(0, 1024).Select(i => $"a{i}='x' xmlns:p{i}='u{i}'")) + "/>";
        var document = XmlDocument.Parse(wide, options, Cancellation);
        Assert.Equal(1024, document.RootElement.Attributes.Count); Assert.Equal(1024, document.RootElement.NamespaceDeclarations.Count);
    }

    [Fact]
    public void AdapterBuildsBeforeEofAndNeverKeepsBorrowedChunks()
    {
        using var adapter = new ByteDomAdapter(new(), Cancellation);
        byte[] chunk = Encoding.UTF8.GetBytes("<r><c>😀&amp;</c>");
        adapter.Append(chunk); chunk.AsSpan().Fill(0xFF);
        Assert.Null(adapter.Diagnostic); Assert.Equal("😀&", adapter.Document.RootElement.FirstChild.GetTextContent());
        byte[] repeated = Encoding.UTF8.GetBytes("<c>α</c>");
        for (int i = 0; i < 128; i++) adapter.Append(repeated);
        repeated.AsSpan().Fill(0xFF);
        Assert.Equal(129, adapter.Document.RootElement.Children.Count);
        adapter.Append("</r>"u8); adapter.Complete(); Assert.Null(adapter.Diagnostic);
        Assert.Equal("😀&" + new string('α', 128), adapter.Document.RootElement.GetTextContent());
        Assert.Throws<InvalidOperationException>(adapter.Complete);
    }

    [Fact]
    public void IndependentBclOracleMatchesDeclaredStrictSubset()
    {
        var bcl = XDocument.Parse(Body, LoadOptions.PreserveWhitespace);
        var actual = XmlDocument.Parse(Body, cancellationToken: Cancellation).RootElement;
        Assert.Equal(bcl.Root!.Name.LocalName, actual.LocalName); Assert.Equal(bcl.Root.Name.NamespaceName, actual.NamespaceUri);
        Assert.Equal(bcl.Root.Value, actual.GetTextContent());
        foreach (var attribute in bcl.Root.Attributes().Where(a => !a.IsNamespaceDeclaration))
            Assert.Equal(attribute.Value, actual.GetAttribute(attribute.Name.LocalName, attribute.Name.NamespaceName));
        Assert.Equal(bcl.Root.Nodes().Count(), actual.Children.Count);
        Assert.Equal(bcl.Nodes().Count(), actual.OwnerDocument.DocumentNode.Children.Count);
    }

    private delegate string NameProbe(ReadOnlySpan<char> name);
    [Fact]
    public void WarmBorrowedNameHitsAllocateZeroAndUseOwnedInternedStrings()
    {
        var sink = new DomSink(new());
        using var core = new Xemele.Parsing.Syntax.XmlParserCore(new());
        core.Append("<r xmlns:p='urn:p' p:a='v'>", [], false, sink, Cancellation);
        var probe = typeof(DomSink).GetMethod("OwnName", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<NameProbe>(sink);
        string rootName = sink.Document.RootElement.LocalName;
        string copiedProbe = new string(new[] { 'r' });
        for (int i = 0; i < 100; i++) probe(copiedProbe.AsSpan());
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            if (!ReferenceEquals(rootName, probe(copiedProbe.AsSpan()))) throw new InvalidOperationException("Borrowed hit did not reuse owned name.");
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void LongTextAndScalarFragmentsPersistOneLogicalNode()
    {
        string text = new string('x', 65536) + "😀&β";
        string source = "<r>" + new string('x', 65536) + "😀&amp;β</r>";
        using var stream = new ChunkStream(Encoding.UTF8.GetBytes(source), 7);
        var document = XmlDocument.Load(stream, cancellationToken: Cancellation);
        Assert.Equal(1, document.RootElement.Children.Count); Assert.Equal(text, document.RootElement.FirstChild.Value);
        Assert.Equal(text, document.RootElement.GetTextContent());
    }

    [Fact]
    public void InvalidByteSequencesPreserveFirstScalarOffsetsAcrossAllReads()
    {
        foreach (byte[] invalid in new byte[][] { [0xFF], [0xC0, 0x80], [0xED, 0xA0, 0x80], [0xF4, 0x90, 0x80, 0x80], [0xF0, 0x9F, 0x98] })
        {
            byte[] bytes = [.. Encoding.UTF8.GetBytes("<r>😀\r\n"), .. invalid];
            for (int read = 1; read <= bytes.Length + 1; read++)
            {
                using var stream = new ChunkStream(bytes, read);
                var diagnostic = Assert.Single(Assert.Throws<XmlParseException>(() => XmlDocument.Load(stream, cancellationToken: Cancellation)).Diagnostics);
                Assert.Equal("INPUT.InvalidEncoding", diagnostic.Code); Assert.Equal(9, diagnostic.Location.ByteOffset);
                Assert.Equal(2, diagnostic.Location.LineNumber); Assert.Equal(1, diagnostic.Location.LinePosition);
            }
        }
        foreach (byte[] bytes in new byte[][] { [0, 0, 0xFE, 0xFF], [0xFF, 0xFE, 0, 0], [0x4C, 0x6F, 0xA7, 0x94] })
        {
            Assert.False(XmlDocument.TryParse(bytes.AsSpan(), out _, out var diagnostics, cancellationToken: Cancellation));
            Assert.Equal("INPUT.UnsupportedEncoding", Assert.Single(diagnostics).Code);
        }
    }

    [Fact]
    public void ExactQuotasAndPrimaryByteScopeAreEnforced()
    {
        var exact = new XmlParseOptions { Limits = new() { DocumentCharacters = 4, TokenCharacters = 4, InternedNames = 1, ExternalBytes = 1 } };
        Assert.Equal("r", XmlDocument.Parse("<r/>", exact, Cancellation).RootElement.LocalName);
        Assert.Equal("r", XmlDocument.Parse("<r/>"u8, exact, Cancellation).RootElement.LocalName);
        foreach (var options in new[] { exact with { Limits = exact.Limits with { DocumentCharacters = 3 } }, exact with { Limits = exact.Limits with { TokenCharacters = 3 } } })
        {
            Assert.False(XmlDocument.TryParse("<r/>", out _, out var diagnostics, options, Cancellation)); Assert.Equal("XML.ResourceLimit", Assert.Single(diagnostics).Code);
        }
        Assert.False(XmlDocument.TryParse("<r><s/></r>", out _, out var names, exact with { Limits = new() { InternedNames = 1 } }, Cancellation));
        Assert.Equal("XML.ResourceLimit", Assert.Single(names).Code);
    }

    internal sealed class ChunkStream(byte[] input, int size) : MemoryStream(input)
    {
        internal bool WasDisposed;
        internal int MaximumRequest, FlushCalls;
        internal Exception? Failure;
        internal Action? BeforeRead;
        public override int Read(Span<byte> buffer)
        {
            MaximumRequest = Math.Max(MaximumRequest, buffer.Length); BeforeRead?.Invoke();
            if (Failure is not null) throw Failure;
            return base.Read(buffer[..Math.Min(size, buffer.Length)]);
        }
        public override void Flush() { FlushCalls++; base.Flush(); }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}

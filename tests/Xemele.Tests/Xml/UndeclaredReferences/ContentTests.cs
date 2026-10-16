using System.Security.Cryptography;
using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.UndeclaredReferences;

public sealed class ContentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal const string Prefix = "<!DOCTYPE r [<!ENTITY % p \"<!ENTITY known 'K'>\">%p;]>";
    internal static XmlParseOptions Options => new() { DtdProcessing = XmlDtdProcessing.Parse };

    [Fact]
    public void IndependentlySpecifiedEventsAndProvenanceAtEverySplitAndPause()
    {
        const string xml = Prefix + "<r>a&x;&y;b&known;c</r>";
        string[] expected = ["document", "doctype:r", "start:r:False", "content", "text:Text:", "fragment:a:False",
            "fragment::True", "unresolved:x:&x;", "unresolved:y:&y;", "text:Text:",
            "fragment:b:False", "fragment:K:False", "fragment:c:False", "fragment::True", "end:r", "end-document"];
        for (var split = 0; split <= xml.Length; split++)
        {
            foreach (var pause in new[] { false, true })
            {
                using var core = new XmlParserCore(Options);
                var sink = new Sink(new DomSink(Options), pause);
                Assert.Equal(ScanStep.NeedInput, Feed(core, xml.AsSpan(0, split), [], false, sink).Step);
                Assert.Equal(ScanStep.Completed, Feed(core, xml.AsSpan(split), [], true, sink).Step);
                Assert.Equal(expected, sink.Events);
                var document = sink.Dom.Document;
                var children = Children(document.RootElement);
                Assert.Equal(["a", "bKc"], children.Select(c => c.Value));
                Assert.All(children, c => Assert.Equal(XmlNodeKind.Text, c.Kind));
                Assert.Equal(2, document.UnresolvedReferences.Count);
                for (var i = 0; i < 2; i++)
                {
                    var reference = document.UnresolvedReferences[i];
                    var name = i == 0 ? "x" : "y";
                    Assert.Equal(name, reference.Name);
                    Assert.Equal("&" + name + ";", reference.LexicalReference);
                    Assert.Equal(new XmlLocation(null, 1, xml.IndexOf("&" + name, StringComparison.Ordinal) + 1, null), reference.Location);
                    Assert.Equal(document.RootElement, reference.Parent);
                    Assert.Equal(children[0], reference.PreviousSibling);
                    Assert.Equal(1, reference.ChildBoundary);
                    Assert.Equal(i, reference.Sequence);
                }
            }
        }
    }

    [Theory]
    [InlineData("<r>&x;</r>", "XML.UndeclaredEntity")]
    [InlineData("<!DOCTYPE r []><r>&x;</r>", "XML.UndeclaredEntity")]
    [InlineData("<!DOCTYPE r [<!ENTITY % p ''>]><r>&x;</r>", "XML.UndeclaredEntity")]
    [InlineData("<?xml version='1.0' standalone='yes'?>" + Prefix + "<r>&x;</r>", "XML.UndeclaredEntity")]
    [InlineData(Prefix + "<r>&a:b;</r>", "XML.Syntax")]
    [InlineData(Prefix + "<r>&:x;</r>", "XML.Syntax")]
    [InlineData(Prefix + "<r>&#0;</r>", "XML.Syntax")]
    public void PairedWfcAndMalformedControls(string xml, string code)
    {
        Assert.False(XmlDocument.TryParse(xml, out var document, out var diagnostics, Options, Ct));
        Assert.Null(document);
        Assert.Equal(code, Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void OriginalE13ExactBytesHaveOwnedUnknownAndProcessedKnownDeclaration()
    {
        var resources = Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES")
            ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
        var path = Path.Combine(resources, "xmlconf/eduni/errata-3e/E13.xml");
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(252, bytes.Length);
        Assert.Equal("40470ff5f2281476ed4521e066d85122e9cc240f1fa916f97bd095644540fa14", Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var xml = Encoding.UTF8.GetString(bytes);
        var uri = new Uri("https://xemele.invalid/corpus/xmlconf/eduni/errata-3e/E13.xml");
        long[] maps = [.. Enumerable.Range(0, bytes.Length).Select(i => (long)i)];
        for (var split = 0; split <= xml.Length; split++)
        {
            foreach (var pause in new[] { false, true })
            {
                using var core = new XmlParserCore(Options, uri);
                core.CompleteByteSource(bytes.Length);
                var sink = new Sink(new DomSink(Options), pause);
                Assert.Equal(ScanStep.NeedInput, Feed(core, xml.AsSpan(0, split), maps.AsSpan(0, split), false, sink).Step);
                Assert.Equal(ScanStep.Completed, Feed(core, xml.AsSpan(split), maps.AsSpan(split), true, sink).Step);
                var document = sink.Dom.Document;
                Assert.Equal("foo", document.RootElement.LocalName);
                Assert.True(document.RootElement.FirstChild.IsDefault);
                var reference = Assert.Single(document.UnresolvedReferences);
                Assert.Equal("ent2", reference.Name);
                Assert.Equal("&ent2;", reference.LexicalReference);
                Assert.Equal(new XmlLocation(uri, 7, 6, 239), reference.Location);
                Assert.Equal(document.RootElement, reference.Parent);
                Assert.True(reference.PreviousSibling.IsDefault);
                Assert.Equal(0, reference.ChildBoundary);
                Assert.Equal(0, reference.Sequence);
                Assert.NotNull(document.Dtd);
                var known = Assert.Single(document.Dtd!.GeneralEntities);
                Assert.Equal("ent1", known.Name);
                Assert.Equal("text", known.ReplacementText);
                Assert.False(known.IsExternal);
                Assert.False(known.IsParameter);
                Assert.Null(known.SystemId);
                Assert.Null(known.PublicId);
                Assert.Null(known.NotationName);
                var parameter = Assert.Single(document.Dtd.ParameterEntities);
                Assert.Equal("pe", parameter.Name);
                Assert.Equal("<!ENTITY ent1 'text'>", parameter.ReplacementText);
                Assert.True(parameter.IsParameter);
                Assert.False(parameter.IsExternal);
                Assert.Null(parameter.SystemId);
                Assert.Null(parameter.PublicId);
                Assert.Null(parameter.NotationName);
                var element = Assert.Single(document.Dtd.Elements);
                Assert.Equal("foo", element.Name);
                Assert.False(element.IsExternal);
                Assert.Equal(DtdParticleKind.Any, element.Content.Kind);
                Assert.Null(element.Content.Name);
                Assert.Equal(DtdOccurrence.Once, element.Content.Occurrence);
                Assert.Empty(element.Content.Children);
                Assert.Empty(document.Dtd.Attributes);
                Assert.Empty(document.Dtd.Notations);
                Assert.Equal("foo", document.DocumentType!.Name);
                Assert.Null(document.DocumentType.SystemId);
                Assert.Null(document.DocumentType.PublicId);
                var documentChildren = Children(document.DocumentNode);
                Assert.Equal(2, documentChildren.Length);
                Assert.Equal(XmlNodeKind.Comment, documentChildren[0].Kind);
                Assert.Equal(" Even internal parameter entity references are enough to make undeclared entities into mere validity errors rather than well-formedness errors. ", documentChildren[0].Value);
                Assert.Equal(document.RootElement, documentChildren[1]);
                Assert.Equal(["document", "text:Comment:", "doctype:foo", "start:foo:False", "content", "unresolved:ent2:&ent2;", "end:foo", "end-document"],
                    sink.Events.Where(e => !e.StartsWith("fragment:", StringComparison.Ordinal) && e != "text:Text:"));
                Assert.Contains("unresolved:ent2:&ent2;", sink.Events);
            }
        }

        Assert.True(XmlDocument.TryParse(bytes, out var parsed, out var diagnostics, Options, Ct));
        Assert.Empty(diagnostics);
        Assert.Equal(239L, Assert.Single(parsed!.UnresolvedReferences).Location.ByteOffset);
        using var stream = new MemoryStream(bytes, false);
        Assert.Single(XmlDocument.Load(stream, Options, Ct).UnresolvedReferences);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void OldSinkRefusesOnlyNewOccurrences()
    {
        using var core = new XmlParserCore(Options);
        var sink = new OldSink();
        var result = core.Append(Prefix + "<r>&x;</r>", [], true, sink, Ct);
        Assert.Equal(ScanStep.Rejected, result.Step);
        Assert.Equal("XML.UnresolvedReferenceSinkRequired", result.Diagnostic!.Code);
        Assert.Equal(0, sink.TextStarts);
        using var clean = new XmlParserCore(Options);
        Assert.Equal(ScanStep.Completed, clean.Append(Prefix + "<r>&known;</r>", [], true, new OldSink(), Ct).Step);
    }

    [Theory]
    [InlineData("UTF-8")]
    [InlineData("UTF-16LE")]
    [InlineData("UTF-16BE")]
    public void ByteTransportsSupplementaryNamesAndTinyReads(string label)
    {
        var xml = "<?xml version='1.0' encoding='" + label + "'?>" + Prefix + "<r>🙂&𐀀x;b</r>";
        Encoding encoding = label == "UTF-8" ? new UTF8Encoding(false, true) : new UnicodeEncoding(label == "UTF-16BE", false, true);
        var bytes = encoding.GetBytes(xml);
        Assert.True(XmlDocument.TryParse(bytes, out var document, out var diagnostics, Options, Ct));
        Assert.Empty(diagnostics);
        var occurrence = Assert.Single(document!.UnresolvedReferences);
        Assert.Equal("𐀀x", occurrence.Name);
        Assert.Equal("&𐀀x;", occurrence.LexicalReference);
        Assert.Equal((long)encoding.GetByteCount(xml.AsSpan(0, xml.IndexOf('&', xml.IndexOf("<r>", StringComparison.Ordinal)))), occurrence.Location.ByteOffset);
        Assert.Equal(["🙂", "b"], Children(document.RootElement).Select(n => n.Value));
        using var stream = new TinyStream(bytes);
        Assert.Equal(occurrence.Name, Assert.Single(XmlDocument.Load(stream, Options, Ct).UnresolvedReferences).Name);
    }

    [Fact]
    public void CancellationAfterPausedOccurrenceDoesNotReplayAndFaultsOperation()
    {
        using var cancellation = new CancellationTokenSource();
        using var core = new XmlParserCore(Options);
        var sink = new Sink(new DomSink(Options), false) { PauseUnresolved = true };
        var xml = Prefix + "<r>&x;</r>";
        var result = core.Append(xml, [], true, sink, Ct);
        Assert.Equal(ScanStep.Paused, result.Step);
        Assert.Single(sink.Dom.Document.UnresolvedReferences);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => core.Append(xml.AsSpan(result.Consumed), [], true, sink, cancellation.Token));
        Assert.Single(sink.Dom.Document.UnresolvedReferences);
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, Ct));
    }

    private static XmlNode[] Children(XmlNode parent)
    {
        var children = new List<XmlNode>();
        foreach (var child in parent.Children)
        {
            children.Add(child);
        }

        return [.. children];
    }

    [Fact]
    public void SupplementaryNamesEveryUtf16SplitRetainExactScalarAndByteLocations()
    {
        const string xml = Prefix + "<r>🙂&𐀀x;b</r>";
        var maps = new long[xml.Length];
        var index = 0;
        long offset = 0;
        foreach (var rune in xml.EnumerateRunes())
        {
            for (var i = 0; i < rune.Utf16SequenceLength; i++)
            {
                maps[index++] = offset;
            }

            offset += rune.Utf8SequenceLength;
        }
        var uri = new Uri("https://fixture.invalid/supplementary.xml");
        for (var split = 0; split <= xml.Length; split++)
        {
            foreach (var bytes in new[] { false, true })
            {
                foreach (var pause in new[] { false, true })
                {
                    using var core = new XmlParserCore(Options, uri);
                    if (bytes)
                    {
                        core.CompleteByteSource(offset);
                    }

                    var sink = new Sink(new DomSink(Options), pause);
                    Assert.Equal(ScanStep.NeedInput, Feed(core, xml.AsSpan(0, split), bytes ? maps.AsSpan(0, split) : [], false, sink).Step);
                    Assert.Equal(ScanStep.Completed, Feed(core, xml.AsSpan(split), bytes ? maps.AsSpan(split) : [], true, sink).Step);
                    var occurrence = Assert.Single(sink.Dom.Document.UnresolvedReferences);
                    Assert.Equal("𐀀x", occurrence.Name);
                    Assert.Equal("&𐀀x;", occurrence.LexicalReference);
                    Assert.Equal(new XmlLocation(uri, 1, Prefix.Length + 5, bytes ? Prefix.Length + 7L : null), occurrence.Location);
                    Assert.Equal(["🙂", "b"], Children(sink.Dom.Document.RootElement).Select(n => n.Value));
                }
            }
        }
    }

    internal static ScanResult Feed(XmlParserCore core, ReadOnlySpan<char> input, ReadOnlySpan<long> offsets, bool final, Sink sink)
    {
        int consumed = 0, turns = 0;
        ScanResult result;
        do
        {
            result = core.Append(input[consumed..], offsets.IsEmpty ? [] : offsets[consumed..], final, sink, Ct);
            Assert.InRange(result.Consumed, 0, input.Length - consumed);
            consumed += result.Consumed;
            Assert.Null(result.Diagnostic);
            Assert.Null(sink.Dom.Diagnostic);
            Assert.True(++turns < input.Length * 4 + 100);
        } while (result.Step == ScanStep.Paused);
        Assert.Equal(input.Length, consumed);
        return result;
    }
    internal sealed class Sink(DomSink dom, bool pause) : IXmlEventSink, IXmlUnresolvedReferenceSink, IXmlDtdSink
    {
        internal DomSink Dom => dom;
        internal readonly List<string> Events = [];
        internal bool PauseUnresolved;
        private bool Next(string value, bool accepted)
        {
            Events.Add(value);
            Assert.True(accepted);
            return !pause;
        }
        public bool DocumentType(XmlDocumentType documentType, Xemele.Parsing.Dtd.DtdModel model, XmlLocation at) =>
            Next("doctype:" + documentType.Name, ((IXmlDtdSink)dom).DocumentType(documentType, model, at));
        public bool StartDocument(XmlLocation at) => Next("document", dom.StartDocument(at));
        public bool Declaration(XmlDeclarationView view) => Next("declaration:" + view.Version.ToString(), dom.Declaration(view));
        public bool StartElement(XmlNameView view, bool empty, XmlLocation at) => Next("start:" + view.LocalName.ToString() + ":" + empty, dom.StartElement(view, empty, at));
        public bool NamespaceDeclaration(XmlNamespaceView view) => Next("namespace:" + view.Prefix.ToString(), dom.NamespaceDeclaration(view));
        public bool Attribute(XmlAttributeView view) => Next("attribute:" + view.Name.LocalName.ToString() + ":" + view.Value.ToString(), dom.Attribute(view));
        public bool StartElementContent() => Next("content", dom.StartElementContent());
        public bool EndElement(XmlNameView view, XmlLocation at) => Next("end:" + view.LocalName.ToString(), dom.EndElement(view, at));
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation at) => Next("text:" + kind + ":" + target.ToString(), dom.StartText(kind, target, at));
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation at) => Next("fragment:" + text.ToString() + ":" + ended, dom.TextFragment(text, ended, at));
        public bool EndDocument(XmlLocation at) => Next("end-document", dom.EndDocument(at));
        public bool UnresolvedReference(XmlUnresolvedReferenceView view)
        {
            var result = Next("unresolved:" + view.Name.ToString() + ":" + view.LexicalReference.ToString(), dom.UnresolvedReference(view));
            return result && !PauseUnresolved;
        }
    }
    private sealed class OldSink : IXmlEventSink
    {
        internal int TextStarts;
        public bool StartDocument(XmlLocation at) => true;
        public bool Declaration(XmlDeclarationView view) => true;
        public bool StartElement(XmlNameView view, bool empty, XmlLocation at) => true;
        public bool NamespaceDeclaration(XmlNamespaceView view) => true;
        public bool Attribute(XmlAttributeView view) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView view, XmlLocation at) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation at)
        {
            TextStarts++;
            return true;
        }
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation at) => true;
        public bool EndDocument(XmlLocation at) => true;
    }
    private sealed class TinyStream(byte[] bytes) : MemoryStream(bytes, false)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(1, buffer.Length)]);
    }
}

using System.Text;
using Xemele.Streaming;
using Xunit;

namespace Xemele.Tests.Sax;

public sealed class SaxTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static void Parse(int transport, string xml, IXmlSaxHandler handler, XmlParseOptions? options = null)
    {
        if (transport == 0)
        {
            XmlSaxParser.Parse(xml, handler, options, Ct);
        }
        else if (transport == 1)
        {
            XmlSaxParser.Parse(Encoding.UTF8.GetBytes(xml), handler, options, Ct);
        }
        else
        {
            using var stream = new ChunkStream(Encoding.UTF8.GetBytes(xml), 1);
            XmlSaxParser.Parse(stream, handler, options, Ct);
            Assert.False(stream.Closed);
        }
    }
    private static XmlParseResult<XmlSaxCompletion> DiagnosticParse(int transport, string xml, IXmlSaxHandler handler, XmlParseOptions? options = null)
    {
        if (transport == 0)
        {
            return XmlSaxParser.ParseWithDiagnostics(xml, handler, options, Ct);
        }

        if (transport == 1)
        {
            return XmlSaxParser.ParseWithDiagnostics(Encoding.UTF8.GetBytes(xml), handler, options, Ct);
        }

        using var stream = new ChunkStream(Encoding.UTF8.GetBytes(xml), 3);
        var result = XmlSaxParser.ParseWithDiagnostics(stream, handler, options, Ct);
        Assert.False(stream.Closed);
        return result;
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void LexicalAndStructuralOrderIncludesCompleteNamespaceTag(int transport)
    {
        var h = new Recorder();
        Parse(transport, "<?xml version='1.0'?><!--before--><r xmlns='urn:r' xmlns:p='urn:p' p:a='β😀'>x<![CDATA[y]]><![CDATA[]]><?pi data?><c/></r><!--after-->", h);
        Assert.Equal(new[] { "document", "declaration", "comment:before", "start:r:False", "text:x", "cdata:start", "text:y", "cdata:end", "cdata:start", "cdata:end", "pi:pi:data", "start:c:True", "end:c", "end:r", "comment:after", "done" }, h.Events);
        Assert.Equal("urn:r", h.RootUri);
        Assert.Equal(new[] { "=urn:r", "p=urn:p" }, h.Namespaces);
        Assert.Equal(new[] { "a/p/urn:p=β😀:False:True" }, h.Attributes);
        if (transport == 0)
        {
            Assert.All(h.Locations, x => Assert.Null(x.ByteOffset));
        }
        else
        {
            Assert.All(h.Locations, x => Assert.NotNull(x.ByteOffset));
        }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DtdDefaultsAndUnresolvedValuesPreserveMetadataAndOrder(int transport)
    {
        var h = new Recorder();
        Parse(transport, "<!DOCTYPE r [<!ELEMENT r ANY><!ENTITY % p ''>%p;<!ATTLIST r a CDATA 'default' b CDATA ' before &unknown; after '>]><r>left&missing;right</r>", h, new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        });
        Assert.Contains("a//=default:True:True", h.Attributes);
        Assert.Contains("b//=:True:False", h.Attributes);
        Assert.Equal(" before |&unknown;| after ", h.Parts);
        Assert.True(h.Events.IndexOf("incomplete") < h.Events.IndexOf("start:r:False"));
        Assert.True(h.Events.IndexOf("dtd:end") < h.Events.IndexOf("start:r:False"));
        Assert.Equal(new[] { "Element:r", "Attribute:a", "Attribute:b", "Entity:p" }, h.Dtd);
        var skipped = h.Events.IndexOf("skipped:missing:&missing;");
        Assert.Equal("left", string.Concat(h.Events.Take(skipped).Where(x => x.StartsWith("text:", StringComparison.Ordinal)).Select(x => x[5..])));
        Assert.Equal("right", string.Concat(h.Events.Skip(skipped + 1).Where(x => x.StartsWith("text:", StringComparison.Ordinal)).Select(x => x[5..])));
        Assert.Equal("done", h.Events[^1]);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DiagnosticResultOwnsOrderedNotificationsAndCompletionFlags(int transport)
    {
        var h = new Recorder();
        var result = DiagnosticParse(transport, "<r a='1' a='2'><c></r>", h, new()
        {
            Recover = true
        });
        Assert.True(result.IsComplete);
        Assert.False(result.IsWellFormed);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.DoesNotContain("done", h.Events);
        for (var i = 0; i < result.Diagnostics.Count; i++)
        {
            Assert.Same(h.Diagnostics[i], result.Diagnostics[i]);
        }

        h.Diagnostics.Clear();
        Assert.Equal(2, result.Diagnostics.Count);
        var strictFailure = DiagnosticParse(transport, "<r>", new Recorder());
        Assert.Null(strictFailure.Value);
        Assert.False(strictFailure.IsComplete);
        Assert.False(strictFailure.IsWellFormed);
        Assert.Single(strictFailure.Diagnostics);
        var partial = DiagnosticParse(transport, "<r><", new Recorder(), new()
        {
            Recover = true
        });
        Assert.NotNull(partial.Value);
        Assert.False(partial.IsComplete);
        Assert.False(partial.IsWellFormed);
        var success = DiagnosticParse(transport, "<r/>", new Recorder());
        Assert.NotNull(success.Value);
        Assert.True(success.IsComplete);
        Assert.True(success.IsWellFormed);
        Assert.Empty(success.Diagnostics);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FatalRejectionNotifiesOnceAndCallbackExceptionIdentitySurvives(int transport)
    {
        var h = new Recorder();
        var error = Assert.Throws<XmlParseException>(() => Parse(transport, "<r>", h));
        Assert.Single(h.Diagnostics);
        Assert.Same(h.Diagnostics[0], error.Diagnostics[0]);
        Assert.DoesNotContain("done", h.Events);
        foreach (var diagnostics in new[] { false, true })
        {
            var callbackError = new XmlParseException([new("user", XmlDiagnosticSeverity.Fatal, "user", default)]);
            h = new Recorder
            {
                TextError = callbackError
            };
            var actual = Assert.Throws<XmlParseException>(() => { if (diagnostics) { DiagnosticParse(transport, "<r>x</r>", h, new() { Recover = true }); } else { Parse(transport, "<r>x</r>", h); } });
            Assert.Same(callbackError, actual);
            Assert.Empty(h.Diagnostics);
            Assert.DoesNotContain("done", h.Events);
        }
    }
    [Fact]
    public void EntryValidationAndRecoveryGuardPrecedeReadsAndCallbacks()
    {
        var h = new Recorder();
        using var stream = new ChunkStream("<r/>"u8.ToArray(), 1);
        Assert.Throws<ArgumentException>(() => XmlSaxParser.Parse(stream, h, new() { Recover = true }, Ct));
        Assert.Throws<ArgumentException>(() => XmlSaxParser.Parse("<r/>", h, new() { Recover = true }, Ct));
        Assert.Throws<ArgumentException>(() => XmlSaxParser.Parse("<r/>"u8, h, new() { Recover = true }, Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => XmlSaxParser.Parse(stream, h, new() { Limits = new() { Depth = 0 } }, Ct));
        Assert.Throws<ArgumentNullException>(() => XmlSaxParser.Parse((string)null!, h, cancellationToken: Ct));
        Assert.Throws<ArgumentNullException>(() => XmlSaxParser.Parse("<r/>", null!, cancellationToken: Ct));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => XmlSaxParser.Parse(stream, h, cancellationToken: cts.Token));
        Assert.Equal(0, stream.Reads);
        Assert.Empty(h.Events);
        Assert.False(stream.Closed);
        using var unreadable = new MemoryStream();
        unreadable.Dispose();
        Assert.Throws<ArgumentException>(() => XmlSaxParser.Parse(unreadable, h, cancellationToken: Ct));
    }
    [Fact]
    public void StreamDeliversPrefixBeforeNextReadAndLeavesOpenOnEveryFailure()
    {
        var h = new Recorder();
        using var stream = new ChunkStream("<r>x</r>"u8.ToArray(), 4);
        stream.BeforeRead = () => { if (stream.Reads == 1) { Assert.Contains("start:r:False", h.Events); } };
        XmlSaxParser.Parse(stream, h, cancellationToken: Ct);
        Assert.False(stream.Closed);
        foreach (var callback in new[] { false, true })
        {
            using var bad = new ChunkStream("<r>x</r>"u8.ToArray(), 4);
            var fault = new IOException("identity");
            h = new Recorder();
            if (callback)
            {
                h.TextError = fault;
            }
            else
            {
                bad.BeforeRead = () => throw fault;
            }

            Assert.Same(fault, Assert.Throws<IOException>(() => XmlSaxParser.Parse(bad, h, cancellationToken: Ct)));
            Assert.False(bad.Closed);
            Assert.DoesNotContain("done", h.Events);
        }
        using var cts = new CancellationTokenSource();
        h = new Recorder();
        using var cancel = new ChunkStream("<r>x</r>"u8.ToArray(), 4);
        cancel.BeforeRead = () => { if (cancel.Reads == 1) { cts.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => XmlSaxParser.Parse(cancel, h, cancellationToken: cts.Token));
        Assert.False(cancel.Closed);
        Assert.DoesNotContain("done", h.Events);
    }
    [Fact]
    public void LargeByteInputAndEveryUtf8ScratchBoundaryMatchCharacterText()
    {
        for (var pad = 0; pad < 8; pad++)
        {
            var body = new string('a', 4090 + pad) + "β😀" + new string('z', 9000);
            var xml = "<r>" + body + "</r>";
            var h = new Recorder();
            XmlSaxParser.Parse(Encoding.UTF8.GetBytes(xml), h, cancellationToken: Ct);
            Assert.Equal(body, string.Concat(h.Events.Where(x => x.StartsWith("text:", StringComparison.Ordinal)).Select(x => x[5..])));
        }
    }
    [Fact]
    public void InvalidEncodingAndQuotasCannotBecomeSuccessfulRecovery()
    {
        var h = new Recorder();
        var result = XmlSaxParser.ParseWithDiagnostics([255], h, new()
        {
            Recover = true
        }, Ct);
        Assert.False(result.IsComplete);
        Assert.False(result.IsWellFormed);
        Assert.Single(result.Diagnostics);
        Assert.Same(h.Diagnostics[0], result.Diagnostics[0]);
        h = new Recorder();
        result = XmlSaxParser.ParseWithDiagnostics("<r>abcd</r>", h, new()
        {
            Recover = true,
            Limits = new()
            {
                DocumentCharacters = 5
            }
        }, Ct);
        Assert.False(result.IsComplete);
        Assert.False(result.IsWellFormed);
        Assert.DoesNotContain("done", h.Events);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SynchronousExternalEntityUsesResolverAndDisposesTransferredOwnerOnce(int transport)
    {
        var resource = new ChunkStream(Encoding.UTF8.GetBytes("β😀"), 1);
        var resolver = new Resolver(resource);
        var h = new Recorder();
        Parse(transport, "<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/e'>]><r>&e;</r>", h,
            new()
            {
                DtdProcessing = XmlDtdProcessing.Parse,
                Resolver = resolver
            });
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resource.Disposals);
        Assert.Equal("β😀", string.Concat(h.Events.Where(x => x.StartsWith("text:", StringComparison.Ordinal)).Select(x => x[5..])));
        Assert.Equal("done", h.Events[^1]);
    }
    [Fact]
    public void DiagnosticCallbackAndMalformedStreamPreserveFailureAndOwnership()
    {
        using var stream = new ChunkStream("<r>"u8.ToArray(), 2);
        var h = new Recorder();
        Assert.Throws<XmlParseException>(() => XmlSaxParser.Parse(stream, h, cancellationToken: Ct));
        Assert.False(stream.Closed);
        var exception = new InvalidOperationException("diagnostic callback");
        var callbacks = new XmlSaxCallbacks { OnDiagnostic = _ => throw exception };
        Assert.Same(exception, Assert.Throws<InvalidOperationException>(() => XmlSaxParser.ParseWithDiagnostics("<r>", callbacks, cancellationToken: Ct)));
    }
    private sealed class Resolver(ChunkStream resource) : XmlResourceResolver
    {
        internal int Opens;
        public override XmlResolvedResource Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Opens++;
            return new(resource, request.Uri);
        }
    }
    private sealed class ChunkStream(byte[] bytes, int chunk) : MemoryStream(bytes)
    {
        internal int Reads, Disposals; internal bool Closed; internal Action? BeforeRead;
        public override int Read(Span<byte> buffer)
        {
            BeforeRead?.Invoke();
            Reads++;
            return base.Read(buffer[..Math.Min(buffer.Length, chunk)]);
        }
        protected override void Dispose(bool disposing)
        {
            Closed = true;
            Disposals++;
            base.Dispose(disposing);
        }
    }
    private sealed class Recorder : XmlSaxHandler
    {
        internal readonly List<string> Events = []; internal readonly List<XmlLocation> Locations = [];
        internal readonly List<XmlDiagnostic> Diagnostics = []; internal readonly List<string> Attributes = [], Namespaces = [], Dtd = [];
        internal string RootUri = "", Parts = ""; internal Exception? TextError;
        private void Add(string text, XmlLocation location)
        {
            Events.Add(text);
            if (location.LineNumber != 0)
            {
                Locations.Add(location);
            }
        }
        public override void StartDocument() => Add("document", default);
        public override void EndDocument() => Add("done", default);
        public override void Declaration(XmlDeclaration d) => Add("declaration", d.Location);
        public override void StartElement(XmlStartElement e)
        {
            Add("start:" + e.LocalName.ToString() + ":" + e.IsEmptyElement, e.Location);
            if (e.LocalName.SequenceEqual("r"))
            {
                RootUri = e.NamespaceUri.ToString();
                foreach (var n in e.NamespaceDeclarations)
                {
                    Namespaces.Add(n.Prefix.ToString() + "=" + n.NamespaceUri.ToString());
                }

                foreach (var a in e.Attributes)
                {
                    Attributes.Add($"{a.LocalName}/{a.Prefix}/{a.NamespaceUri}={a.Value}:{a.IsDefaulted}:{a.IsValueComplete}");
                    if (!a.IsValueComplete)
                    {
                        Parts = string.Join('|', a.ValueParts.ToArray().Select(p => p is XmlKnownAttributeText t ? t.Text : ((XmlUnknownAttributeReference)p).LexicalReference));
                    }
                }
            }
        }
        public override void EndElement(XmlEndElement e) => Add("end:" + e.LocalName.ToString(), e.Location);
        public override void Text(ReadOnlySpan<char> t)
        {
            if (TextError is not null)
            {
                throw TextError;
            }

            Add("text:" + t.ToString(), default);
        }
        public override void Comment(ReadOnlySpan<char> t) => Add("comment:" + t.ToString(), default);
        public override void ProcessingInstruction(ReadOnlySpan<char> t, ReadOnlySpan<char> d) => Add("pi:" + t.ToString() + ":" + d.ToString(), default);
        public override void StartCData() => Add("cdata:start", default);
        public override void EndCData() => Add("cdata:end", default);
        public override void StartDtd(XmlDocumentTypeView d) => Add("dtd:start", d.Location);
        public override void DtdDeclaration(XmlDtdDeclaration d)
        {
            Dtd.Add(d.Kind + ":" + d.Name.ToString());
        }
        public override void EndDtd() => Add("dtd:end", default);
        public override void SkippedEntity(XmlSkippedEntity e) => Add("skipped:" + e.Name.ToString() + ":" + e.LexicalReference.ToString(), e.Location);
        public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault d) => Events.Add("incomplete");
        public override void Diagnostic(XmlDiagnostic d)
        {
            Diagnostics.Add(d);
            Add("diagnostic", d.Location);
        }
    }
}

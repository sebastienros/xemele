using System.Reflection;
using System.Text;
using Xemele.Streaming;
using Xunit;

namespace Xemele.Tests.Push;

public sealed class PushTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("<r/>")]
    [InlineData("<?xml version='1.0'?><!--before--><r xmlns='urn:r' xmlns:p='urn:p' p:a='β😀'>x&amp;y<![CDATA[β😀]]><![CDATA[]]><?pi data?><p:c/></r><!--after-->")]
    [InlineData("<!DOCTYPE r [<!ELEMENT r ANY><!ENTITY % p ''>%p;<!ATTLIST r a CDATA 'default' b CDATA ' before &unknown; after '>]><r>left&missing;right</r>")]
    public void EveryTwoChunkSplitAndSingleBytesMatchVerifiedSax(string xml)
    {
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
        var expected = new Recorder();
        XmlSaxParser.Parse(xml, expected, options, Ct);
        var bytes = Encoding.UTF8.GetBytes(xml);
        for (var split = 0; split <= bytes.Length; split++)
        {
            var actual = new Recorder();
            using var parser = new XmlPushParser(actual, options, Ct);
            parser.Append([]);
            parser.Append(bytes.AsSpan(0, split));
            parser.Append([]);
            parser.Append(bytes.AsSpan(split));
            parser.Append([]);
            parser.Complete();
            Assert.Equal(expected.Events, actual.Events);
        }
        var single = new Recorder();
        using var one = new XmlPushParser(single, options, Ct);
        foreach (var b in bytes)
        {
            one.Append(new ReadOnlySpan<byte>(in b));
        }

        one.Complete();
        Assert.Equal(expected.Events, single.Events);
    }
    [Theory]
    [InlineData("<r/>")]
    [InlineData("<r>x</r>")]
    public void EveryPartitionOfTinyDocumentsIncludingEmptyInterleaves(string xml)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        var expected = new Recorder();
        XmlSaxParser.Parse(xml, expected, cancellationToken: Ct);
        for (var mask = 0; mask < 1 << (bytes.Length - 1); mask++)
        {
            var h = new Recorder();
            using var p = new XmlPushParser(h, cancellationToken: Ct);
            var start = 0;
            for (var end = 1; end <= bytes.Length; end++)
            {
                if (end != bytes.Length && (mask & (1 << (end - 1))) == 0)
                {
                    continue;
                }

                p.Append([]);
                p.Append(bytes.AsSpan(start, end - start));
                start = end;
            }
            p.Append([]);
            p.Complete();
            Assert.Equal(expected.Events, h.Events);
        }
    }
    [Fact]
    public void CompletionCallbackFailureFaultsRatherThanCompleting()
    {
        var error = new IOException("end callback");
        using var p = new XmlPushParser(new XmlSaxCallbacks { OnEndDocument = () => throw error }, cancellationToken: Ct);
        p.Append("<r/>"u8);
        Assert.Same(error, Assert.Throws<IOException>(() => p.Complete()));
        Assert.Throws<InvalidOperationException>(() => p.Complete());
        Assert.Throws<InvalidOperationException>(() => p.Append([]));
    }
    [Fact]
    public void ImmediateDeliveryCopiesInputAndCompletionIsExplicit()
    {
        var h = new Recorder();
        using var parser = new XmlPushParser(h, cancellationToken: Ct);
        Assert.Empty(h.Events);
        parser.Append([]);
        Assert.Equal(new[] { "document" }, h.Events);
        var prefix = "<r a='hel"u8.ToArray();
        parser.Append(prefix);
        Array.Fill(prefix, (byte)'!');
        Assert.Equal(new[] { "document" }, h.Events);
        parser.Append("lo'>x<c/>"u8);
        Assert.Contains("start:r::False|a///False/True=hello", h.Events);
        Assert.Contains("end:c:", h.Events);
        Assert.DoesNotContain("done", h.Events);
        parser.Append("</r>"u8);
        Assert.DoesNotContain("done", h.Events);
        parser.Complete();
        Assert.Equal("done", h.Events[^1]);
        Assert.Throws<InvalidOperationException>(() => parser.Complete());
        Assert.Throws<InvalidOperationException>(() => parser.Append([]));
        parser.Dispose();
        parser.Dispose();
        Assert.Throws<ObjectDisposedException>(() => parser.Complete());
        Assert.Throws<ObjectDisposedException>(() => parser.Append([]));
    }
    [Fact]
    public void AbortNeverCompletesAndEmptyCompletionRejectsMissingRoot()
    {
        var h = new Recorder();
        var parser = new XmlPushParser(h, cancellationToken: Ct);
        parser.Dispose();
        Assert.Empty(h.Events);
        using var empty = new XmlPushParser(h, cancellationToken: Ct);
        var error = Assert.Throws<XmlParseException>(() => empty.Complete());
        Assert.Equal(new[] { "document", "diagnostic" }, h.Events);
        Assert.Single(h.Diagnostics);
        Assert.Same(h.Diagnostics[0], error.Diagnostics[0]);
        Assert.Throws<InvalidOperationException>(() => empty.Append([]));
        Assert.Throws<InvalidOperationException>(() => empty.Complete());
        h = new();
        parser = new(h, cancellationToken: Ct);
        parser.Append("<r>"u8);
        parser.Dispose();
        Assert.DoesNotContain("done", h.Events);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CaughtReentrancyDoesNotFaultButEscapingReentrancyDoes(int action)
    {
        XmlPushParser parser = null!;
        var calls = 0;
        void Compete()
        {
            if (action == 0)
            {
                parser.Append([]);
            }
            else if (action == 1)
            {
                parser.Complete();
            }
            else
            {
                parser.Dispose();
            }
        }
        var callbacks = new XmlSaxCallbacks { OnStartElement = _ => { calls++; Assert.Throws<InvalidOperationException>(Compete); } };
        using (parser = new(callbacks, cancellationToken: Ct))
        {
            parser.Append("<r/>"u8);
            parser.Complete();
        }
        Assert.Equal(1, calls);
        callbacks = new()
        {
            OnStartElement = _ => Compete()
        };
        using (parser = new(callbacks, cancellationToken: Ct))
        {
            Assert.Throws<InvalidOperationException>(() => parser.Append("<r/>"u8));
            Assert.Throws<InvalidOperationException>(() => parser.Complete());
        }
    }
    [Fact]
    public async Task CompetingThreadCannotDisposeOrCompleteAnActiveAppend()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var h = new XmlSaxCallbacks { OnStartElement = _ => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10), Ct)); } };
        using var parser = new XmlPushParser(h, cancellationToken: Ct);
        var work = Task.Run(() => parser.Append("<r/>"u8), Ct);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), Ct));
            Assert.Throws<InvalidOperationException>(() => parser.Append([]));
            Assert.Throws<InvalidOperationException>(() => parser.Complete());
            Assert.Throws<InvalidOperationException>(() => parser.Dispose());
        }
        finally { release.Set(); }
        await work;
        parser.Complete();
    }
    [Fact]
    public void CallbackAndDiagnosticFaultsPreserveExactExceptionObject()
    {
        Exception[] failures = [new IOException("callback"), new XmlParseException([new("user", XmlDiagnosticSeverity.Fatal, "user", default)])];
        foreach (var failure in failures)
        {
            int diagnostics = 0, end = 0;
            using var parser = new XmlPushParser(new XmlSaxCallbacks { OnText = _ => throw failure, OnDiagnostic = _ => diagnostics++, OnEndDocument = () => end++ }, cancellationToken: Ct);
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => parser.Append("<r>x</r>"u8)));
            Assert.Equal(0, diagnostics);
            Assert.Equal(0, end);
            Assert.Throws<InvalidOperationException>(() => parser.Append([]));
        }
        var primary = new InvalidOperationException("diagnostic callback");
        using var diagnostic = new XmlPushParser(new XmlSaxCallbacks { OnDiagnostic = _ => throw primary }, cancellationToken: Ct);
        Assert.Same(primary, Assert.Throws<InvalidOperationException>(() => diagnostic.Complete()));
    }
    [Fact]
    public void CancellationAtEntryAndDuringCallbackPermanentlyFaults()
    {
        using var cts = new CancellationTokenSource();
        var end = 0;
        using var parser = new XmlPushParser(new XmlSaxCallbacks { OnStartElement = _ => cts.Cancel(), OnEndDocument = () => end++ }, cancellationToken: cts.Token);
        Assert.Throws<OperationCanceledException>(() => parser.Append("<r/>"u8));
        Assert.Throws<InvalidOperationException>(() => parser.Complete());
        Assert.Equal(0, end);
        Assert.Throws<OperationCanceledException>(() => new XmlPushParser(new Recorder(), cancellationToken: cts.Token));
        using var between = new CancellationTokenSource();
        using var idle = new XmlPushParser(new Recorder(), cancellationToken: between.Token);
        between.Cancel();
        Assert.Throws<OperationCanceledException>(() => idle.Append([]));
        Assert.Throws<InvalidOperationException>(() => idle.Append([]));
    }
    [Fact]
    public void ValidationAndMalformedBytesHaveNoSuccessPath()
    {
        var h = new Recorder();
        Assert.Throws<ArgumentNullException>(() => new XmlPushParser(null!, cancellationToken: Ct));
        Assert.Throws<ArgumentException>(() => new XmlPushParser(h, new() { Recover = true }, Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => new XmlPushParser(h, new() { Limits = new() { Depth = 0 } }, Ct));
        Assert.Empty(h.Events);
        using var parser = new XmlPushParser(h, cancellationToken: Ct);
        parser.Append([255]);
        Assert.Throws<XmlParseException>(() => parser.Complete());
        Assert.Single(h.Diagnostics);
        Assert.DoesNotContain("done", h.Events);
    }
    [Fact]
    public void ExternalOwnersCloseExactlyOnceIncludingPrimaryAndCleanupFailures()
    {
        foreach (var failureMode in new[] { 0, 1, 2, 3 })
        {
            var primary = new IOException("primary");
            using var cts = new CancellationTokenSource();
            var resource = new Resource("β😀"u8.ToArray()) { ReadFailure = failureMode == 1 ? primary : null, CleanupFailure = failureMode is 1 or 2 or 3 ? new IOException("cleanup") : null };
            var resolver = new Resolver(resource);
            var h = new XmlSaxCallbacks { OnText = _ => { if (failureMode == 2) { throw primary; } if (failureMode == 3) { cts.Cancel(); } } };
            var parser = new XmlPushParser(h, new()
            {
                DtdProcessing = XmlDtdProcessing.Parse,
                Resolver = resolver
            }, cts.Token);
            if (failureMode == 0)
            {
                parser.Append("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/e'>]><r>&e;</r>"u8);
                parser.Complete();
            }
            else
            {
                var actual = Record.Exception(() => parser.Append("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/e'>]><r>&e;</r>"u8));
                if (failureMode is 1 or 2)
                {
                    Assert.Same(primary, actual);
                }
                else
                {
                    Assert.IsAssignableFrom<OperationCanceledException>(actual);
                }

                Assert.Throws<InvalidOperationException>(() => parser.Complete());
            }
            parser.Dispose();
            parser.Dispose();
            Assert.Equal(1, resolver.Opens);
            Assert.Equal(1, resource.Disposals);
        }
    }
    [Fact]
    public void ScratchCapacityDoesNotTrackCallerChunkOrDocumentLength()
    {
        long text = 0;
        using var parser = new XmlPushParser(new XmlSaxCallbacks { OnText = t => text += t.Length }, new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null
            }
        }, Ct);
        parser.Append("<r>"u8);
        var body = new byte[1024 * 1024];
        Array.Fill(body, (byte)'x');
        for (var i = 0; i < 16; i++)
        {
            parser.Append(body);
        }

        var scratch = (byte[])typeof(XmlPushParser).GetField("scratch", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(parser)!;
        Assert.Equal(4096, scratch.Length);
        Assert.Equal(16L * body.Length, text);
        parser.Append("</r>"u8);
        parser.Complete();
        Assert.Null(typeof(XmlPushParser).GetField("scratch", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(parser));
    }
    private sealed class Resolver(Resource resource) : XmlResourceResolver
    {
        internal int Opens;
        public override XmlResolvedResource Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Opens++;
            return new(resource, request.Uri);
        }
    }
    private sealed class Resource(byte[] bytes) : MemoryStream(bytes)
    {
        internal int Disposals; internal Exception? ReadFailure, CleanupFailure;
        public override int Read(Span<byte> buffer)
        {
            if (ReadFailure is not null)
            {
                throw ReadFailure;
            }

            return base.Read(buffer[..Math.Min(1, buffer.Length)]);
        }
        protected override void Dispose(bool disposing)
        {
            Disposals++;
            base.Dispose(disposing);
            if (CleanupFailure is not null)
            {
                throw CleanupFailure;
            }
        }
    }
    private sealed class Recorder : XmlSaxHandler
    {
        internal readonly List<string> Events = []; internal readonly List<XmlDiagnostic> Diagnostics = [];
        public override void StartDocument() => Events.Add("document");
        public override void EndDocument() => Events.Add("done");
        public override void StartElement(XmlStartElement e)
        {
            var s = new StringBuilder($"start:{e.LocalName}:{e.NamespaceUri}:{e.IsEmptyElement}");
            foreach (var n in e.NamespaceDeclarations)
            {
                s.Append($"|xmlns:{n.Prefix}={n.NamespaceUri}");
            }

            foreach (var a in e.Attributes)
            {
                s.Append($"|{a.LocalName}/{a.Prefix}/{a.NamespaceUri}/{a.IsDefaulted}/{a.IsValueComplete}={a.Value}");
                foreach (var p in a.ValueParts)
                {
                    s.Append(p is XmlKnownAttributeText t ? "|known:" + t.Text : "|unknown:" + ((XmlUnknownAttributeReference)p).LexicalReference);
                }
            }
            Events.Add(s.ToString());
        }
        public override void EndElement(XmlEndElement e) => Events.Add($"end:{e.LocalName}:{e.NamespaceUri}");
        public override void Text(ReadOnlySpan<char> t)
        {
            if (Events.Count > 0 && Events[^1].StartsWith("text:", StringComparison.Ordinal))
            {
                Events[^1] += t.ToString();
            }
            else
            {
                Events.Add("text:" + t.ToString());
            }
        }
        public override void Comment(ReadOnlySpan<char> t) => Events.Add("comment:" + t.ToString());
        public override void ProcessingInstruction(ReadOnlySpan<char> t, ReadOnlySpan<char> d) => Events.Add($"pi:{t}:{d}");
        public override void StartCData() => Events.Add("cdata:start");
        public override void EndCData() => Events.Add("cdata:end");
        public override void Declaration(XmlDeclaration d) => Events.Add($"declaration:{d.Version}:{d.EncodingName}:{d.Standalone}");
        public override void StartDtd(XmlDocumentTypeView d) => Events.Add($"dtd:start:{d.Name}:{d.HasPublicId}:{d.PublicId}:{d.HasSystemId}:{d.SystemId}");
        public override void DtdDeclaration(XmlDtdDeclaration d) => Events.Add($"dtd:{d.Kind}:{d.Name}:{d.ElementName}:{d.ContentModel}:{d.AttributeType}:{d.DefaultKind}:{d.HasValue}:{d.Value}:{d.IsParameter}:{d.IsExternal}");
        public override void EndDtd() => Events.Add("dtd:end");
        public override void SkippedEntity(XmlSkippedEntity e) => Events.Add($"skipped:{e.Name}:{e.LexicalReference}");
        public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault d) => Events.Add("incomplete:" + d.Sequence);
        public override void Diagnostic(XmlDiagnostic d)
        {
            Diagnostics.Add(d);
            Events.Add("diagnostic");
        }
    }
}

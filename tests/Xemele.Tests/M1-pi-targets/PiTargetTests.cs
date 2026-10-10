using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using Xemele.Parsing.Syntax;
using Xemele.Tests.Syntax;
using Xunit;
namespace Xemele.Tests.PiTargets;

public sealed class PiTargetTests
{
    private static readonly string[] Invalid = [":a", "a:b", "a:", "a:b:c", ":", "xml:like", "XML", "xMl", "1pi", "\uD800", "a\uDC00", "\uD800x"];
    private static readonly string[] Valid = ["pi", "xml-stylesheet", "xmlfoo", "XmlLike", "_a", "é", "π.x-1", "a\u0300", "\U00010000", "\U000EFFFFx"];
    private static ScanResult Scan(string xml, int split, SyntaxTests.Sink sink)
    {
        using var core = new XmlParserCore(new());
        ScanResult result = default;
        foreach (var (start, length, final) in new[] { (0, split, false), (split, xml.Length - split, true) })
        {
            int consumed = 0;
            do
            {
                result = core.Append(xml.AsSpan(start + consumed, length - consumed), [], final, sink, TestContext.Current.CancellationToken);
                consumed += result.Consumed;
            } while (result.Step == ScanStep.Paused);
            if (result.Step == ScanStep.Rejected) return result;
            Assert.Equal(length, consumed);
        }
        return result;
    }
    [Fact]
    public void InvalidTargetsRejectAtEverySplitBeforePiCallbackIncludingPauses()
    {
        foreach (string target in Invalid)
        foreach (string xml in new[] { "<?" + target + " bogus?><foo/>", "<foo><?" + target + "?></foo>" })
        for (int split = 0; split <= xml.Length; split++)
        foreach (bool pause in new[] { false, true })
        {
            var sink = new SyntaxTests.Sink { Pause = pause };
            var result = Scan(xml, split, sink);
            Assert.Equal(ScanStep.Rejected, result.Step);
            Assert.Equal("XML.Syntax", result.Diagnostic!.Code);
            Assert.DoesNotContain(sink.Events, e => e.StartsWith("text:ProcessingInstruction:", StringComparison.Ordinal));
            Assert.DoesNotContain("end-document", sink.Events);
        }
    }
    [Fact]
    public void ValidNcNamesAndDeclarationRemainDistinctAtEverySplit()
    {
        foreach (string target in Valid)
        {
            string xml = "<?xml version='1.0'?><?" + target + " data?><p:foo xmlns:p='u' p:a='v'/>";
            for (int split = 0; split <= xml.Length; split++)
            {
                var sink = new SyntaxTests.Sink { Pause = true };
                Assert.Equal(ScanStep.Completed, Scan(xml, split, sink).Step);
                var pi = Assert.Single(sink.TextTokens, t => t.Kind == XmlTextKind.ProcessingInstruction);
                Assert.Equal(target, pi.Target); Assert.Equal("data", pi.Text);
                Assert.Single(sink.Events, e => e.StartsWith("declaration:", StringComparison.Ordinal));
                Assert.Contains("start:p:foo:u:True", sink.Events);
                Assert.Contains("attribute:p:a:u:v", sink.Events);
            }
        }
        foreach (string xml in new[] { "<?xml?><r/>", "<?XML version='1.0'?><r/>", "<r/><?xml version='1.0'?>" })
            Assert.Equal(ScanStep.Rejected, Scan(xml, xml.Length / 2, new()).Step);
    }
    [Fact]
    public void PublicFactoryRejectsBeforeArenaInternOrMutationCommit()
    {
        var document = new XmlDocument(); var root = document.CreateElement("r"); document.DocumentNode.AppendChild(root);
        int nodes = document.Nodes.Count; long version = document.MutationVersion;
        var enumeration = root.Children.GetEnumerator();
        foreach (string target in Invalid)
        {
            Assert.Throws<ArgumentException>(() => document.CreateProcessingInstruction(target, "data"));
            Assert.Equal(nodes, document.Nodes.Count); Assert.Equal(version, document.MutationVersion);
            Assert.False(enumeration.MoveNext()); Assert.True(root.FirstChild.IsDefault);
        }
        foreach (string target in Valid) Assert.Equal(target, document.CreateProcessingInstruction(target, "data").LocalName);
    }
    [Fact]
    public void CorruptTargetsRejectUnderEitherNamespacePolicyBeforeSmallBufferedOutput()
    {
        foreach (string target in Invalid)
        foreach (XmlNamespaceHandling policy in new[] { XmlNamespaceHandling.Strict, XmlNamespaceHandling.Repair })
        {
            var document = new XmlDocument(); var root = document.CreateElement("r"); document.DocumentNode.AppendChild(root);
            var pi = document.CreateProcessingInstruction("pi", "data"); root.AppendChild(pi);
            document.Nodes[pi.Slot] = new XmlDocument.NodeData(XmlNodeKind.ProcessingInstruction, target, "", "", "data") { Parent = root.Slot };
            var options = new XmlSaveOptions { NamespaceHandling = policy };
            Assert.Throws<InvalidOperationException>(() => document.ToXmlString(options, TestContext.Current.CancellationToken));
            using var stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => document.Save(stream, options, TestContext.Current.CancellationToken));
            Assert.Equal(0, stream.Length); Assert.True(stream.CanWrite);
        }
    }
    [Fact]
    public void Synthetic042AndByteStreamTransportsAgreeWithNamespaceAwareBcl()
    {
        // Owned synthetic source models the pinned upstream case without copying its fixture.
        const string synthetic = "<?a:b bogus?><foo/>";
        foreach (string target in Invalid.Where(t => !t.Any(char.IsSurrogate)))
        {
            string xml = target == "a:b" ? synthetic : "<?" + target + " bogus?><foo/>";
            Assert.Throws<XmlException>(() => { using var reader = XmlReader.Create(new StringReader(xml)); while (reader.Read()) { } });
            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            {
                byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(xml)];
                Assert.False(XmlDocument.TryParse(bytes, out var result, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken));
                Assert.Null(result); Assert.Equal("XML.Syntax", Assert.Single(diagnostics).Code);
                using var stream = new OneByteStream(bytes);
                Assert.Throws<XmlParseException>(() => XmlDocument.Load(stream, cancellationToken: TestContext.Current.CancellationToken));
                Assert.True(stream.CanRead);
            }
        }
        foreach (string target in Valid)
        {
            string xml = "<?" + target + " data?><foo/>";
            // BCL's older name tables reject supplementary names permitted by XML 1.0 Fifth Edition.
            if (!target.Any(char.IsSurrogate)) { using var reader = XmlReader.Create(new StringReader(xml)); while (reader.Read()) { } }
            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            {
                byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(xml)];
                var document = XmlDocument.Parse(bytes, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(target, document.DocumentNode.FirstChild.LocalName);
                using var stream = new OneByteStream(bytes);
                Assert.Equal(target, XmlDocument.Load(stream, cancellationToken: TestContext.Current.CancellationToken).DocumentNode.FirstChild.LocalName);
                Assert.Equal(xml, document.ToXmlString(new() { OmitXmlDeclaration = true }, TestContext.Current.CancellationToken));
            }
        }
    }
    [Fact]
    public void NonQNameEntityTokensDisallowColonsAndPreserveUndeclaredNameDiagnostic()
    {
        foreach (string name in new[] { ":a", "a:b", "a:", "a:b:c", "unknown", "π" })
        {
            string xml = "<r>&" + name + ";</r>";
            for (int split = 0; split <= xml.Length; split++)
                Assert.Equal(name.Contains(':') ? "XML.Syntax" : "XML.UndeclaredEntity", Scan(xml, split, new()).Diagnostic!.Code);
        }
    }
    [Fact]
    public void RepeatedBorrowedPiTargetsAllocateZeroAfterWarmup()
    {
        using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null } });
        var sink = new StreamingTests.CountingSink(); var cancellation = TestContext.Current.CancellationToken;
        const string chunk = "<?π.x-1 data?><?xml-stylesheet?><?\U00010000?>";
        var root = core.Append("<r>", [], false, sink, cancellation);
        Assert.Equal(ScanStep.NeedInput, root.Step); Assert.Equal(3, root.Consumed);
        Assert.Equal(3, sink.Calls); Assert.Equal(0, sink.Units);
        // One non-inlined call site checks identical progress in both phases.
        AppendPiChecked(core, chunk, sink, cancellation, 100);
        Assert.Equal(603, sink.Calls); Assert.Equal(400, sink.Units);
        long before = GC.GetAllocatedBytesForCurrentThread();
        AppendPiChecked(core, chunk, sink, cancellation, 10000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(60603, sink.Calls); Assert.Equal(40400, sink.Units);
        var completed = core.Append("</r>", [], true, sink, cancellation);
        Assert.Equal(ScanStep.Completed, completed.Step); Assert.Equal(4, completed.Consumed);
        Assert.Equal(60605, sink.Calls); Assert.Equal(40400, sink.Units);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AppendPiChecked(XmlParserCore core, string chunk, StreamingTests.CountingSink sink,
        CancellationToken cancellation, int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            var result = core.Append(chunk, [], false, sink, cancellation);
            if (result.Step != ScanStep.NeedInput || result.Consumed != chunk.Length || result.Diagnostic is not null)
                throw new InvalidOperationException("PI append did not consume the complete chunk.");
        }
    }
    [Fact]
    public void PiQuotaCancellationAndInternPrecommitRemainEnforced()
    {
        const string source = "<?π?><r/>";
        using var exact = new XmlParserCore(new() { Limits = new() { TokenCharacters = 5 } });
        Assert.Equal(ScanStep.Completed, exact.Append(source, [], true, new SyntaxTests.Sink(), TestContext.Current.CancellationToken).Step);
        using var over = new XmlParserCore(new() { Limits = new() { TokenCharacters = 4 } });
        Assert.Equal("XML.ResourceLimit", over.Append(source, [], true, new SyntaxTests.Sink(), TestContext.Current.CancellationToken).Diagnostic!.Code);
        var document = new XmlDocument(new XmlResourceLimits { InternedNames = 1 });
        Assert.Throws<ArgumentException>(() => document.CreateProcessingInstruction("a:b", ""));
        Assert.Equal("pi", document.CreateProcessingInstruction("pi", "").LocalName);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        using var core = new XmlParserCore(new()); var sink = new SyntaxTests.Sink();
        Assert.Throws<OperationCanceledException>(() => core.Append(source, [], true, sink, cancellation.Token));
        Assert.Empty(sink.Events);
        Assert.Throws<OperationCanceledException>(() => XmlDocument.Parse(source, cancellationToken: cancellation.Token));
        var serializable = XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken);
        using var stream = new MemoryStream();
        Assert.Throws<OperationCanceledException>(() => serializable.Save(stream, cancellationToken: cancellation.Token));
        Assert.Equal(0, stream.Length);
    }
    private sealed class OneByteStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }
}

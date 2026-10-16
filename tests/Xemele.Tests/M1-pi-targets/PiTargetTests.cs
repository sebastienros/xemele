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
            var consumed = 0;
            do
            {
                result = core.Append(xml.AsSpan(start + consumed, length - consumed), [], final, sink, TestContext.Current.CancellationToken);
                consumed += result.Consumed;
            } while (result.Step == ScanStep.Paused);
            if (result.Step == ScanStep.Rejected)
            {
                return result;
            }

            Assert.Equal(length, consumed);
        }
        return result;
    }
    [Fact]
    public void InvalidTargetsRejectAtEverySplitBeforePiCallbackIncludingPauses()
    {
        foreach (var target in Invalid)
        {
            foreach (var xml in new[] { "<?" + target + " bogus?><foo/>", "<foo><?" + target + "?></foo>" })
            {
                for (var split = 0; split <= xml.Length; split++)
                {
                    foreach (var pause in new[] { false, true })
                    {
                        var sink = new SyntaxTests.Sink { Pause = pause };
                        var result = Scan(xml, split, sink);
                        Assert.Equal(ScanStep.Rejected, result.Step);
                        Assert.Equal("XML.Syntax", result.Diagnostic!.Code);
                        Assert.DoesNotContain(sink.Events, e => e.StartsWith("text:ProcessingInstruction:", StringComparison.Ordinal));
                        Assert.DoesNotContain("end-document", sink.Events);
                    }
                }
            }
        }
    }
    [Fact]
    public void ValidNcNamesAndDeclarationRemainDistinctAtEverySplit()
    {
        foreach (var target in Valid)
        {
            var xml = "<?xml version='1.0'?><?" + target + " data?><p:foo xmlns:p='u' p:a='v'/>";
            for (var split = 0; split <= xml.Length; split++)
            {
                var sink = new SyntaxTests.Sink { Pause = true };
                Assert.Equal(ScanStep.Completed, Scan(xml, split, sink).Step);
                var (Kind, Target, Text) = Assert.Single(sink.TextTokens, t => t.Kind == XmlTextKind.ProcessingInstruction);
                Assert.Equal(target, Target);
                Assert.Equal("data", Text);
                Assert.Single(sink.Events, e => e.StartsWith("declaration:", StringComparison.Ordinal));
                Assert.Contains("start:p:foo:u:True", sink.Events);
                Assert.Contains("attribute:p:a:u:v", sink.Events);
            }
        }
        foreach (var xml in new[] { "<?xml?><r/>", "<?XML version='1.0'?><r/>", "<r/><?xml version='1.0'?>" })
        {
            Assert.Equal(ScanStep.Rejected, Scan(xml, xml.Length / 2, new()).Step);
        }
    }
    [Fact]
    public void PublicFactoryRejectsBeforeArenaInternOrMutationCommit()
    {
        var document = new XmlDocument();
        var root = document.CreateElement("r");
        document.DocumentNode.AppendChild(root);
        var nodes = document.Nodes.Count;
        var version = document.MutationVersion;
        var enumeration = root.Children.GetEnumerator();
        foreach (var target in Invalid)
        {
            Assert.Throws<ArgumentException>(() => document.CreateProcessingInstruction(target, "data"));
            Assert.Equal(nodes, document.Nodes.Count);
            Assert.Equal(version, document.MutationVersion);
            Assert.False(enumeration.MoveNext());
            Assert.True(root.FirstChild.IsDefault);
        }
        foreach (var target in Valid)
        {
            Assert.Equal(target, document.CreateProcessingInstruction(target, "data").LocalName);
        }
    }
    [Fact]
    public void CorruptTargetsRejectUnderEitherNamespacePolicyBeforeSmallBufferedOutput()
    {
        foreach (var target in Invalid)
        {
            foreach (var policy in new[] { XmlNamespaceHandling.Strict, XmlNamespaceHandling.Repair })
            {
                var document = new XmlDocument();
                var root = document.CreateElement("r");
                document.DocumentNode.AppendChild(root);
                var pi = document.CreateProcessingInstruction("pi", "data");
                root.AppendChild(pi);
                document.Nodes[pi.Slot] = new XmlDocument.NodeData(XmlNodeKind.ProcessingInstruction, target, "", "", "data") { Parent = root.Slot };
                var options = new XmlSaveOptions { NamespaceHandling = policy };
                Assert.Throws<InvalidOperationException>(() => document.ToXmlString(options, TestContext.Current.CancellationToken));
                using var stream = new MemoryStream();
                Assert.Throws<InvalidOperationException>(() => document.Save(stream, options, TestContext.Current.CancellationToken));
                Assert.Equal(0, stream.Length);
                Assert.True(stream.CanWrite);
            }
        }
    }
    [Fact]
    public void Synthetic042AndByteStreamTransportsAgreeWithNamespaceAwareBcl()
    {
        // Owned synthetic source models the pinned upstream case without copying its fixture.
        const string synthetic = "<?a:b bogus?><foo/>";
        foreach (var target in Invalid.Where(t => !t.Any(char.IsSurrogate)))
        {
            var xml = target == "a:b" ? synthetic : "<?" + target + " bogus?><foo/>";
            Assert.Throws<XmlException>(() => { using var reader = XmlReader.Create(new StringReader(xml)); while (reader.Read()) { } });
            foreach (var encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            {
                byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(xml)];
                Assert.False(XmlDocument.TryParse(bytes, out var result, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken));
                Assert.Null(result);
                Assert.Equal("XML.Syntax", Assert.Single(diagnostics).Code);
                using var stream = new OneByteStream(bytes);
                Assert.Throws<XmlParseException>(() => XmlDocument.Load(stream, cancellationToken: TestContext.Current.CancellationToken));
                Assert.True(stream.CanRead);
            }
        }
        foreach (var target in Valid)
        {
            var xml = "<?" + target + " data?><foo/>";
            // BCL's older name tables reject supplementary names permitted by XML 1.0 Fifth Edition.
            if (!target.Any(char.IsSurrogate))
            {
                using var reader = XmlReader.Create(new StringReader(xml));
                while (reader.Read())
                {
                }
            }
            foreach (var encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            {
                byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(xml)];
                var document = XmlDocument.Parse(bytes, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(target, document.DocumentNode.FirstChild.LocalName);
                using var stream = new OneByteStream(bytes);
                Assert.Equal(target, XmlDocument.Load(stream, cancellationToken: TestContext.Current.CancellationToken).DocumentNode.FirstChild.LocalName);
                Assert.Equal(xml, document.ToXmlString(new()
                {
                    OmitXmlDeclaration = true
                }, TestContext.Current.CancellationToken));
            }
        }
    }
    [Fact]
    public void NonQNameEntityTokensDisallowColonsAndPreserveUndeclaredNameDiagnostic()
    {
        foreach (var name in new[] { ":a", "a:b", "a:", "a:b:c", "unknown", "π" })
        {
            var xml = "<r>&" + name + ";</r>";
            for (var split = 0; split <= xml.Length; split++)
            {
                Assert.Equal(name.Contains(':') ? "XML.Syntax" : "XML.UndeclaredEntity", Scan(xml, split, new()).Diagnostic!.Code);
            }
        }
    }
    [Fact]
    public void RepeatedBorrowedPiTargetsAllocateZeroAfterWarmup()
    {
        var child = PiAllocationChild.EnterChild();
        if (child is null)
        {
            // Each guarded child runs this same compiled Fact once; parent
            // class parallelism and runtime defaults remain unchanged.
            var pair = PiAllocationChild.RunPair();
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"real={pair.Real.AllocatedBytes}; positive={pair.Positive.AllocatedBytes}; warm100={pair.Real.WarmBytes}; measured10000={pair.Real.AllocatedBytes}; measuredUnits={pair.Real.MeasuredUnits}; measuredCalls={pair.Real.MeasuredCalls}; totalUnits={pair.Real.TotalUnits}; totalCalls={pair.Real.TotalCalls}; completed=true; artifacts={pair.Directory}");
            return;
        }
        var positive = child.Mode == "positive";
        using var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                DocumentCharacters = null
            }
        });
        var sink = new StreamingTests.CountingSink();
        var cancellation = TestContext.Current.CancellationToken;
        object? escaped = null;
        if (positive)
        {
            sink.Hook = () => escaped = new byte[37];
        }

        const string chunk = "<?π.x-1 data?><?xml-stylesheet?><?\U00010000?>";
        var root = core.Append("<r>", [], false, sink, cancellation);
        Assert.Equal(ScanStep.NeedInput, root.Step);
        Assert.Equal(3, root.Consumed);
        Assert.Null(root.Diagnostic);
        Assert.Equal(3, sink.Calls);
        Assert.Equal(0, sink.Units);
        // Both counters and the whole checked loop share the same non-inlined
        // helper; setup, assertions, reports and completion are outside it.
        var warmBytes = PiAllocationProbe.AppendChecked(core, chunk, sink, cancellation, 100);
        Assert.Equal(1003, sink.Calls);
        Assert.Equal(400, sink.Units);
        long warmCalls = sink.Calls - 3, warmUnits = sink.Units;
        long calls = sink.Calls, units = sink.Units;
        var allocated = PiAllocationProbe.AppendChecked(core, chunk, sink, cancellation, 10000);
        long measuredCalls = sink.Calls - calls, measuredUnits = sink.Units - units;
        var completed = core.Append("</r>", [], true, sink, cancellation);
        GC.KeepAlive(escaped);
        // Preserve raw measurement before assertions, including a nonzero real
        // result. The parent still fails on any child failure or invalid report.
        PiAllocationChild.Write(child, warmBytes, allocated, warmUnits, warmCalls,
            measuredUnits, measuredCalls, sink.Units, sink.Calls, completed, (escaped as byte[])?.Length ?? 0);
        TestContext.Current.TestOutputHelper!.WriteLine(
            $"mode={child.Mode}; primitive={allocated}; measuredUnits={measuredUnits}; measuredCalls={measuredCalls}");
        if (positive)
        {
            Assert.True(allocated > 0, "Escaped callback allocation must be detected.");
        }
        else
        {
            Assert.Equal(0, allocated);
        }

        Assert.Equal(100000, measuredCalls);
        Assert.Equal(40000, measuredUnits);
        Assert.Equal(ScanStep.Completed, completed.Step);
        Assert.Equal(4, completed.Consumed);
        Assert.Null(completed.Diagnostic);
        Assert.Equal(101005, sink.Calls);
        Assert.Equal(40400, sink.Units);
    }
    [Fact]
    public void PiQuotaCancellationAndInternPrecommitRemainEnforced()
    {
        const string source = "<?π?><r/>";
        using var exact = new XmlParserCore(new()
        {
            Limits = new()
            {
                TokenCharacters = 5
            }
        });
        Assert.Equal(ScanStep.Completed, exact.Append(source, [], true, new SyntaxTests.Sink(), TestContext.Current.CancellationToken).Step);
        using var over = new XmlParserCore(new()
        {
            Limits = new()
            {
                TokenCharacters = 4
            }
        });
        Assert.Equal("XML.ResourceLimit", over.Append(source, [], true, new SyntaxTests.Sink(), TestContext.Current.CancellationToken).Diagnostic!.Code);
        var document = new XmlDocument(new XmlResourceLimits { InternedNames = 1 });
        Assert.Throws<ArgumentException>(() => document.CreateProcessingInstruction("a:b", ""));
        Assert.Equal("pi", document.CreateProcessingInstruction("pi", "").LocalName);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var core = new XmlParserCore(new());
        var sink = new SyntaxTests.Sink();
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

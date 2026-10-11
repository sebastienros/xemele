using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.DtdInstanceProvenance;

public sealed class CompletionHookTests
{
    private const string Xml = "<!DOCTYPE r [<!ELEMENT r (r*)>]><r>&#32;</r>";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void EverySplitEveryPauseKeepsExactlyOnceHookBeforeEndDocument()
    {
        for (int split = 0; split <= Xml.Length; split++)
        {
            using var core = new XmlParserCore(new() { DtdProcessing = XmlDtdProcessing.Parse });
            var sink = new Sink { Pause = true };
            ScanResult result = default;
            foreach (var (start, count, final) in new[] { (0, split, false), (split, Xml.Length - split, true) })
            {
                int used = 0;
                do
                {
                    result = core.Append(Xml.AsSpan(start + used, count - used), [], final, sink, Ct);
                    used += result.Consumed;
                } while (result.Step == ScanStep.Paused);
            }
            Assert.Equal(ScanStep.Completed, result.Step);
            Assert.Equal(1, sink.Proofs); Assert.Equal("end-document", sink.Events[^1]);
            Assert.Equal("proof", sink.Events[^2]);
        }
    }

    [Fact]
    public void OrdinarySinkKeepsExistingEventsAndCreatesNoSourceWitness()
    {
        using var core = new XmlParserCore(new() { DtdProcessing = XmlDtdProcessing.Parse });
        var sink = new Xemele.Tests.Syntax.SyntaxTests.Sink();
        Assert.Equal(ScanStep.Completed, core.Append(Xml, [], true, sink, Ct).Step);
        Assert.DoesNotContain("proof", sink.Events); Assert.Equal("end-document", sink.Events[^1]);
    }

    [Fact]
    public void CancellationBeforePublicationLeavesNoTrustedMetadata()
    {
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
        using var core = new XmlParserCore(options);
        var sink = new Xemele.Parsing.Dom.DomSink(options);
        string prefix = Xml[..^4];
        Assert.Equal(ScanStep.NeedInput, core.Append(prefix, [], false, sink, Ct).Step);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => core.Append("</r>", [], true, sink, cancellation.Token));
        Assert.Null(sink.Document.ValidationInstanceDiagnostic); Assert.Null(sink.Document.ValidationSourceContentRevision);
    }

    [Fact]
    public void DeclarationLookupHasExactBoundedWorkThreshold()
    {
        const string xml = "<!DOCTYPE r [<!ELEMENT unrelated EMPTY><!ELEMENT r EMPTY>]><r/>";
        XmlDocument Parse(long cap) => XmlDocument.Parse(xml, new() { DtdProcessing = XmlDtdProcessing.Parse, Limits = new() { EvaluationWork = cap } }, Ct);
        Assert.NotNull(Parse(3));
        Assert.Equal("XML.ResourceLimit", Assert.Single(Assert.Throws<XmlParseException>(() => Parse(2)).Diagnostics).Code);
    }

    private sealed class Sink : Xemele.Tests.Syntax.SyntaxTests.Sink, IXmlValidationProvenanceSink, IXmlEventSink
    {
        internal int Proofs;
        public void ValidationProvenance(XmlDiagnostic diagnostic, DtdElementDeclaration declaration) { Proofs++; Events.Add("proof"); }
        bool IXmlEventSink.EndDocument(XmlLocation location) => base.EndDocument(location);
    }
}

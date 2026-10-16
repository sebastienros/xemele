using System.Reflection;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class ReviewRegressionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static XmlEntityContext Context(string name, string replacement) => new(new([], [],
        [new DtdEntityDeclaration(name, false, replacement, null, null, null, false, default)], [], []), Cancellation);

    [Fact]
    public void ColonReferenceKeepsCoreRejectionAtEverySplitAndPause()
    {
        const string input = "<r>&a:b;X</r>";
        for (var split = 0; split <= input.Length; split++)
        {
            for (var pause = -1; pause < 5; pause++)
            {
                using var core = new XmlParserCore(new(), Context("a:b", "value"));
                var sink = new ContentTests.RecordingSink(pause);
                var offset = 0;
                var first = true;
                while (true)
                {
                    var end = first ? split : input.Length;
                    var result = core.Append(input.AsSpan(offset, end - offset), [], !first, sink, Cancellation);
                    offset += result.Consumed;
                    if (result.Step == ScanStep.Rejected)
                    {
                        Assert.Equal("XML.Syntax", result.Diagnostic!.Code);
                        Assert.Equal(8, offset);
                        break;
                    }
                    Assert.NotEqual(ScanStep.Completed, result.Step);
                    if (result.Step == ScanStep.NeedInput)
                    {
                        first = false;
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData("<")]
    [InlineData("&amp")]
    [InlineData("<b>")]
    public void InvalidEntityEofNeverConsumesParentSuffix(string replacement)
    {
        const string input = "<r>&e;X</r>";
        for (var pause = -1; pause < 6; pause++)
        {
            using var core = new XmlParserCore(new(), Context("e", replacement));
            var sink = new ContentTests.RecordingSink(pause);
            var offset = 0;
            while (true)
            {
                var result = core.Append(input.AsSpan(offset), [], true, sink, Cancellation);
                offset += result.Consumed;
                if (result.Step == ScanStep.Rejected)
                {
                    Assert.Equal(6, offset);
                    Assert.Equal("X</r>", input[offset..]);
                    break;
                }
                Assert.Equal(ScanStep.Paused, result.Step);
            }
        }
    }

    [Fact]
    public void CanceledCharacterResumeDrainsAcceptedEntityBeforeDispose()
    {
        const string input = "<r>&e;</r>";
        using var core = new XmlParserCore(new(), Context("e", "many characters"));
        var sink = new ContentTests.RecordingSink(4);
        var first = core.Append(input, [], true, sink, Cancellation);
        Assert.Equal(ScanStep.Paused, first.Step);
        var budget = Assert.IsType<OperationBudget>(typeof(XmlParserCore).GetField("budget", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(core));
        Assert.Equal(1, budget.Count(Quota.EntityDepth));
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => core.Append(input.AsSpan(first.Consumed), [], true, sink, canceled.Token));
        Assert.Equal(0, budget.Count(Quota.EntityDepth));
    }

    [Fact]
    public void CanceledEmptyModelsRejectAtContextAndMergeEntry()
    {
        var empty = new Xemele.Parsing.Dtd.DtdModel([], [], [], [], []);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new XmlEntityContext(empty, canceled.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => XmlEntityContext.Merge(empty, empty, canceled.Token));
    }

    [Theory]
    [InlineData("<!ENTITY a:b 'v'>", "<r a='&a:b;'/>")]
    [InlineData("<!ENTITY a:b 'v'><!ENTITY e '&a:b;'>", "<r a='&e;'/>")]
    [InlineData("<!ENTITY a:b 'v'><!ATTLIST r a CDATA '&a:b;'>", "<r/>")]
    public void AttributeUseRejectsColonThroughExplicitNestedAndDefaultReferences(string declarations, string body)
    {
        var input = "<!DOCTYPE r [" + declarations + "]>" + body;
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
        Assert.False(XmlDocument.TryParse(input, out _, out var diagnostics, options, Cancellation));
        Assert.Equal("XML.Syntax", Assert.Single(diagnostics).Code);
        Assert.False(XmlDocument.TryParse("<!DOCTYPE r [<!ENTITY a:b 'v'>]><r/>", out _, out diagnostics, options, Cancellation));
        Assert.Equal("XML.Syntax", Assert.Single(diagnostics).Code);
    }
}

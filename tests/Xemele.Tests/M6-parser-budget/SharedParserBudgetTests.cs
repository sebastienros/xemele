using System.Runtime.InteropServices;
using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.ParserBudget;

public sealed class SharedParserBudgetTests
{
    private static readonly Uri Source = new("https://fixture.invalid/redirect/final.xml");
    private static XmlDiagnostic? Characters(string xml, OperationBudget budget)
    {
        using var core = new XmlParserCore(budget.Options, Source, operationBudget: budget);
        var sink = new DomSink(budget.Options);
        var result = core.Append(xml.AsSpan(), [], true, sink, TestContext.Current.CancellationToken);
        return sink.Diagnostic ?? result.Diagnostic;
    }

    [Fact]
    public void CharacterParsesShareExactScalarQuotaAndReleaseDepth()
    {
        const string xml = "<r>\U0001F642</r>";
        var scalars = xml.Length - 1;
        var budget = new OperationBudget(new()
        {
            Limits = new()
            {
                DocumentCharacters = scalars * 2,
                Depth = 1
            }
        }, default);
        Assert.Null(Characters(xml, budget));
        Assert.Equal(scalars, budget.Count(Quota.DocumentCharacters));
        Assert.Equal(0, budget.Count(Quota.Depth));
        Assert.Null(Characters(xml, budget));
        Assert.Equal(scalars * 2, budget.Count(Quota.DocumentCharacters));
        Assert.NotNull(Characters("<r/>", budget));
        Assert.Equal(scalars * 2, budget.Count(Quota.DocumentCharacters));
        Assert.Equal(0, budget.Count(Quota.Depth));
    }

    [Fact]
    public void ExpansionsAccumulateAcrossParsesWithoutCountingMarkup()
    {
        var budget = new OperationBudget(new()
        {
            Limits = new()
            {
                ExpandedCharacters = 2
            }
        }, default);
        Assert.Null(Characters("<r/>", budget));
        Assert.Equal(0, budget.Count(Quota.ExpandedCharacters));
        Assert.Null(Characters("<r>&amp;</r>", budget));
        Assert.Equal(1, budget.Count(Quota.ExpandedCharacters));
        Assert.Null(Characters("<r>&#x1F642;</r>", budget));
        Assert.Equal(2, budget.Count(Quota.ExpandedCharacters));
        Assert.NotNull(Characters("<r>&lt;</r>", budget));
        Assert.Equal(2, budget.Count(Quota.ExpandedCharacters));
    }

    [Fact]
    public void ByteParsesShareCountersAndHonorBudgetOptions()
    {
        var budget = new OperationBudget(new()
        {
            Limits = new()
            {
                DocumentCharacters = 8,
                Depth = 1
            }
        }, default);
        for (var i = 0; i < 2; i++)
        {
            using var adapter = new ByteDomAdapter(new()
            {
                Limits = new()
                {
                    DocumentCharacters = 1
                }
            }, default, budget, Source);
            adapter.Append("<r/>"u8);
            adapter.Complete();
            Assert.Null(adapter.Diagnostic);
            Assert.Equal("r", adapter.Document.RootElement.LocalName);
            Assert.Equal(0, budget.Count(Quota.Depth));
        }
        Assert.Equal(8, budget.Count(Quota.DocumentCharacters));
        using var denied = new ByteDomAdapter(budget.Options, default, budget, Source);
        denied.Append("<r/>"u8);
        denied.Complete();
        Assert.NotNull(denied.Diagnostic);
        Assert.Equal(Source, denied.Diagnostic.Location.SourceUri);
        Assert.Equal(8, budget.Count(Quota.DocumentCharacters));
    }

    [Fact]
    public void ByteFramingPreservesDeclaredLegacyEncodingAndFinalSource()
    {
        const string xml = "<?xml version='1.0' encoding='iso-8859-1'?><r>é</r>";
        var budget = new OperationBudget(new(), default);
        using var adapter = new ByteDomAdapter(budget.Options, default, budget, Source);
        foreach (var b in Encoding.Latin1.GetBytes(xml))
        {
            adapter.Append([b]);
        }

        adapter.Complete();
        Assert.Null(adapter.Diagnostic);
        Assert.Equal("é", adapter.Document.RootElement.GetTextContent());
        Assert.Equal(xml.Length, budget.Count(Quota.DocumentCharacters));
        using var invalid = new ByteDomAdapter(budget.Options, default, budget, Source);
        invalid.Append("<r>"u8);
        invalid.Complete();
        Assert.NotNull(invalid.Diagnostic);
        Assert.Equal(Source, invalid.Diagnostic.Location.SourceUri);
    }

    [Fact]
    public void DefaultConstructorsKeepIndependentQuotas()
    {
        for (var i = 0; i < 2; i++)
        {
            var options = new XmlParseOptions { Limits = new() { DocumentCharacters = 4 } };
            using var core = new XmlParserCore(options);
            var sink = new DomSink(options);
            Assert.Null(core.Append("<r/>".AsSpan(), [], true, sink, TestContext.Current.CancellationToken).Diagnostic);
            using var adapter = new ByteDomAdapter(options, default);
            adapter.Append("<r/>"u8);
            adapter.Complete();
            Assert.Null(adapter.Diagnostic);
        }
        Assert.StartsWith(".NET 10.", RuntimeInformation.FrameworkDescription);
    }

    [Fact]
    public void SharedOptionsRetainEntityResolverAndExactExpansionLimit()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY e SYSTEM 'entity.ent'>]><r>&e;</r>";
        var resolver = new EntityResolver();
        var budget = new OperationBudget(new()
        {
            DtdProcessing = XmlDtdProcessing.Parse,
            Resolver = resolver,
            Limits = new()
            {
                ExpandedCharacters = 4,
                ExternalResources = null,
                ExternalBytes = null
            }
        }, default);
        Assert.Null(Characters(xml, budget));
        Assert.Equal(2, budget.Count(Quota.ExpandedCharacters));
        Assert.Null(Characters(xml, budget));
        Assert.Equal(4, budget.Count(Quota.ExpandedCharacters));
        Assert.NotNull(Characters(xml, budget));
        Assert.Equal(4, budget.Count(Quota.ExpandedCharacters));
        Assert.Equal(3, resolver.Calls);
        Assert.Equal(3, resolver.Disposals);
        Assert.Equal(0, budget.Count(Quota.EntityDepth));
        Assert.Equal(0, budget.Count(Quota.Depth));
    }

    private sealed class EntityResolver : XmlResourceResolver
    {
        internal int Calls, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return new(new EntityStream(this), new("https://fixture.invalid/entity-final"));
        }
        private sealed class EntityStream(EntityResolver owner) : MemoryStream("xy"u8.ToArray())
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    owner.Disposals++;
                }

                base.Dispose(disposing);
            }
        }
    }
}

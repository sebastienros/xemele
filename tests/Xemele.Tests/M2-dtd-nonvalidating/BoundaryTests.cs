using Xemele.Parsing.Dtd;
using Xunit;
namespace Xemele.Tests.DtdNonvalidating;

public sealed class BoundaryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Uri Parent = new("https://fixture.invalid/parent.dtd"), Child = new("https://fixture.invalid/actual/child.dtd");
    // Same expanded grammar is allowed from inside markup and forbidden as an
    // incomplete DeclSep replacement. Expected results are authored from WFCs.
    [Theory]
    [InlineData("<!ELEMENT r ", "EMPTY>", "", true)]
    [InlineData("<!ELEMENT r ", "(a", ",b)>", true)]
    [InlineData("<!ELEMENT r ", "(a|", "b,c)>", false)]
    [InlineData("<!ELEMENT r ", "(a,", ")>", false)]
    [InlineData("<!ELEMENT r ", "(a", ">", false)]
    [InlineData("", "<!ELEMENT r", " EMPTY>", false)]
    [InlineData("", "<!--", "-->", false)]
    [InlineData("", "<?p", "?>", false)]
    [InlineData("", "<![INCLUDE[", "]]>", false)]
    [InlineData("", "<![IGNORE[", "]]>", false)]
    [InlineData("<![", "INCLUDE[<!ELEMENT r EMPTY>", "]]>", true)]
    [InlineData("<![", "IGNORE[garbage", "]]><!ELEMENT r EMPTY>", true)]
    [InlineData("<![", "INCLUDE[<!ELEMENT r EMPTY>", "]>", false)]
    [InlineData("<![INCLUDE[", "]]><!ELEMENT r EMPTY><![INCLUDE[", "]]>", false)]
    [InlineData("<!ENTITY e '", "'", "'>", true)]
    public void EveryParentAndExternalChildSplitRetainsInsertionContext(string prefix, string child, string suffix, bool valid)
    {
        string source = "<!ENTITY % p SYSTEM 'child.dtd'>" + prefix + "%p;" + suffix;
        for (int split = 0; split <= source.Length; split++)
        for (int childSplit = 0; childSplit <= child.Length; childSplit++)
        {
            var ledger = new DtdParserSourceTests.Ledger();
            using var parser = new DtdParser(DtdSubsetKind.External, new(), ledger, new(Parent, 1, 1, null));
            int consumed = 0; var first = parser.Append(source.AsSpan(0, split), [], false, Ct); consumed += first.Consumed;
            var result = first;
            if (result.Step == DtdParseStep.NeedInput) { result = parser.Append(source.AsSpan(consumed), [], true, Ct); consumed += result.Consumed; }
            Assert.Equal(DtdParseStep.NeedEntity, result.Step);
            Assert.Equal("child.dtd", result.Entity!.Value.SystemId); Assert.Equal(Parent, result.Entity.Value.Location.SourceUri);
            parser.PushEntity(Child);
            result = parser.Append(child.AsSpan(0, childSplit), [], false, Ct);
            if (result.Step == DtdParseStep.NeedInput) result = parser.Append(child.AsSpan(childSplit), [], true, Ct);
            if (result.Step == DtdParseStep.NeedInput) result = parser.Append(source.AsSpan(consumed), [], true, Ct);
            Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, result.Step);
            Assert.Equal(1, ledger.Enters); Assert.Equal(1, ledger.Exits);
            if (valid)
            {
                Assert.Null(result.Diagnostic); Assert.Single(parser.Model.ParameterEntities);
                if (prefix == "<!ENTITY e '") { var entity = Assert.Single(parser.Model.GeneralEntities); Assert.Equal("'", entity.ReplacementText); Assert.Equal(Parent, entity.Location.SourceUri); }
                else { var element = Assert.Single(parser.Model.Elements); Assert.Equal("r", element.Name); Assert.True(element.IsExternal); Assert.Equal(child.StartsWith("INCLUDE[", StringComparison.Ordinal) ? Child : Parent, element.Location.SourceUri); }
            }
            else Assert.NotNull(result.Diagnostic);
        }
    }
    [Theory]
    [InlineData("<!ENTITY % p '&#37;p;'><!ELEMENT r %p;>", "DTD.EntityCycle")]
    [InlineData("<!ENTITY % p 'EMPTY>'><!ELEMENT r %p;", null)]
    [InlineData("<!ENTITY % p \"'\"><!ENTITY e '%p;", "DTD.Syntax")]
    [InlineData("<!ENTITY % p \"<!ATTLIST r a CDATA 'x\">%p;'>", "DTD.Syntax")]
    public void InternalPlacementLiteralContainmentRecursionAndExpandedGrammar(string text, string? code)
    {
        for (int split = 0; split <= text.Length; split++)
        {
            using var p = new DtdParser(code == "DTD.EntityCycle" || text.Contains("<!ENTITY e", StringComparison.Ordinal) ? DtdSubsetKind.External : DtdSubsetKind.Internal, new() { EntityDepth = null });
            var a = p.Append(text.AsSpan(0, split), [], false, Ct);
            var b = a.Step == DtdParseStep.Rejected ? a : p.Append(text.AsSpan(split), [], true, Ct);
            Assert.Equal(DtdParseStep.Rejected, b.Step); if (code is not null) Assert.Equal(code, b.Diagnostic!.Code);
        }
    }
    [Theory]
    [InlineData("cancellation")]
    [InlineData("quota")]
    [InlineData("budget-fault")]
    public void SuspendedCrossSourceGrammarFailureBalancesAcceptedEntries(string failure)
    {
        var ledger = new DtdParserSourceTests.Ledger(); using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, new(Parent, 1, 1, null));
        Assert.Equal(DtdParseStep.NeedEntity, p.Append("<!ENTITY % p SYSTEM 'child'><!ELEMENT r %p;", [], true, Ct).Step); p.PushEntity(Child);
        if (failure == "cancellation") { using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); Assert.Throws<OperationCanceledException>(() => p.Append("EMPTY>", [], true, cancellation.Token)); }
        else if (failure == "budget-fault") { ledger.ChargeException = new IOException("owned fault"); Assert.Same(ledger.ChargeException, Assert.Throws<IOException>(() => p.Append("EMPTY>", [], true, Ct))); }
        else { ledger.MaxExpanded = ledger.Expanded; Assert.Equal("XML.ResourceLimit", p.Append("EMPTY>", [], true, Ct).Diagnostic!.Code); }
        Assert.Equal(1, ledger.Enters); Assert.Equal(1, ledger.Exits);
    }
}

using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8SchValidate;

public sealed class EvaluationTests
{
    internal const string Ns = "http://purl.oclc.org/dsdl/schematron";
    internal static SchematronSchema Compile(string body, string attributes = "") => SchematronSchema.Compile("<schema xmlns='" + Ns + "' " + attributes + ">" + body + "</schema>");
    internal static string Pattern(string checks, string context = "item", string attributes = "") => "<pattern " + attributes + "><rule context='" + context + "'>" + checks + "</rule></pattern>";

    [Fact]
    public void AssertionsAndReportsHaveDistinctStatusAndMetadata()
    {
        var schema = Compile("<title>Checks</title>" + Pattern("<assert id='required' role='' flag='f' test='@code'>Missing <name/></assert><report id='r' test='@code'>Present</report>"));
        var result = schema.Validate("<root><item/><item code='x'/></root>");
        Assert.True(result.Validation.IsComplete); Assert.False(result.Validation.IsValid);
        Assert.Equal("Checks", result.Title); Assert.Equal("#ALL", result.Phase);
        Assert.Equal(new[] { "SCH.Assert", "SCH.Report" }, result.Validation.Diagnostics.Select(d => d.Code));
        Assert.Equal(new[] { XmlDiagnosticSeverity.Error, XmlDiagnosticSeverity.Warning }, result.Validation.Diagnostics.Select(d => d.Severity));
        Assert.Equal(new[] { SchematronFindingKind.FailedAssert, SchematronFindingKind.SuccessfulReport }, result.Findings.Select(f => f.Kind));
        Assert.Equal("Missing item", result.Findings[0].Text); Assert.Equal("", result.Findings[0].Role); Assert.Equal("f", result.Findings[0].Flag);
        Assert.Equal(new[] { 0, 1 }, result.FiredRules.Select(e => e.Key)); Assert.Equal(new[] { 0, 1 }, result.Findings.Select(f => f.FiredRuleKey));
        Assert.Null(Assert.Single(result.ActivePatterns).Id);
        var reports = Compile(Pattern("<assert test='true()'/><report test='true()'>warning</report>"));
        Assert.True(reports.Validate("<item/>").Validation.IsValid);
        Assert.Empty(Compile(Pattern("<assert test='true()'/><report test='false()'/>" )).Validate("<item/>").Findings);
    }

    [Fact]
    public void PhasesAreSelectedPerRunInSchemaPatternOrder()
    {
        var schema = Compile("<phase id='selected'><let name='v' value='2'/><active pattern='second'/><active pattern='first'/></phase>" +
            "<phase id='other'><let name='v' value='3'/><active pattern='first'/></phase>" +
            Pattern("<report test='true()'><value-of select='$v'/></report>", attributes: "id='first'") +
            Pattern("<report test='true()'>second</report>", attributes: "id='second'") + Pattern("<report test='true()'>unnamed</report>"), "defaultPhase='selected'");
        var selected = schema.Validate("<item/>"); Assert.Equal("selected", selected.Phase);
        Assert.Equal(new[] { "first", "second" }, selected.ActivePatterns.Select(p => p.Id)); Assert.Equal("2", selected.Findings[0].Text);
        var other = schema.Validate("<item/>", new() { Phase = "other" }); Assert.Equal("3", Assert.Single(other.Findings).Text);
        Assert.Equal("selected", schema.Validate("<item/>", new() { Phase = "#DEFAULT" }).Phase);
        var all = schema.Validate("<item/>", new() { Phase = "#ALL" });
        Assert.False(all.Validation.IsComplete); Assert.Equal("SCH.XPath", all.Validation.Diagnostics[^1].Code);
        Assert.True(schema.Validate("<root/>", new() { Phase = "#ALL" }).Validation.IsValid); // missing phase-only variable is never evaluated
        Assert.Throws<ArgumentException>(() => schema.Validate("malformed", new() { Phase = "absent" }));
        Assert.Equal("#ALL", Compile(Pattern("<assert test='true()'/>" )).Validate("<item/>", new() { Phase = "#DEFAULT" }).Phase);
    }

    [Fact]
    public void TypedLetsDependOnOuterValuesAndShadowOnlyAfterInitializer()
    {
        var schema = Compile("<let name='x' value='1'/><let name='nodes' value='/root/item'/><let name='flag' value='true()'/><let name='word' value='&quot;typed&quot;'/>" +
            "<phase id='p'><let name='phase' value='$x+1'/><active pattern='a'/></phase>" +
            "<pattern id='a'><let name='x' value='$x+1'/><rule context='item'><let name='x' value='$x+1'/>" +
            "<assert test='$x=3 and $phase=2 and count($nodes)=2 and $flag and $word=&quot;typed&quot;'>wrong</assert>" +
            "<report test='true()' diagnostics='d' properties='prop'><emph>X</emph><value-of select='$x'/><span class='s'>S</span><dir value='ltr'>D</dir></report></rule></pattern>" +
            "<diagnostics><diagnostic id='d'><value-of select='$x'/></diagnostic></diagnostics><properties><property id='prop'><value-of select='$word'/></property></properties>", "defaultPhase='p'");
        var result = schema.Validate("<root><item/><item/></root>");
        Assert.True(result.Validation.IsValid); Assert.Equal(2, result.Findings.Count);
        foreach (var finding in result.Findings) { Assert.Equal("X3SD", finding.Text); Assert.Equal("3", Assert.Single(finding.Diagnostics).Text); Assert.Equal("typed", Assert.Single(finding.Properties).Text); }
    }

    [Fact]
    public void RuleFocusIsSingletonAndFirstMatchingRuleWinsInDocumentOrder()
    {
        var schema = Compile("<pattern id='p'><rule context='item[@special]'><assert test='false()'>special</assert></rule>" +
            "<rule context='item | @code | /'><report test='position()=1 and last()=1'><name/>:<value-of select='position()'/>/<value-of select='last()'/></report></rule>" +
            "<rule context='item'><assert test='false()'>must not fire</assert></rule></pattern>");
        var result = schema.Validate("<root code='a'><item/><item special='yes' code='b'/><item/></root>");
        Assert.Equal(new[] { ":1/1", "code:1/1", "item:1/1", "special", "code:1/1", "item:1/1" }, result.Findings.Select(f => f.Text));
        Assert.Equal(new[] { 1, 1, 1, 0, 1, 1 }, result.FiredRules.Select(f => f.RuleKey));
        Assert.Equal(6, result.FiredRules.Select(e => e.Key).Distinct().Count());
    }

    [Fact]
    public async Task SharedSchemaIsImmutableAcrossConcurrentDifferentPhasesAndInstances()
    {
        var source = XmlDocument.Parse("<schema xmlns='" + Ns + "'><phase id='a'><let name='v' value='1'/><active pattern='p'/></phase><phase id='b'><let name='v' value='2'/><active pattern='p'/></phase>" + Pattern("<report test='true()'><value-of select='$v'/></report>", attributes: "id='p'") + "</schema>", cancellationToken: TestContext.Current.CancellationToken);
        var schema = SchematronSchema.Compile(source); source.RootElement.SetTextContent("changed");
        await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() =>
        {
            var result = schema.Validate("<root><item/></root>", new() { Phase = i % 2 == 0 ? "a" : "b" });
            Assert.True(result.Validation.IsValid); Assert.Equal((i % 2 + 1).ToString(), Assert.Single(result.Findings).Text);
        }, TestContext.Current.CancellationToken)));
    }
}

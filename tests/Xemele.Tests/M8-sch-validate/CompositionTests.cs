using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8SchValidate;

public sealed class CompositionTests
{
    private sealed class Resolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default) => resolve(request);
    }
    private sealed class CountStream(string text) : MemoryStream(Encoding.UTF8.GetBytes(text))
    {
        internal int Disposals; protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }

    [Fact]
    public void PublicCompileComposesIncludeAbstractParametersExtendsAndFinalSourceLocations()
    {
        var uri = new Uri("https://example.test/redirect/pattern.sch");
        using var external = new CountStream("<pattern xmlns='" + EvaluationTests.Ns + "' abstract='true' id='template'>" +
            "<rule abstract='true' id='base'><report test='true()' subject='$subject'>included <name/></report></rule>" +
            "<rule context='$context'><extends rule='base'/></rule></pattern>");
        var requests = 0;
        var options = new SchematronCompileOptions
        {
            Schema = new()
            {
                BaseUri = new("https://example.test/main.sch"),
                Resolver = new Resolver(request =>
        {
            requests++;
            Assert.Equal(XmlResourceKind.Schema, request.Kind);
            Assert.Equal("pattern.sch", request.SystemId);
            return new(external, uri);
        })
            }
        };
        var source = "<schema xmlns='" + EvaluationTests.Ns + "'><include href='pattern.sch'/><pattern is-a='template'><param name='context' value='item'/><param name='subject' value='@code'/></pattern></schema>";
        var schema = SchematronSchema.Compile(source, options);
        Assert.Equal(1, requests);
        Assert.Equal(1, external.Disposals);
        var finding = Assert.Single(schema.Validate("<item code='x'/>").Findings);
        Assert.Equal("included item", finding.Text);
        Assert.Contains("/@*", finding.Location);
        using var invalidSubject = new CountStream("<pattern xmlns='" + EvaluationTests.Ns + "'><rule context='item'><report test='true()' subject='1'>x</report></rule></pattern>");
        var other = SchematronSchema.Compile("<schema xmlns='" + EvaluationTests.Ns + "'><include href='pattern.sch'/></schema>", options with
        {
            Schema = options.Schema with
            {
                Resolver = new Resolver(_ => new(invalidSubject, uri))
            }
        });
        var diagnostic = Assert.Single(other.Validate("<item/>").Validation.Diagnostics);
        Assert.Equal("SCH.XPath", diagnostic.Code);
        Assert.Equal(uri, diagnostic.Location.SourceUri);
        Assert.NotNull(diagnostic.Location.ByteOffset);
    }

    [Fact]
    public void IdenticalContextStringsRemainDistinctKeyedEventsAcrossPatterns()
    {
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<report test='true()'>one</report>", attributes: "id='first'") + EvaluationTests.Pattern("<report test='true()'>two</report>"));
        var result = schema.Validate("<item/>");
        Assert.Equal([0, 1], result.ActivePatterns.Select(p => p.Key));
        Assert.Equal([0, 1], result.FiredRules.Select(p => p.Key));
        Assert.Equal([0, 1], result.FiredRules.Select(p => p.RuleKey));
        Assert.Equal(result.FiredRules[0].Location, result.FiredRules[1].Location);
        Assert.Equal(["first", null], result.Findings.Select(f => f.PatternId));
        Assert.Equal([0, 1], result.Findings.Select(f => f.FiredRuleKey));
    }

    [Fact]
    public void DocumentAndAttributeFocusPreserveMessageLetTypesAndSubjects()
    {
        var schema = EvaluationTests.Compile("<pattern><rule context='/'><let name='root' value='/*'/><report test='count($root)=1' subject='$root'>root:<name/></report></rule>" +
            "<rule context='@code'><let name='owner' value='..'/><report test='count($owner)=1 and position()=1 and last()=1' subject='$owner'><name/>:<value-of select='.'/></report></rule></pattern>");
        var result = schema.Validate("<item code='x'/>");
        Assert.True(result.Validation.IsValid);
        Assert.Equal("/", result.Findings[0].ContextLocation);
        Assert.Equal("root:", result.Findings[0].Text);
        Assert.Equal("code:x", result.Findings[1].Text);
        Assert.Equal(result.Findings[0].Location, result.Findings[1].Location);
    }
}

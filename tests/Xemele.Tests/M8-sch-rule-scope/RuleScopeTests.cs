using System.Text;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8SchCompile.CompilerTests;

namespace Xemele.Tests.M8SchRuleScope;

public sealed class RuleScopeTests
{
    private const string Abstract = "<rule abstract='true' id='a'><assert test='true()'>ok</assert></rule>";
    private const string Extending = "<rule context='root'><extends rule='a'/></rule>";

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void OtherPatternRuleRejectsEvenWhenAbstractOrInactive(bool abstractPattern, bool inactive)
    {
        string phase = inactive ? "<phase id='only'><active pattern='owner'/></phase>" : "";
        string other = abstractPattern ? "id='other' abstract='true'" : "id='other'";
        string xml = Schema(phase + Pattern(Abstract, "id='owner'") + Pattern(Extending, other),
            inactive ? "defaultPhase='only'" : "");
        var error = Assert.Throws<SchemaCompileException>(() => Compile(xml));
        Assert.Equal("SCH.SchemaRestriction", error.Diagnostics[0].Code);
    }

    [Fact]
    public void UnusedAbstractRuleCannotExtendAcrossPatterns()
    {
        string xml = Schema(Pattern(Abstract) + Pattern("<rule abstract='true' id='b'><extends rule='a'/></rule>"));
        Assert.Equal("SCH.SchemaRestriction", Assert.Throws<SchemaCompileException>(() => Compile(xml)).Diagnostics[0].Code);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RejectionUsesExtendsCharacterOrByteLocation(bool bytes)
    {
        string xml = Schema(Pattern(Abstract) + "\n<pattern><rule context='root'>\n<extends rule='a'/></rule></pattern>");
        var options = new SchematronCompileOptions { Schema = new() { BaseUri = new("urn:scope:source") } };
        SchemaCompileException error;
        if (bytes)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            error = Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile(stream, options));
            Assert.Equal(xml.IndexOf("<extends", StringComparison.Ordinal), error.Diagnostics[0].Location.ByteOffset);
        }
        else
        {
            error = Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile(xml, options));
            Assert.Null(error.Diagnostics[0].Location.ByteOffset);
        }
        Assert.Equal("SCH.SchemaRestriction", error.Diagnostics[0].Code);
        Assert.Equal(new Uri("urn:scope:source"), error.Diagnostics[0].Location.SourceUri);
        Assert.Equal(3, error.Diagnostics[0].Location.LineNumber);
        Assert.Equal(1, error.Diagnostics[0].Location.LinePosition);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SamePatternChainsRemainValidIncludingSeparateTemplateInstances(bool template)
    {
        string body = Abstract + "<rule abstract='true' id='b'><extends rule='a'/><report test='false()'>none</report></rule>" +
            "<rule context='root'><extends rule='b'/></rule>";
        string xml = Schema(Pattern(body, template ? "abstract='true' id='template'" : "") +
            (template ? "<pattern is-a='template' id='one'/><pattern is-a='template' id='two'/>" : ""));
        var compiled = SchematronSchema.Compile(xml);
        var result = compiled.Validate("<root/>");
        Assert.True(result.Validation.IsComplete);
        Assert.True(result.Validation.IsValid);
        Assert.Equal(template ? 2 : 1, result.FiredRules.Count);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void IncludedRuleBelongsToEffectiveContainingPattern()
    {
        var options = new SchematronCompileOptions { Schema = new() { Resolver = new Resolver(), BaseUri = new("urn:scope:main") } };
        var compiled = Compile(Schema(Pattern("<include href='urn:scope:rule'/>" + Extending)), options);
        var check = Assert.Single(Assert.Single(Assert.Single(compiled.Patterns).Rules).Checks);
        Assert.Equal(new Uri("urn:scope:rule"), check.Location.SourceUri);
        Assert.NotNull(check.Location.ByteOffset);
    }

    private sealed class Resolver : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default) =>
            new(new MemoryStream(Encoding.UTF8.GetBytes(Abstract.Replace("<rule ", "<rule xmlns='" + Ns + "' ", StringComparison.Ordinal))), request.Uri);
    }
}

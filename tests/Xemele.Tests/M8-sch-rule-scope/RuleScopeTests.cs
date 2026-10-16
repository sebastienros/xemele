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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OtherPatternRuleRejectsEvenWhenAbstractOrInactive(bool abstractPattern, bool inactive)
    {
        var phase = inactive ? "<phase id='only'><active pattern='owner'/></phase>" : "";
        var other = abstractPattern ? "id='other' abstract='true'" : "id='other'";
        var xml = Schema(phase + Pattern(Abstract, "id='owner'") + Pattern(Extending, other),
            inactive ? "defaultPhase='only'" : "");
        var error = Assert.Throws<SchemaCompileException>(() => Compile(xml));
        Assert.Equal("SCH.SchemaRestriction", error.Diagnostics[0].Code);
    }

    [Fact]
    public void UnusedAbstractRuleCannotExtendAcrossPatterns()
    {
        var xml = Schema(Pattern(Abstract) + Pattern("<rule abstract='true' id='b'><extends rule='a'/></rule>"));
        Assert.Equal("SCH.SchemaRestriction", Assert.Throws<SchemaCompileException>(() => Compile(xml)).Diagnostics[0].Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectionUsesExtendsCharacterOrByteLocation(bool bytes)
    {
        var xml = Schema(Pattern(Abstract) + "\n<pattern><rule context='root'>\n<extends rule='a'/></rule></pattern>");
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
    [InlineData(false)]
    [InlineData(true)]
    public void SamePatternChainsRemainValidIncludingSeparateTemplateInstances(bool template)
    {
        var body = Abstract + "<rule abstract='true' id='b'><extends rule='a'/><report test='false()'>none</report></rule>" +
            "<rule context='root'><extends rule='b'/></rule>";
        var xml = Schema(Pattern(body, template ? "abstract='true' id='template'" : "") +
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

    [Fact]
    public void ScopeChecksShareExactWorkBudgetAndCancellation()
    {
        var preceding = string.Concat(Enumerable.Repeat("<rule context='other'><assert test='true()'/></rule>", 80));
        var document = XmlDocument.Parse(Schema(Pattern(preceding + Abstract + Extending)), cancellationToken: TestContext.Current.CancellationToken);
        var counting = new Counting(new SchemaCompileOperation());
        new SchematronCompiler().Compile(document, new(), counting);
        Assert.True(counting.Work > 80);
        var exact = new SchematronCompileOptions { Schema = new() { MaxOperations = counting.Work } };
        new SchematronCompiler().Compile(document, exact, new SchemaCompileOperation(exact.Schema));
        var under = exact with
        {
            Schema = exact.Schema with
            {
                MaxOperations = counting.Work - 1
            }
        };
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() =>
            new SchematronCompiler().Compile(document, under, new SchemaCompileOperation(under.Schema))).Diagnostics[^1].Code);
        using var cancellation = new CancellationTokenSource();
        var stopped = new Counting(new SchemaCompileOperation(new()
        {
            CancellationToken = cancellation.Token
        }), cancellation, counting.Work / 2);
        Assert.Throws<OperationCanceledException>(() => new SchematronCompiler().Compile(document, new(), stopped));
    }

    private sealed class Counting(ISchemaOperation inner, CancellationTokenSource? cancellation = null, long stop = long.MaxValue) : ISchemaOperation
    {
        internal long Work;
        public XmlResourceLimits Limits => inner.Limits;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete;
        public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation location)
        {
            Work += work;
            if (Work >= stop)
            {
                cancellation?.Cancel();
            }

            return inner.Charge(work, location);
        }
        public bool ChargeComponents(int count, XmlLocation location) => inner.ChargeComponents(count, location);
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
        public void Refuse(string code, string message, XmlLocation location) => inner.Refuse(code, message, location);
    }

    private sealed class Resolver : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default) =>
            new(new MemoryStream(Encoding.UTF8.GetBytes(Abstract.Replace("<rule ", "<rule xmlns='" + Ns + "' ", StringComparison.Ordinal))), request.Uri);
    }
}

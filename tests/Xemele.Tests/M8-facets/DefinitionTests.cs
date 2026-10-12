using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8Facets.FacetTests;
namespace Xemele.Tests.M8Facets;

public sealed class DefinitionTests
{
    [Theory]
    [InlineData(XmlFacetKind.Length, "2", "3")][InlineData(XmlFacetKind.MinLength, "3", "2")]
    [InlineData(XmlFacetKind.MaxLength, "3", "4")][InlineData(XmlFacetKind.TotalDigits, "3", "4")]
    [InlineData(XmlFacetKind.FractionDigits, "3", "4")]
    [InlineData(XmlFacetKind.MinInclusive, "3", "2")][InlineData(XmlFacetKind.MaxInclusive, "3", "4")]
    [InlineData(XmlFacetKind.MinExclusive, "3", "2")][InlineData(XmlFacetKind.MaxExclusive, "3", "4")]
    public void InheritedScalarFacetsCannotWeaken(object raw, string before, string after)
    {
        var kind = (XmlFacetKind)raw;
        string parentType = kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength ? "string" : "decimal";
        var parent = Restrict("parent", parentType, F(kind, before));
        Bad(Restrict("child", parentType, F(kind, after)) with { BaseType = parent.Name }, parent);
    }
    [Theory]
    [InlineData(XmlFacetKind.Length, "2", "3")][InlineData(XmlFacetKind.MinLength, "2", "3")]
    [InlineData(XmlFacetKind.MaxLength, "3", "2")][InlineData(XmlFacetKind.TotalDigits, "3", "2")]
    [InlineData(XmlFacetKind.FractionDigits, "3", "2")]
    [InlineData(XmlFacetKind.MinInclusive, "3", "4")][InlineData(XmlFacetKind.MaxInclusive, "3", "2")]
    [InlineData(XmlFacetKind.MinExclusive, "3", "4")][InlineData(XmlFacetKind.MaxExclusive, "3", "2")]
    public void FixedInheritedFacetCannotChange(object raw, string before, string after)
    {
        var kind = (XmlFacetKind)raw; string primitive = kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength ? "string" : "decimal";
        var parent = Restrict("parent", primitive, F(kind, before, true));
        Bad(Restrict("child", primitive, F(kind, after)) with { BaseType = parent.Name }, parent);
    }
    [Fact]
    public void FixedEqualityUsesTypedCanonicalValueRatherThanLexicalSpelling()
    {
        var parent = Restrict("parent", "decimal", F(XmlFacetKind.MinInclusive, "1.00", true));
        var child = Restrict("child", "decimal", F(XmlFacetKind.MinInclusive, "+01.0")) with { BaseType = parent.Name };
        Valid(child, "2", null, parent);
    }
    [Fact]
    public void FixedRestrictionIsStillCheckedThroughAnIntermediateType()
    {
        var parent = Restrict("a", "string", F(XmlFacetKind.Length, "2", true));
        var middle = Restrict("b", "string") with { BaseType = parent.Name };
        Bad(Restrict("c", "string", F(XmlFacetKind.Length, "3")) with { BaseType = middle.Name }, parent, middle);
    }
    [Theory]
    [InlineData(XmlFacetKind.Length, "2", XmlFacetKind.MinLength, "3")]
    [InlineData(XmlFacetKind.Length, "2", XmlFacetKind.MaxLength, "1")]
    [InlineData(XmlFacetKind.MinLength, "2", XmlFacetKind.MaxLength, "1")]
    [InlineData(XmlFacetKind.TotalDigits, "2", XmlFacetKind.FractionDigits, "3")]
    [InlineData(XmlFacetKind.MinInclusive, "2", XmlFacetKind.MaxInclusive, "1")]
    [InlineData(XmlFacetKind.MinInclusive, "2", XmlFacetKind.MaxExclusive, "2")]
    [InlineData(XmlFacetKind.MinExclusive, "2", XmlFacetKind.MaxInclusive, "2")]
    [InlineData(XmlFacetKind.MinInclusive, "1", XmlFacetKind.MinExclusive, "2")]
    [InlineData(XmlFacetKind.MaxInclusive, "1", XmlFacetKind.MaxExclusive, "2")]
    public void ConflictingFacetsReject(object a, string x, object b, string y)
    {
        var kind = (XmlFacetKind)a;
        Bad(Restrict("conflict", kind is XmlFacetKind.Length or XmlFacetKind.MinLength ? "string" : "decimal", F(kind, x), F((XmlFacetKind)b, y)));
    }
    [Fact]
    public void CombinedBoundsAndLengthsPermitTheirExactSharedEndpoint()
    {
        Valid(Restrict("range", "decimal", F(XmlFacetKind.MinInclusive, "2"), F(XmlFacetKind.MaxInclusive, "2")), "2.0");
        Valid(Restrict("length", "string", F(XmlFacetKind.Length, "2"), F(XmlFacetKind.MinLength, "1"), F(XmlFacetKind.MaxLength, "3")), "ab");
    }
    [Theory]
    [InlineData(XmlFacetKind.MinExclusive)][InlineData(XmlFacetKind.MaxExclusive)]
    public void RepeatedInheritedExclusiveEndpointIsAllowed(object raw)
    {
        var kind = (XmlFacetKind)raw; var parent = Restrict("parent", "decimal", F(kind, "1"));
        var child = Restrict("child", "decimal", F(kind, "1.0")) with { BaseType = parent.Name };
        Valid(child, kind == XmlFacetKind.MinExclusive ? "2" : "0", null, parent); Invalid(child, "1", parent);
        var inclusive = Restrict("inclusive", "decimal", F(kind == XmlFacetKind.MinExclusive ? XmlFacetKind.MinInclusive : XmlFacetKind.MaxInclusive, "1")) with { BaseType = parent.Name };
        Bad(inclusive, parent);
    }
    [Fact]
    public void IntrinsicIntegerFractionDigitsAndRangesAreInherited()
    {
        Bad(Restrict("fraction", "integer", F(XmlFacetKind.FractionDigits, "1")));
        Bad(Restrict("overflow", "byte", F(XmlFacetKind.MaxInclusive, "128")));
        var type = Restrict("positivebyte", "byte", F(XmlFacetKind.MinInclusive, "1"));
        Valid(type, "127"); Invalid(type, "128"); Invalid(type, "0"); Invalid(type, "1.0");
    }
    [Theory]
    [InlineData("normalizedString", "preserve")][InlineData("token", "replace")][InlineData("decimal", "replace")]
    public void WhitespaceWeakeningRejects(string parent, string rule) => Bad(Restrict("weak", parent, F(XmlFacetKind.WhiteSpace, rule)));
    [Fact]
    public void FixedWhitespaceRemainsFixedInFurtherRestrictions()
    {
        var parent = Restrict("parent", "string", F(XmlFacetKind.WhiteSpace, "replace", true));
        Bad(Restrict("child", "string", F(XmlFacetKind.WhiteSpace, "collapse")) with { BaseType = parent.Name }, parent);
    }
    [Fact]
    public void EnumerationsNarrowAtEveryLevelAndPatternsUseFinalWhitespace()
    {
        var parent = Restrict("a", "string", F(XmlFacetKind.Pattern, "a b"), F(XmlFacetKind.Enumeration, "a b"), F(XmlFacetKind.Enumeration, "c"));
        var child = Restrict("b", "string", F(XmlFacetKind.WhiteSpace, "collapse"), F(XmlFacetKind.Enumeration, " a\tb ")) with { BaseType = parent.Name };
        Valid(child, " a\tb ", null, parent); Invalid(child, "c", parent);
        Bad(Restrict("bad", "string", F(XmlFacetKind.Enumeration, "d")) with { BaseType = parent.Name }, parent);
    }
    [Fact]
    public void TemporalAndDurationConflictsDoNotInventAnOrder()
    {
        Bad(Restrict("tz", "dateTime", F(XmlFacetKind.MinInclusive, "2000-01-01T00:00:00"), F(XmlFacetKind.MaxInclusive, "2000-01-01T00:00:00Z")));
        Bad(Restrict("duration", "duration", F(XmlFacetKind.MinInclusive, "P1M"), F(XmlFacetKind.MaxInclusive, "P30D")));
        Invalid(Restrict("duration", "duration", F(XmlFacetKind.MinInclusive, "P1M")), "P30D");
    }
    [Theory]
    [InlineData(XmlFacetKind.Length)][InlineData(XmlFacetKind.MinLength)][InlineData(XmlFacetKind.MaxLength)]
    [InlineData(XmlFacetKind.TotalDigits)][InlineData(XmlFacetKind.FractionDigits)]
    [InlineData(XmlFacetKind.WhiteSpace)][InlineData(XmlFacetKind.MinInclusive)][InlineData(XmlFacetKind.MaxExclusive)]
    public void RepeatedScalarFacetsReject(object raw)
    {
        var kind = (XmlFacetKind)raw; string parent = kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength or XmlFacetKind.WhiteSpace ? "string" : "decimal";
        string text = kind == XmlFacetKind.WhiteSpace ? "preserve" : "1";
        Bad(Restrict("dup", parent, F(kind, text), F(kind, text)));
    }
    [Fact]
    public void InvalidRegexNeverPublishesAPlan()
    {
        var type = Restrict("regex", "string", F(XmlFacetKind.Pattern, "["));
        var registry = new XmlDatatypeRegistry([type], Op()); var e = new XmlFacetEvaluator();
        Assert.Throws<SchemaCompileException>(() => e.ValidateDefinition(type, registry, Op()));
        Assert.Throws<SchemaCompileException>(() => e.TryValidate(type, "a", Context, registry, Op(), out _, out _));
    }
}

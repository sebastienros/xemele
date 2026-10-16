using Xunit;
using Xemele.Validation;
namespace Xemele.Tests.M8Facets;

public sealed class FacetTests
{
    internal static readonly SchemaNamespaceContext Context = new(new Dictionary<string, string> { ["p"] = "urn:values", ["q"] = "urn:values" }, "urn:default", null);
    internal static SchemaCompileOperation Op(long? work = null, int? components = null) => new(new() { MaxOperations = work, MaxComponents = components, Limits = new() { EvaluationWork = null }, CancellationToken = TestContext.Current.CancellationToken });
    internal static XmlFacet F(XmlFacetKind kind, string value, bool fix = false, SchemaNamespaceContext? context = null) => new(kind, value, fix, context ?? Context, default);
    internal static XmlDatatypeDefinition Restrict(string name, string parent, params XmlFacet[] facets) => new(new("urn:test", name), XmlDatatypeVariety.Atomic, DatatypeTypes.Name(parent), null, [], facets, [], false, default);
    internal static (XmlFacetEvaluator, XmlDatatypeRegistry) Ready(XmlDatatypeDefinition type, params XmlDatatypeDefinition[] definitions)
    {
        var registry = new XmlDatatypeRegistry(type.Name.NamespaceUri == DatatypeTypes.RngNamespace ? definitions : definitions.Append(type), Op());
        var evaluator = new XmlFacetEvaluator();
        var op = Op();
        Assert.True(evaluator.ValidateDefinition(type, registry, op), string.Join("; ", op.Diagnostics.Select(d => d.Message)));
        return (evaluator, registry);
    }
    internal static XmlDatatypeValue Valid(XmlDatatypeDefinition type, string lexical, SchemaNamespaceContext? context = null, params XmlDatatypeDefinition[] definitions)
    {
        var (e, r) = Ready(type, definitions);
        Assert.True(e.TryValidate(type, lexical, context ?? Context, r, Op(), out var value, out var error), error?.Message);
        Assert.Null(error);
        Assert.NotNull(value);
        Assert.Equal(type.Name, value.TypeName);
        return value;
    }
    internal static void Invalid(XmlDatatypeDefinition type, string lexical, params XmlDatatypeDefinition[] definitions)
    {
        var (e, r) = Ready(type, definitions);
        var op = Op();
        Assert.False(e.TryValidate(type, lexical, Context, r, op, out var value, out var error));
        Assert.Null(value);
        Assert.NotNull(error);
        Assert.True(op.IsComplete);
    }
    internal static void Bad(XmlDatatypeDefinition type, params XmlDatatypeDefinition[] definitions)
    {
        var r = new XmlDatatypeRegistry(type.Name.NamespaceUri == DatatypeTypes.RngNamespace ? definitions : definitions.Append(type), Op());
        var op = Op();
        Assert.False(new XmlFacetEvaluator().ValidateDefinition(type, r, op));
        Assert.True(op.IsComplete);
        Assert.Contains(op.Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    [Theory]
    [InlineData("string", "根😀", "2", true)]
    [InlineData("string", "根😀", "3", false)]
    [InlineData("hexBinary", "00aAFF", "3", true)]
    [InlineData("hexBinary", "00aAFF", "6", false)]
    [InlineData("base64Binary", "YWI=", "2", true)]
    [InlineData("base64Binary", "YWI=", "4", false)]
    [InlineData("anyURI", "根😀", "2", true)]
    [InlineData("QName", "p:longname", "0", true)]
    [InlineData("NOTATION", "p:longname", "999", true)]
    public void LengthUsesTheRequiredUnit(string parent, string input, string length, bool valid)
    {
        var type = Restrict("unit", parent, F(XmlFacetKind.Length, length));
        if (valid)
        {
            Valid(type, input);
        }
        else
        {
            Invalid(type, input);
        }
    }
    [Theory]
    [InlineData(XmlFacetKind.MinLength, "2", "a", false)]
    [InlineData(XmlFacetKind.MinLength, "2", "ab", true)]
    [InlineData(XmlFacetKind.MaxLength, "2", "abc", false)]
    [InlineData(XmlFacetKind.MaxLength, "2", "ab", true)]
    [InlineData(XmlFacetKind.Length, "0", "", true)]
    public void LengthBoundaryIsInclusive(object rawKind, string limit, string input, bool valid)
    {
        var kind = (XmlFacetKind)rawKind;
        var type = Restrict("length", "string", F(kind, limit));
        if (valid)
        {
            Valid(type, input);
        }
        else
        {
            Invalid(type, input);
        }
    }
    [Theory]
    [InlineData(XmlFacetKind.MinInclusive, "1", "1.00", true)]
    [InlineData(XmlFacetKind.MinInclusive, "1", ".999", false)]
    [InlineData(XmlFacetKind.MinExclusive, "1", "1.00", false)]
    [InlineData(XmlFacetKind.MinExclusive, "1", "1.001", true)]
    [InlineData(XmlFacetKind.MaxInclusive, "1", "1.00", true)]
    [InlineData(XmlFacetKind.MaxInclusive, "1", "1.001", false)]
    [InlineData(XmlFacetKind.MaxExclusive, "1", "1.00", false)]
    [InlineData(XmlFacetKind.MaxExclusive, "1", ".999", true)]
    public void NumericBoundariesUseTypedOrder(object rawKind, string limit, string input, bool valid)
    {
        var kind = (XmlFacetKind)rawKind;
        var type = Restrict("bound", "decimal", F(kind, limit));
        if (valid)
        {
            Valid(type, input);
        }
        else
        {
            Invalid(type, input);
        }
    }
    [Theory]
    [InlineData(XmlFacetKind.TotalDigits, "3", ".001", true)]
    [InlineData(XmlFacetKind.TotalDigits, "2", ".001", false)]
    [InlineData(XmlFacetKind.TotalDigits, "1", "1.00", true)]
    [InlineData(XmlFacetKind.TotalDigits, "1", "0", true)]
    [InlineData(XmlFacetKind.TotalDigits, "4", "1000.00", true)]
    [InlineData(XmlFacetKind.TotalDigits, "3", "1000.00", false)]
    [InlineData(XmlFacetKind.FractionDigits, "3", ".001", true)]
    [InlineData(XmlFacetKind.FractionDigits, "2", ".001", false)]
    [InlineData(XmlFacetKind.FractionDigits, "0", "1.00", true)]
    [InlineData(XmlFacetKind.FractionDigits, "0", "1.01", false)]
    public void CanonicalDigitsIgnoreInsignificantZeros(object rawKind, string limit, string input, bool valid)
    {
        var kind = (XmlFacetKind)rawKind;
        var type = Restrict("digits", "decimal", F(kind, limit));
        if (valid)
        {
            Valid(type, input);
        }
        else
        {
            Invalid(type, input);
        }
    }
    [Theory]
    [InlineData("preserve", "a\tb", "a\tb")]
    [InlineData("replace", "a\tb", "a b")]
    [InlineData("collapse", " \ta\r\nb \n", "a b")]
    public void WhitespaceIsAppliedBeforePatternsAndOwnedValues(string policy, string input, string expected)
    {
        var type = Restrict("ws", "string", F(XmlFacetKind.WhiteSpace, policy), F(XmlFacetKind.Enumeration, expected));
        var (e, r) = Ready(type);
        Assert.True(e.TryValidate(type, input, Context, r, Op(), out var value, out _));
        var lib = r.Find(DatatypeTypes.XsdNamespace)!;
        Assert.True(lib.TryParse(DatatypeTypes.Name("string"), expected, Context, Op(), out var text, out _));
        Assert.True(lib.ValueEquals(value!, text!, Op()));
    }
    [Fact]
    public void SameLevelPatternsAreAlternativesAndInheritedLevelsConjoin()
    {
        var parent = Restrict("parent", "string", F(XmlFacetKind.Pattern, "a.*"), F(XmlFacetKind.Pattern, "b.*"));
        var child = Restrict("child", "string", F(XmlFacetKind.Pattern, ".*z")) with
        {
            BaseType = parent.Name
        };
        Valid(child, "az", null, parent);
        Valid(child, "bz", null, parent);
        Invalid(child, "cz", parent);
        Invalid(child, "ax", parent);
    }
    [Theory]
    [InlineData("decimal", "1.00", "1")]
    [InlineData("float", "NaN", "NaN")]
    [InlineData("double", "-0", "0")]
    public void EnumerationUsesValueEquality(string parent, string lexical, string enumerated)
    {
        Valid(Restrict("enum", parent, F(XmlFacetKind.Enumeration, enumerated)), lexical);
    }
    [Fact]
    public void EnumerationRetainsItsOwnNamespaceContext()
    {
        var authored = new SchemaNamespaceContext(new Dictionary<string, string> { ["p"] = "urn:authored" }, "urn:authored-default", null);
        var instance = new SchemaNamespaceContext(new Dictionary<string, string> { ["q"] = "urn:authored", ["p"] = "urn:other" }, "urn:authored-default", null);
        var type = Restrict("qname", "QName", F(XmlFacetKind.Enumeration, "p:a", context: authored), F(XmlFacetKind.Enumeration, "b", context: authored));
        Valid(type, "q:a", instance);
        Valid(type, "b", instance);
        var (e, r) = Ready(type);
        Assert.False(e.TryValidate(type, "p:a", instance, r, Op(), out _, out _));
    }
    [Theory]
    [InlineData("2000-01-02T00:00:00Z", true)]
    [InlineData("2000-01-01T00:00:00", false)]
    [InlineData("1999-12-31T00:00:00Z", false)]
    public void TimezoneAbsenceRemainsUnordered(string input, bool valid)
    {
        var type = Restrict("date", "dateTime", F(XmlFacetKind.MinInclusive, "2000-01-01T00:00:00Z"));
        if (valid)
        {
            Valid(type, input);
        }
        else
        {
            Invalid(type, input);
        }
    }
    [Theory]
    [InlineData(XmlFacetKind.Length, "-1")]
    [InlineData(XmlFacetKind.MinLength, "1.0")]
    [InlineData(XmlFacetKind.MaxLength, "x")]
    [InlineData(XmlFacetKind.TotalDigits, "0")]
    [InlineData(XmlFacetKind.FractionDigits, "-1")]
    [InlineData(XmlFacetKind.WhiteSpace, "trim")]
    [InlineData(XmlFacetKind.MaxInclusive, "NaNx")]
    public void FacetLexicalFormsReject(object rawKind, string lexical)
    {
        var kind = (XmlFacetKind)rawKind;
        Bad(Restrict("bad", kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength or XmlFacetKind.WhiteSpace ? "string" : "decimal", F(kind, lexical)));
    }
    [Theory]
    [InlineData("boolean", XmlFacetKind.Length)]
    [InlineData("string", XmlFacetKind.MaxInclusive)]
    [InlineData("float", XmlFacetKind.TotalDigits)]
    [InlineData("date", XmlFacetKind.FractionDigits)]
    [InlineData("QName", XmlFacetKind.MinInclusive)]
    public void InapplicableFacetsReject(string parent, object rawKind) => Bad(Restrict("bad", parent, F((XmlFacetKind)rawKind, "1")));
    [Fact]
    public void ArbitraryFacetIntegerAndLexicalSignsAreSupported()
    {
        Valid(Restrict("big", "string", F(XmlFacetKind.MaxLength, "999999999999999999999999999999999999")), "a");
        Valid(Restrict("zero", "string", F(XmlFacetKind.Length, " -000 ")), "");
        Valid(Restrict("plus", "string", F(XmlFacetKind.Length, " +0002 ")), "ab");
    }
}

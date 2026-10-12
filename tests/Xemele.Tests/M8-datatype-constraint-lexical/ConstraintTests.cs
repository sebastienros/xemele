using System.Globalization;
using Xunit;
using Xemele.Validation;
namespace Xemele.Tests.M8ConstraintLexical;

public sealed class ConstraintTests
{
    internal static readonly SchemaNamespaceContext Context = new(new Dictionary<string, string> { ["p"] = "urn:declared" }, "urn:default", null);
    internal static SchemaCompileOperation Op(long? work = null, int? components = null) => new(new() { MaxOperations = work, MaxComponents = components, Limits = new() { EvaluationWork = null }, CancellationToken = TestContext.Current.CancellationToken });
    internal static XmlDatatypeDefinition Restrict(string name, string parent, params XmlFacet[] facets) => new(new("urn:constraint", name), XmlDatatypeVariety.Atomic, DatatypeTypes.Name(parent), null, [], facets, [], false, default);
    internal static XmlFacet F(XmlFacetKind kind, string text) => new(kind, text, false, Context, default);
    internal static DatatypeConstraintRepresentation Create(string name, string authored, out XmlDatatypeRegistry r)
    {
        r = new XmlDatatypeRegistry(); var type = r.FindType(DatatypeTypes.Name(name))!;
        Assert.True(DatatypeConstraintRepresentation.TryCreate(type, authored, Context, r, new XmlFacetEvaluator(), Op(), out var result, out var error), error?.Message);
        Assert.Null(error); return result!;
    }
    [Theory]
    [InlineData("int", " +001 ", "1")][InlineData("integer", "-000000", "0")]
    [InlineData("decimal", "1", "1.0")][InlineData("decimal", "-.0001000", "-0.0001")]
    [InlineData("decimal", "000123.45000", "123.45")][InlineData("decimal", "-0.00", "0.0")]
    [InlineData("boolean", "1", "true")][InlineData("boolean", "0", "false")]
    [InlineData("hexBinary", "00aBcD", "00ABCD")][InlineData("base64Binary", " Y W J j ", "YWJj")]
    [InlineData("base64Binary", "Y Q = =", "YQ==")][InlineData("base64Binary", "Y W I =", "YWI=")]
    [InlineData("hexBinary", "", "")][InlineData("base64Binary", "", "")]
    [InlineData("string", " a\tb ", " a\tb ")][InlineData("token", " a\tb ", "a b")]
    [InlineData("anyURI", " ../relative?q=1 ", "../relative?q=1")]
    [InlineData("QName", " p:local ", "p:local")][InlineData("NOTATION", " p:local ", "p:local")]
    [InlineData("duration", " P001Y00M ", "P001Y00M")][InlineData("gYear", "2000+00:00", "2000+00:00")]
    [InlineData("gYearMonth", "2000-12-14:00", "2000-12-14:00")][InlineData("gMonth", "--12+00:00", "--12+00:00")]
    [InlineData("gMonthDay", "--02-29+13:00", "--02-29+13:00")][InlineData("gDay", "---01-00:00", "---01-00:00")]
    public void ExactPrescribedAndFallbackFormsRoundTrip(string type, string input, string expected)
    {
        var rep = Create(type, input, out var registry); Assert.Equal(expected, rep.Lexical);
        Assert.Same(Context, rep.Context);
        var library = registry.Find(DatatypeTypes.XsdNamespace)!;
        Assert.True(library.TryParse(DatatypeTypes.Name(type), expected, rep.Context, Op(), out var parsed, out _));
        Assert.True(library.ValueEquals(rep.DeclaredValue, parsed!, Op()));
    }
    [Theory]
    [InlineData("float", "1", "1.0E0")][InlineData("float", "0.1", "1.0E-1")]
    [InlineData("float", "-0", "0.0E0")][InlineData("double", "-0.0", "0.0E0")]
    [InlineData("double", "12300", "1.23E4")][InlineData("double", ".00000123", "1.23E-6")]
    [InlineData("double", "INF", "INF")][InlineData("float", "-INF", "-INF")][InlineData("double", "NaN", "NaN")]
    [InlineData("float", "1.401298464324817e-45", "1.0E-45")]
    [InlineData("double", "4.9406564584124654e-324", "5.0E-324")]
    [InlineData("float", "3.4028234663852886e38", "3.4028235E38")]
    [InlineData("double", "1.7976931348623157e308", "1.7976931348623157E308")]
    [InlineData("float", "1.000000059604644775390625", "1.0E0")]
    [InlineData("float", "9.9999999", "1.0E1")]
    public void FloatingFormsUseTheSelectedPrecision(string type, string input, string expected) => ExactPrescribedAndFallbackFormsRoundTrip(type, input, expected);

    [Theory]
    [InlineData("dateTime", "2000-02-28T24:00:00.000", "2000-02-29T00:00:00")]
    [InlineData("dateTime", "2000-03-01T00:30:00.123400+01:00", "2000-02-29T23:30:00.1234Z")]
    [InlineData("dateTime", "1900-03-01T00:00:00+14:00", "1900-02-28T10:00:00Z")]
    [InlineData("dateTime", "-0001-12-31T24:00:00Z", "0001-01-01T00:00:00Z")]
    [InlineData("dateTime", "0001-01-01T00:00:00+14:00", "-0001-12-31T10:00:00Z")]
    [InlineData("dateTime", "1999-12-31T23:59:60.12300Z", "1999-12-31T23:59:60.123Z")]
    [InlineData("dateTime", "2000-01-01T12:00:60Z", "2000-01-01T12:01:00Z")]
    [InlineData("dateTime", "1999-12-31T23:59:60+01:00", "1999-12-31T23:00:00Z")]
    [InlineData("time", "24:00:00", "00:00:00")][InlineData("time", "00:30:00+01:00", "23:30:00Z")]
    [InlineData("time", "23:59:60.500Z", "23:59:60.5Z")][InlineData("time", "12:00:00.000", "12:00:00")]
    [InlineData("date", "2002-10-10+13:00", "2002-10-09-11:00")]
    [InlineData("date", "2002-10-10-12:00", "2002-10-11+12:00")]
    [InlineData("date", "2002-10-10+12:00", "2002-10-10+12:00")]
    [InlineData("date", "2002-10-10-11:59", "2002-10-10-11:59")]
    [InlineData("date", "2000-02-28-14:00", "2000-02-29+10:00")]
    [InlineData("date", "-0001-12-31-14:00", "0001-01-01+10:00")]
    [InlineData("date", "0001-01-01+14:00", "-0001-12-31-10:00")]
    [InlineData("date", "2000-01-01-00:00", "2000-01-01Z")]
    [InlineData("date", "2000-01-01", "2000-01-01")]
    public void TemporalBoundariesKeepTheSupportedValue(string type, string input, string expected) => ExactPrescribedAndFallbackFormsRoundTrip(type, input, expected);

    [Fact]
    public void ActualTypeAssessesCanonicalLexicalAndFixedComparisonKeepsDeclaredValue()
    {
        var small = Restrict("Small", "int", F(XmlFacetKind.Pattern, "[0-9]"));
        var r = new XmlDatatypeRegistry([small], Op()); var e = new XmlFacetEvaluator();
        Assert.True(DatatypeConstraintRepresentation.TryCreate(r.FindType(DatatypeTypes.Name("int"))!, "01", Context, r, e, Op(), out var integer, out _));
        Assert.Equal("1", integer!.Lexical);
        Assert.True(e.TryValidate(small, integer.Lexical, integer.Context, r, Op(), out var actual, out _));
        Assert.Equal(small.Name, actual!.TypeName);
        Assert.True(r.Find(DatatypeTypes.XsdNamespace)!.ValueEquals(integer.DeclaredValue, actual, Op()));
        Assert.True(DatatypeConstraintRepresentation.TryCreate(r.FindType(DatatypeTypes.Name("decimal"))!, "1", Context, r, e, Op(), out var dec, out _));
        Assert.Equal("1.0", dec!.Lexical);
        Assert.False(e.TryValidate(r.FindType(DatatypeTypes.Name("int"))!, dec.Lexical, dec.Context, r, Op(), out _, out _));
        Assert.True(r.Find(DatatypeTypes.XsdNamespace)!.ValueEquals(integer.DeclaredValue, dec.DeclaredValue, Op()));
    }
    [Fact]
    public void DeclarationFacetsMustPassBeforeFormattingAndDecimalRestrictionIsNotIntegerAncestry()
    {
        var one = Restrict("one", "int", F(XmlFacetKind.Pattern, "0[0-9]"));
        var decimalZero = Restrict("decimalZero", "decimal", F(XmlFacetKind.FractionDigits, "0"));
        var r = new XmlDatatypeRegistry([one, decimalZero], Op()); var e = new XmlFacetEvaluator();
        Assert.False(DatatypeConstraintRepresentation.TryCreate(one, "1", Context, r, e, Op(), out var bad, out _)); Assert.Null(bad);
        Assert.True(DatatypeConstraintRepresentation.TryCreate(one, "01", Context, r, e, Op(), out var good, out _)); Assert.Equal("1", good!.Lexical);
        Assert.True(DatatypeConstraintRepresentation.TryCreate(decimalZero, "001", Context, r, e, Op(), out var dec, out _)); Assert.Equal("1.0", dec!.Lexical);
    }
    [Fact]
    public void CapturedQNamePrefixAndNamespaceRemainOwnedAndAreUsedForAssessment()
    {
        var prefixes = new Dictionary<string, string> { ["p"] = "urn:declared" }; var context = new SchemaNamespaceContext(prefixes, "urn:default", null);
        var r = new XmlDatatypeRegistry(); var e = new XmlFacetEvaluator(); var type = r.FindType(DatatypeTypes.Name("QName"))!;
        char[] input = " p:a ".ToCharArray();
        Assert.True(DatatypeConstraintRepresentation.TryCreate(type, input, context, r, e, Op(), out var rep, out _));
        prefixes["p"] = "urn:instance"; Array.Fill(input, '!'); Assert.Equal("p:a", rep!.Lexical);
        var lib = r.Find(DatatypeTypes.XsdNamespace)!;
        Assert.True(e.TryValidate(type, rep.Lexical, rep.Context, r, Op(), out var schemaValue, out _));
        Assert.True(lib.ValueEquals(rep.DeclaredValue, schemaValue!, Op()));
        Assert.True(e.TryValidate(type, rep.Lexical, new(prefixes, "", null), r, Op(), out var instanceValue, out _));
        Assert.False(lib.ValueEquals(rep.DeclaredValue, instanceValue!, Op()));
        Assert.False(DatatypeConstraintRepresentation.TryCreate(type, "x:a", context, r, e, Op(), out _, out _));
    }
    [Fact]
    public void ListsAndNestedSelectedUnionsPreserveIdentityAndTokenAssociation()
    {
        var u = new XmlDatatypeDefinition(new("urn:constraint", "u"), XmlDatatypeVariety.Union, DatatypeTypes.Name("anySimpleType"), null, [DatatypeTypes.Name("boolean"), DatatypeTypes.Name("decimal")], [], [], false, default);
        var list = new XmlDatatypeDefinition(new("urn:constraint", "list"), XmlDatatypeVariety.List, DatatypeTypes.Name("anySimpleType"), u.Name, [], [], [], false, default);
        var outer = new XmlDatatypeDefinition(new("urn:constraint", "outer"), XmlDatatypeVariety.Union, DatatypeTypes.Name("anySimpleType"), null, [list.Name, DatatypeTypes.Name("string")], [], [], false, default);
        var r = new XmlDatatypeRegistry([u, list, outer], Op()); var e = new XmlFacetEvaluator();
        Assert.True(DatatypeConstraintRepresentation.TryCreate(outer, " 1\t02  0 ", Context, r, e, Op(), out var rep, out _));
        Assert.Equal("true 2.0 false", rep!.Lexical); var selected = rep.DeclaredValue.SelectedMember!; Assert.Equal(list.Name, selected.TypeName);
        Assert.Equal(DatatypeTypes.Name("boolean"), selected.Items![0].SelectedMember!.TypeName);
        Assert.Equal(DatatypeTypes.Name("decimal"), selected.Items[1].SelectedMember!.TypeName);
        Assert.True(e.TryValidate(outer, rep.Lexical, rep.Context, r, Op(), out var parsed, out _));
        Assert.True(r.Find(DatatypeTypes.XsdNamespace)!.ValueEquals(rep.DeclaredValue, parsed!, Op()));
        var nested = u with { Name = new("urn:constraint", "nested"), MemberTypes = [u.Name, DatatypeTypes.Name("string")] };
        var rr = new XmlDatatypeRegistry([u, nested], Op());
        Assert.True(DatatypeConstraintRepresentation.TryCreate(nested, "1", Context, rr, e, Op(), out var nr, out _));
        Assert.Equal("true", nr!.Lexical); Assert.Equal(u.Name, nr.DeclaredValue.SelectedMember!.TypeName);
        Assert.Equal(DatatypeTypes.Name("boolean"), nr.DeclaredValue.SelectedMember.SelectedMember!.TypeName);
    }
    [Fact]
    public void LargeYearsFractionsAndDecimalDigitsAreNotNarrowed()
    {
        string year = new string('9', 256), fraction = new string('1', 4096);
        ExactPrescribedAndFallbackFormsRoundTrip("dateTime", year + "-12-31T24:00:00.000Z", "1" + new string('0', 256) + "-01-01T00:00:00Z");
        ExactPrescribedAndFallbackFormsRoundTrip("dateTime", "2000-01-01T01:00:00." + fraction + "00+01:00", "2000-01-01T00:00:00." + fraction + "Z");
        ExactPrescribedAndFallbackFormsRoundTrip("decimal", new string('9', 4096) + ".01000", new string('9', 4096) + ".01");
    }
    [Theory]
    [InlineData("fr-FR")][InlineData("ar-SA")][InlineData("tr-TR")]
    public void CultureDoesNotChangeRepresentatives(string culture)
    {
        var old = CultureInfo.CurrentCulture; var ui = CultureInfo.CurrentUICulture;
        try { CultureInfo.CurrentCulture = new(culture); CultureInfo.CurrentUICulture = new(culture); ExactPrescribedAndFallbackFormsRoundTrip("double", "123.5", "1.235E2"); ExactPrescribedAndFallbackFormsRoundTrip("date", "2000-01-01+13:00", "1999-12-31-11:00"); }
        finally { CultureInfo.CurrentCulture = old; CultureInfo.CurrentUICulture = ui; }
    }
}

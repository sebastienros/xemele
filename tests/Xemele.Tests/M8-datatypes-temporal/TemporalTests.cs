using System.Globalization;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8DatatypesTemporal;

public sealed class TemporalTests
{
    private static readonly TemporalDatatypeProvider Provider = new();
    private static readonly SchemaNamespaceContext Context = new(new Dictionary<string, string>(), "", null);
    private static SchemaCompileOperation Op(long? work = null, int? components = null) =>
        new(new() { MaxOperations = work, MaxComponents = components, CancellationToken = TestContext.Current.CancellationToken, Limits = new() { EvaluationWork = null } });
    private static SchemaCompileOperation TokenOp(CancellationToken token) =>
        new(new() { MaxOperations = null, MaxComponents = null, CancellationToken = token, Limits = new() { EvaluationWork = null } });
    private static DatatypeAtomicValue Parse(string type, ReadOnlySpan<char> input, ISchemaOperation? op = null, TemporalDatatypeProvider? provider = null)
    {
        Assert.True((provider ?? Provider).TryParse(DatatypeTypes.All[type], input, Context, op ?? Op(), out var value, out var error));
        Assert.Null(error); Assert.NotNull(value); return value;
    }
    private static void Invalid(string type, params string[] inputs)
    {
        foreach (var input in inputs)
        {
            var op = Op(); Assert.False(Provider.TryParse(DatatypeTypes.All[type], input, Context, op, out var value, out var error));
            Assert.Null(value); Assert.Same(DatatypeErrors.InvalidLexical, error); Assert.True(op.IsComplete);
        }
    }
    private static void Order(string type, string a, string b, XmlValueOrder expected)
    {
        var x = Parse(type, a); var y = Parse(type, b);
        Assert.Equal(expected, DatatypeValues.Compare(x, y, Op()));
        Assert.Equal(expected switch { XmlValueOrder.Less => XmlValueOrder.Greater, XmlValueOrder.Greater => XmlValueOrder.Less, _ => expected }, DatatypeValues.Compare(y, x, Op()));
        Assert.Equal(expected == XmlValueOrder.Equal, DatatypeValues.ValueEquals(x, y, Op()));
        if (expected == XmlValueOrder.Equal) Assert.Equal(DatatypeValues.ValueHashCode(x, Op()), DatatypeValues.ValueHashCode(y, Op()));
    }

    [Fact] public void AllNinePrimitivesUseVerifiedCoreMetadataAndOwnDeclaredTypes()
    {
        (string Type, string Lexical)[] cases = [("dateTime", "2000-02-29T23:59:59.123Z"), ("time", "12:34:56"), ("date", "2004-02-29"),
            ("gYearMonth", "-0004-02"), ("gYear", "12345678901234567890"), ("gMonthDay", "--02-29"), ("gDay", "---31"), ("gMonth", "--12"), ("duration", "-P1Y2M3DT4H5M6.7S")];
        foreach (var c in cases)
        {
            var value = Parse(c.Type, c.Lexical); Assert.Equal(DatatypeTypes.Name(c.Type), value.TypeName);
            Assert.Equal(DatatypeTypes.All[c.Type].Primitive, value.Primitive); Assert.Same(Provider, value.Provider);
            Assert.Null(DatatypeValues.ValueLength(value, Op())); Assert.Null(DatatypeValues.TotalDigits(value, Op())); Assert.Null(DatatypeValues.FractionDigits(value, Op()));
        }
    }
    [Fact] public void GregorianLeapAndCalendarBoundsIncludeBce()
    {
        foreach (string lexical in new[] { "2000-02-29", "2004-02-29", "-0004-02-29", "-0400-02-29", "0001-01-01", "9999-12-31" }) Parse("date", lexical);
        Invalid("date", "1900-02-29", "2100-02-29", "2001-02-29", "-0001-02-29", "-0100-02-29", "2000-04-31", "2000-00-01", "2000-13-01", "2000-01-00", "2000-01-32");
        Invalid("gMonthDay", "--02-30", "--04-31"); Invalid("gDay", "---00", "---32"); Invalid("gMonth", "--00", "--13");
    }
    [Fact] public void YearSyntaxDoesNotAcceptXsd11OrReducedWidths()
    {
        foreach (string type in new[] { "gYear", "date", "gYearMonth", "dateTime" })
        {
            string tail = type switch { "date" => "-01-01", "gYearMonth" => "-01", "dateTime" => "-01-01T00:00:00", _ => "" };
            Invalid(type, "0000" + tail, "-0000" + tail, "123" + tail, "01234" + tail, "-01234" + tail, "+2000" + tail);
            Parse(type, "10000" + tail); Parse(type, "-10000" + tail);
        }
    }
    [Fact] public void BceTransitionSkipsZeroAndPreservesNegativeYearLeapRule()
    {
        Order("dateTime", "-0001-12-31T24:00:00Z", "0001-01-01T00:00:00Z", XmlValueOrder.Equal);
        Order("dateTime", "-0004-02-28T24:00:00", "-0004-02-29T00:00:00", XmlValueOrder.Equal);
        Order("dateTime", "-0001-12-31T23:30:00-01:00", "0001-01-01T00:30:00Z", XmlValueOrder.Equal);
    }
    [Fact] public void MidnightTwentyFourAndFractionsAreExact()
    {
        Order("dateTime", "1999-12-31T24:00:00.000Z", "2000-01-01T00:00:00Z", XmlValueOrder.Equal);
        Order("time", "24:00:00.000", "00:00:00", XmlValueOrder.Equal);
        Invalid("time", "24:00:00.0001", "24:01:00", "24:00:01", "25:00:00", "00:60:00", "00:00:60", "00:00:00.");
        Order("time", "12:00:00.123000", "12:00:00.123", XmlValueOrder.Equal);
        Order("time", "12:00:00.123000000000000000000000001", "12:00:00.123000000000000000000000002", XmlValueOrder.Less);
    }
    [Fact] public void TimezoneExtremaAndZeroAliasesHaveConsistentIdentity()
    {
        Order("dateTime", "2000-01-01T14:00:00+14:00", "2000-01-01T00:00:00Z", XmlValueOrder.Equal);
        Order("dateTime", "2000-01-01T10:00:00-14:00", "2000-01-02T00:00:00Z", XmlValueOrder.Equal);
        foreach (string zone in new[] { "Z", "+00:00", "-00:00" }) Order("dateTime", "2000-01-01T00:00:00" + zone, "2000-01-01T00:00:00Z", XmlValueOrder.Equal);
        Invalid("time", "00:00:00+14:01", "00:00:00-14:01", "00:00:00+15:00", "00:00:00+01:60", "00:00:00+1:00", "00:00:00z");
    }
    [Fact] public void AbsentZonesUseStrictFourteenHourUncertainty()
    {
        Order("dateTime", "2000-01-01T00:00:00", "2000-01-01T00:00:00Z", XmlValueOrder.Indeterminate);
        Order("dateTime", "2000-01-01T00:00:00", "2000-01-01T14:00:00Z", XmlValueOrder.Indeterminate);
        Order("dateTime", "2000-01-01T00:00:00", "1999-12-31T10:00:00Z", XmlValueOrder.Indeterminate);
        Order("dateTime", "2000-01-01T00:00:00", "2000-01-01T14:00:00.0000000001Z", XmlValueOrder.Less);
        Order("dateTime", "2000-01-01T00:00:00", "1999-12-31T09:59:59.9999999999Z", XmlValueOrder.Greater);
        Order("dateTime", "2000-01-01T00:00:00", "2000-01-01T00:00:01", XmlValueOrder.Less);
    }
    [Fact] public void DateIntervalsTrackTheirStartingInstant()
    {
        Order("date", "2002-10-10+13:00", "2002-10-09-11:00", XmlValueOrder.Equal);
        Order("date", "2002-10-10", "2002-10-10Z", XmlValueOrder.Indeterminate);
        Order("date", "2002-10-10", "2002-10-11Z", XmlValueOrder.Less);
        Order("date", "2002-10-10+00:01", "2002-10-10Z", XmlValueOrder.Less);
    }
    [Fact] public void RecurringTimesWrapUtcMidnightAndKeepZonePresence()
    {
        Order("time", "00:00:00+01:00", "23:00:00Z", XmlValueOrder.Equal);
        Order("time", "23:00:00-02:00", "01:00:00Z", XmlValueOrder.Equal);
        Order("time", "12:00:00", "12:00:00Z", XmlValueOrder.Indeterminate);
        Order("time", "00:00:00", "23:00:00Z", XmlValueOrder.Less);
    }
    [Fact] public void TruncatedCalendarLexicalsArePinnedToSecondEdition()
    {
        Parse("gMonth", "--05Z"); Invalid("gMonth", "--05--", "--05--Z", "-05", "05");
        Invalid("gMonthDay", "--2-29", "--02-9", "2000-02-29"); Invalid("gDay", "--31", "---1");
        Invalid("gYearMonth", "2000-1", "2000-01-01"); Invalid("date", "2000-1-01", "2000-01-1", "20000101");
        Order("gMonthDay", "--02-29", "--03-01", XmlValueOrder.Less);
        Order("gDay", "---30", "---31", XmlValueOrder.Less);
        Order("gMonth", "--11", "--12", XmlValueOrder.Less);
        Order("gYearMonth", "2000-12", "2001-01", XmlValueOrder.Less);
    }
    [Fact] public void ArbitraryYearsAndFractionsHaveNoClrRangeOrFloatLoss()
    {
        string year = "1" + new string('0', 160);
        Parse("date", year + "-02-29");
        Order("gYear", year, "2" + new string('0', 160), XmlValueOrder.Less);
        Order("gYear", "-" + year, "-9999", XmlValueOrder.Less);
        string fraction = new string('0', 300) + "1";
        Order("dateTime", year + "-01-01T00:00:00." + fraction + "000Z", year + "-01-01T00:00:00." + fraction + "Z", XmlValueOrder.Equal);
        Order("time", "00:00:00." + fraction, "00:00:00", XmlValueOrder.Greater);
    }
    [Fact] public void DurationLexicalGrammarAndUnboundedComponents()
    {
        foreach (string input in new[] { "P0Y", "-PT0S", "P1Y2M3DT4H5M6.70S", "P13M", "P999999999999999999999999D", "PT999999999999999999999999.1S" }) Parse("duration", input);
        Invalid("duration", "P", "PT", "P1YT", "P-1Y", "+P1D", "P1M1Y", "P1D1D", "P1.2Y", "PT.5S", "PT1.S", "P1S", "PT1D", "P1DT1HT1S", "P1Djunk");
    }
    [Fact] public void DurationEquivalentFieldsAndSignedZeroHashEqually()
    {
        Order("duration", "P1Y", "P12M", XmlValueOrder.Equal);
        Order("duration", "P1D", "PT24H", XmlValueOrder.Equal);
        Order("duration", "PT1M", "PT60S", XmlValueOrder.Equal);
        Order("duration", "PT0.00100S", "PT0.001S", XmlValueOrder.Equal);
        Order("duration", "-PT0.00100S", "-PT0.001S", XmlValueOrder.Equal);
        Order("duration", "-P0Y0M0DT0H0M0.000S", "PT0S", XmlValueOrder.Equal);
        Order("duration", "-PT0.0001S", "PT0S", XmlValueOrder.Less);
    }
    [Fact] public void DurationNormativeReferenceTableHasPartialOrder()
    {
        foreach (var c in new[] { ("P1Y", "P364D", XmlValueOrder.Greater), ("P1Y", "P365D", XmlValueOrder.Indeterminate),
            ("P1Y", "P366D", XmlValueOrder.Indeterminate), ("P1Y", "P367D", XmlValueOrder.Less),
            ("P1M", "P27D", XmlValueOrder.Greater), ("P1M", "P28D", XmlValueOrder.Indeterminate),
            ("P1M", "P30D", XmlValueOrder.Indeterminate), ("P1M", "P31D", XmlValueOrder.Indeterminate), ("P1M", "P32D", XmlValueOrder.Less),
            ("P5M", "P149D", XmlValueOrder.Greater), ("P5M", "P150D", XmlValueOrder.Indeterminate), ("P5M", "P154D", XmlValueOrder.Less) })
            Order("duration", c.Item1, c.Item2, c.Item3);
    }
    [Fact] public void DurationNegativeAndHugeValuesUseCalendarEndpoints()
    {
        Order("duration", "-P1M", "-P30D", XmlValueOrder.Indeterminate);
        Order("duration", "-P1Y", "-P367D", XmlValueOrder.Greater);
        Order("duration", "P100000000000000000000000000Y", "P1200000000000000000000000000M", XmlValueOrder.Equal);
        Order("duration", "-P100000000000000000000000000Y", "-P1200000000000000000000000000M", XmlValueOrder.Equal);
        Order("duration", "PT999999999999999999999999.0000000001S", "PT999999999999999999999999.0000000002S", XmlValueOrder.Less);
    }
    [Fact] public void CoreWrappersAndProviderIdentityPreserveDisjointPrimitives()
    {
        var v = Parse("dateTime", "2000-01-01T00:00:00Z");
        var derived = DatatypeValues.Retype(new("urn:types", "restricted"), v, Op());
        var union = DatatypeValues.CreateUnion(new("urn:types", "union"), derived, Op());
        Assert.True(DatatypeValues.ValueEquals(v, union, Op())); Assert.Equal(DatatypeValues.ValueHashCode(v, Op()), DatatypeValues.ValueHashCode(union, Op()));
        Assert.False(DatatypeValues.ValueEquals(v, Parse("date", "2000-01-01Z"), Op()));
        Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(v, Parse("time", "00:00:00Z"), Op()));
        var other = Parse("dateTime", "2000-01-01T00:00:00Z", provider: new());
        Assert.Throws<ArgumentException>(() => DatatypeValues.Compare(v, other, Op()));
    }
    [Fact] public void PayloadDetachesMutableLexicalAndContextAndIgnoresCulture()
    {
        char[] buffer = "2000-01-01T00:00:00.123Z".ToCharArray(); var v = Parse("dateTime", buffer); Array.Fill(buffer, 'X');
        Assert.True(DatatypeValues.ValueEquals(v, Parse("dateTime", "2000-01-01T00:00:00.1230Z"), Op()));
        var old = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = new("ar-SA"); Order("duration", "P1Y", "P12M", XmlValueOrder.Equal); Invalid("gYear", "٢٠٠٠"); }
        finally { CultureInfo.CurrentCulture = old; }
    }
    [Fact] public void ParseExactWorkAndGrowthCapsStopBeforePublication()
    {
        foreach (var c in new[] { ("dateTime", "2000-02-29T24:00:00.000"), ("duration", "-P100000000000000000000Y2MT0.123S"), ("gYear", "12345678901234567890") })
        {
            var ledger = new Ledger(); Parse(c.Item1, c.Item2, ledger);
            Assert.True(ledger.Work > 0); Assert.True(ledger.Components > 1);
            Parse(c.Item1, c.Item2, Op(ledger.Work, ledger.Components));
            foreach (var refused in new[] { Op(ledger.Work - 1, ledger.Components), Op(ledger.Work, ledger.Components - 1) })
            {
                Assert.False(Provider.TryParse(DatatypeTypes.All[c.Item1], c.Item2, Context, refused, out var value, out var error));
                Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error); Assert.False(refused.IsComplete);
            }
        }
    }
    [Fact] public void CompareHashExactWorkAndMidstreamCancellationPropagate()
    {
        var a = Parse("duration", "P1M"); var b = Parse("duration", "P30D"); var ledger = new Ledger();
        var expected = DatatypeValues.Compare(a, b, ledger); Assert.Equal(expected, DatatypeValues.Compare(a, b, Op(ledger.Work)));
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.Compare(a, b, Op(ledger.Work - 1)));
        ledger = new(); int hash = DatatypeValues.ValueHashCode(a, ledger); Assert.Equal(hash, DatatypeValues.ValueHashCode(a, Op(ledger.Work)));
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.ValueHashCode(a, Op(ledger.Work - 1)));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => Parse("gYear", "2000", TokenOp(cts.Token)));
        var middle = new Ledger { FailAt = 20 };
        Assert.Throws<OperationCanceledException>(() => Parse("duration", "P100000000000000000000Y", middle));
        Assert.Throws<OperationCanceledException>(() => DatatypeValues.Compare(a, b, new Ledger { FailAt = 4 }));
        Assert.Throws<OperationCanceledException>(() => DatatypeValues.ValueHashCode(a, new Ledger { FailAt = 4 }));
    }
    [Fact] public void StructuredUnknownInvalidStoppedAndCallerFaultAreDistinct()
    {
        Assert.False(Provider.TryParse(DatatypeTypes.All["string"], "x", Context, Op(), out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.UnknownType, error);
        Invalid("dateTime", "", " 2000-01-01T00:00:00", "2000-01-01T00:00:00 ", "2000-01-01t00:00:00", "2000-01-01T00:00:00\ud800");
        var stop = Op(1); stop.Charge(1, default); stop.Charge(1, default);
        Assert.False(Provider.TryParse(DatatypeTypes.All["gYear"], "2000", Context, stop, out value, out error)); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error);
        var fault = new Ledger { CallerFault = true };
        Assert.Throws<InvalidOperationException>(() => Parse("gYear", "2000", fault));
    }

    private sealed class Ledger : ISchemaOperation
    {
        internal long Work; internal int Components; internal long? FailAt; internal bool CallerFault;
        public XmlResourceLimits Limits { get; } = new(); public CancellationToken CancellationToken => default; public bool IsComplete => true; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation location)
        { if (CallerFault) throw new InvalidOperationException("Caller fault"); if (FailAt is long n && Work + work >= n) throw new OperationCanceledException(); Work += work; return true; }
        public bool ChargeComponents(int count, XmlLocation location) { Components += count; return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException();
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException();
    }
}

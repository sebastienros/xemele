using System.Globalization;
using System.Numerics;
using Xunit;
using Xemele.Validation;

namespace Xemele.Tests.M8DatatypesNumeric;

public sealed class NumericTests
{
    private static readonly NumericDatatypeProvider Provider = new();
    private static readonly SchemaNamespaceContext Context = new(new Dictionary<string, string>(), "", null);
    private static SchemaCompileOperation Operation(long? work = null, int? components = null) =>
        new(new() { MaxOperations = work, MaxComponents = components, CancellationToken = TestContext.Current.CancellationToken, Limits = new() { EvaluationWork = null } });
    private static SchemaCompileOperation OperationWithToken(CancellationToken token) => new(new() { MaxOperations = null, MaxComponents = null, CancellationToken = token, Limits = new() { EvaluationWork = null } });
    private static DatatypeAtomicValue Parse(string lexical, string type = "decimal", ISchemaOperation? operation = null)
    {
        Assert.True(Provider.TryParse(DatatypeTypes.All[type], lexical, Context, operation ?? Operation(), out var value, out var error));
        Assert.Null(error); return Assert.IsAssignableFrom<DatatypeAtomicValue>(value);
    }
    [Theory]
    [InlineData("+00012.34000", 4, 2)]
    [InlineData(".00120", 4, 4)]
    [InlineData("-0.0000", 1, 0)]
    [InlineData("12000.0000", 5, 0)]
    [InlineData("1.", 1, 0)]
    [InlineData("+.1", 1, 1)]
    [InlineData("0.001", 3, 3)][InlineData("0.00100", 3, 3)]
    [InlineData("0001.000", 1, 0)][InlineData("0", 1, 0)]
    [InlineData("1000", 4, 0)][InlineData("1000.00", 4, 0)]
    public void DecimalGrammarAndValueSpaceDigits(string input, int total, int fraction)
    {
        var value = Parse(input); var op = Operation();
        Assert.Equal(total, DatatypeValues.TotalDigits(value, op));
        Assert.Equal(fraction, DatatypeValues.FractionDigits(value, op));
        Assert.Null(DatatypeValues.ValueLength(value, op)); Assert.Equal(DatatypeTypes.Name("decimal"), value.TypeName);
    }
    [Theory]
    [InlineData("", "decimal")][InlineData(".", "decimal")][InlineData("+", "decimal")]
    [InlineData("1e2", "decimal")][InlineData("1,2", "decimal")][InlineData(" 1", "decimal")]
    [InlineData("1 ", "decimal")][InlineData("1.2.3", "decimal")][InlineData("١", "decimal")]
    [InlineData("1.0", "integer")][InlineData("--1", "integer")][InlineData("INF", "decimal")]
    [InlineData("nan", "float")][InlineData("+INF", "float")][InlineData("Infinity", "double")]
    [InlineData("-NaN", "float")][InlineData("1e", "double")][InlineData("1e+-1", "double")]
    [InlineData(".e1", "float")][InlineData("1e1.0", "float")][InlineData("1e2e3", "float")]
    [InlineData("1_000", "double")][InlineData("0x10", "float")][InlineData("\u00a01", "decimal")]
    public void InvalidLexicalIsStructuredAndBounded(string input, string type)
    {
        var op = Operation(); Assert.False(Provider.TryParse(DatatypeTypes.All[type], input, Context, op, out var value, out var error));
        Assert.Null(value); Assert.Same(DatatypeErrors.InvalidLexical, error); Assert.True(op.IsComplete);
    }
    [Theory]
    [InlineData("byte", "-128", "127", "-129", "128")]
    [InlineData("short", "-32768", "32767", "-32769", "32768")]
    [InlineData("int", "-2147483648", "2147483647", "-2147483649", "2147483648")]
    [InlineData("long", "-9223372036854775808", "9223372036854775807", "-9223372036854775809", "9223372036854775808")]
    [InlineData("unsignedByte", "0", "255", "-1", "256")]
    [InlineData("unsignedShort", "0", "65535", "-1", "65536")]
    [InlineData("unsignedInt", "0", "4294967295", "-1", "4294967296")]
    [InlineData("unsignedLong", "0", "18446744073709551615", "-1", "18446744073709551616")]
    public void BuiltinRangeEdgesAreExact(string type, string min, string max, string below, string above)
    {
        Parse(min, type); Parse(max, type); Parse("+000" + max, type);
        foreach (string input in new[] { below, above })
        { Assert.False(Provider.TryParse(DatatypeTypes.All[type], input, Context, Operation(), out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.InvalidLexical, error); }
    }
    [Theory]
    [InlineData("nonPositiveInteger", "0", "-999", "1")]
    [InlineData("negativeInteger", "-1", "-999", "-0")]
    [InlineData("nonNegativeInteger", "-0", "999", "-1")]
    [InlineData("positiveInteger", "+1", "999", "0")]
    public void UnboundedIntegerDerivationsRespectSign(string type, string a, string b, string invalid)
    {
        Parse(a, type); Parse(b, type);
        Assert.False(Provider.TryParse(DatatypeTypes.All[type], invalid, Context, Operation(), out _, out var error)); Assert.Same(DatatypeErrors.InvalidLexical, error);
    }
    [Fact]
    public void ArbitraryMagnitudePrecisionComparisonAndLifetime()
    {
        string large = new('9', 8192); var integer = Parse(large, "integer"); var decimalValue = Parse(large + ".000");
        Assert.True(DatatypeValues.ValueEquals(integer, decimalValue, Operation()));
        Assert.Equal(DatatypeValues.ValueHashCode(integer, Operation()), DatatypeValues.ValueHashCode(decimalValue, Operation()));
        Assert.Equal(8192, DatatypeValues.TotalDigits(integer, Operation()));
        var tiny = Parse("0." + new string('0', 8192) + "12340000");
        Assert.Equal(8196, DatatypeValues.FractionDigits(tiny, Operation()));
        Assert.Equal(XmlValueOrder.Less, DatatypeValues.Compare(tiny, Parse("0.1"), Operation()));
        char[] caller = "0012.3400".ToCharArray(); Assert.True(Provider.TryParse(DatatypeTypes.All["decimal"], caller, Context, Operation(), out var snapshot, out _));
        Array.Fill(caller, '9'); Assert.True(DatatypeValues.ValueEquals(snapshot!, Parse("12.34"), Operation()));
    }
    [Theory]
    [InlineData("12.00", "+00012", "integer")][InlineData("-0.000", "+000", "unsignedLong")]
    [InlineData("127.000", "127", "byte")][InlineData("-.001200", "-0.0012", "decimal")]
    public void CompatibleDerivedValuesShareEqualityOrderHash(string a, string b, string type)
    {
        var x = Parse(a); var y = Parse(b, type); var op = Operation();
        Assert.True(DatatypeValues.ValueEquals(x, y, op)); Assert.Equal(XmlValueOrder.Equal, DatatypeValues.Compare(x, y, op));
        Assert.Equal(DatatypeValues.ValueHashCode(x, op), DatatypeValues.ValueHashCode(y, op));
        var wrapped = DatatypeValues.Retype(new("urn:test", "restriction"), y, op);
        var selected = DatatypeValues.CreateUnion(new("urn:test", "union"), wrapped, op);
        Assert.True(DatatypeValues.ValueEquals(x, selected, op)); Assert.Equal(DatatypeValues.ValueHashCode(x, op), DatatypeValues.ValueHashCode(selected, op));
    }
    [Fact]
    public void DecimalOrderMatchesIndependentBigIntegerRationalOracle()
    {
        var random = new Random(42);
        for (int i = 0; i < 200; i++)
        {
            long a = random.NextInt64(-1000000000000, 1000000000000), b = random.NextInt64(-1000000000000, 1000000000000);
            int aScale = random.Next(0, 18), bScale = random.Next(0, 18);
            string Lex(long n, int scale) { string digits = Math.Abs(n).ToString(CultureInfo.InvariantCulture).PadLeft(scale + 1, '0'); return (n < 0 ? "-" : "") + (scale == 0 ? digits : digits.Insert(digits.Length - scale, ".")); }
            var x = Parse(Lex(a, aScale)); var y = Parse(Lex(b, bScale));
            int oracle = Math.Sign(((BigInteger)a * BigInteger.Pow(10, bScale)).CompareTo((BigInteger)b * BigInteger.Pow(10, aScale)));
            Assert.Equal((XmlValueOrder)oracle, DatatypeValues.Compare(x, y, Operation()));
        }
    }
    [Theory]
    [InlineData("float")][InlineData("double")]
    public void FloatingSpecialsRoundingZerosAndPrimitiveDomains(string type)
    {
        var op = Operation(); var zero = Parse("0", type); var minusZero = Parse("-0e10", type);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(((NumericDatatypeProvider.FloatingValue)minusZero).Number));
        Assert.True(DatatypeValues.ValueEquals(zero, minusZero, op)); Assert.Equal(DatatypeValues.ValueHashCode(zero, op), DatatypeValues.ValueHashCode(minusZero, op));
        var nan = Parse("NaN", type); Assert.True(DatatypeValues.ValueEquals(nan, Parse("NaN", type), op));
        Assert.Equal(XmlValueOrder.Equal, DatatypeValues.Compare(nan, nan, op)); Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(nan, zero, op));
        Assert.Equal(XmlValueOrder.Less, DatatypeValues.Compare(Parse("-INF", type), zero, op));
        Assert.Equal(XmlValueOrder.Greater, DatatypeValues.Compare(Parse("INF", type), zero, op));
        Assert.True(DatatypeValues.ValueEquals(Parse("1e999999", type), Parse("INF", type), op));
        Assert.True(DatatypeValues.ValueEquals(Parse("-1e-999999", type), minusZero, op));
        Assert.True(DatatypeValues.ValueEquals(Parse("+.5e+1", type), Parse("5", type), op));
        Assert.Null(DatatypeValues.TotalDigits(zero, op)); Assert.Null(DatatypeValues.FractionDigits(zero, op));
        var decimalZero = Parse("0"); Assert.False(DatatypeValues.ValueEquals(zero, decimalZero, op)); Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(zero, decimalZero, op));
        Assert.False(DatatypeValues.ValueEquals(Parse("1", "float"), Parse("1", "double"), op));
        string midpoint = type == "float" ? "16777217" : "9007199254740993";
        string rounded = type == "float" ? "16777216" : "9007199254740992";
        Assert.True(DatatypeValues.ValueEquals(Parse(midpoint, type), Parse(rounded, type), op));
        string minSubnormal = type == "float" ? "1.401298464324817070923729583289916131280e-45" : "4.940656458412465441765687928682213723651e-324";
        Assert.Equal(XmlValueOrder.Greater, DatatypeValues.Compare(Parse(minSubnormal, type), zero, op));
    }
    [Theory]
    [InlineData("float", 150)][InlineData("double", 1075)]
    public void LongMantissasExponentsAndSubnormalMidpoints(string type, int binaryScale)
    {
        var op = Operation();
        Assert.True(DatatypeValues.ValueEquals(Parse("1." + new string('0', 4096) + "1", type), Parse("1", type), op));
        Assert.True(DatatypeValues.ValueEquals(Parse("1e" + new string('9', 4096), type), Parse("INF", type), op));
        Assert.True(DatatypeValues.ValueEquals(Parse("1e-" + new string('9', 4096), type), Parse("0", type), op));
        string coefficient = BigInteger.Pow(5, binaryScale).ToString(CultureInfo.InvariantCulture);
        string half = "0." + coefficient.PadLeft(binaryScale, '0');
        Assert.True(DatatypeValues.ValueEquals(Parse(half, type), Parse("0", type), op));
        string aboveCoefficient = (BigInteger.Parse(coefficient, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        string above = "0." + aboveCoefficient.PadLeft(binaryScale, '0');
        Assert.Equal(XmlValueOrder.Greater, DatatypeValues.Compare(Parse(above, type), Parse("0", type), op));
    }
    [Theory]
    [InlineData("float", 24, 150, 103, 25)][InlineData("double", 53, 1075, 970, 54)]
    public void BoundedConversionPreservesCarryOverflowAndDistantStickyTails(string type, int precision, int subnormalScale, int overflowScale, int overflowBits)
    {
        static string DecimalRational(BigInteger coefficient, int scale)
        {
            string digits = coefficient.ToString(CultureInfo.InvariantCulture).PadLeft(scale + 1, '0');
            return digits.Insert(digits.Length - scale, ".");
        }
        double Number(string text) => ((NumericDatatypeProvider.FloatingValue)Parse(text, type)).Number;
        double Oracle(string text) => type == "float" ? float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        string carry = DecimalRational(((BigInteger.One << (precision + 1)) - 1) * BigInteger.Pow(5, precision), precision);
        Assert.Equal(2, Number(carry));
        string halfSubnormal = DecimalRational(BigInteger.Pow(5, subnormalScale), subnormalScale);
        foreach (string midpoint in new[] { carry, halfSubnormal })
        {
            string above = midpoint + new string('0', 4096) + "1";
            string exactWithZeros = midpoint + new string('0', 4096);
            BigInteger coefficient = BigInteger.Parse(midpoint.Replace(".", ""), CultureInfo.InvariantCulture);
            int scale = midpoint.Length - midpoint.IndexOf('.') - 1;
            string below = DecimalRational(coefficient * BigInteger.Pow(10, 4097) - 1, scale + 4097);
            foreach (string lexical in new[] { midpoint, above, exactWithZeros, below, "-" + above, "-" + below })
                Assert.Equal(BitConverter.DoubleToInt64Bits(Oracle(lexical)), BitConverter.DoubleToInt64Bits(Number(lexical)));
        }
        BigInteger overflow = ((BigInteger.One << overflowBits) - 1) << overflowScale;
        Assert.True(double.IsPositiveInfinity(Number(overflow.ToString(CultureInfo.InvariantCulture))));
        Assert.False(double.IsInfinity(Number((overflow - 1).ToString(CultureInfo.InvariantCulture))));
        var random = new Random(91);
        for (int i = 0; i < 80; i++)
        {
            char[] digits = new char[1800]; for (int j = 0; j < digits.Length; j++) digits[j] = (char)('0' + random.Next(10));
            string lexical = (i % 2 == 0 ? "-" : "+") + new string(digits).Insert(random.Next(0, digits.Length), ".") + "e" + random.Next(-2000, 500).ToString(CultureInfo.InvariantCulture);
            Assert.Equal(BitConverter.DoubleToInt64Bits(Oracle(lexical)), BitConverter.DoubleToInt64Bits(Number(lexical)));
        }
    }
    [Fact]
    public void CultureMetadataProviderAndArgumentBoundaries()
    {
        var original = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = new("fr-FR"); Parse("123.5e-2", "double"); Parse("123.5"); }
        finally { CultureInfo.CurrentCulture = original; }
        Assert.False(Provider.TryParse(DatatypeTypes.All["boolean"], "1", Context, Operation(), out _, out var error)); Assert.Same(DatatypeErrors.UnknownType, error);
        Assert.False(Provider.TryParse(DatatypeTypes.All["integer"] with { LocalName = "unknown" }, "1", Context, Operation(), out _, out error)); Assert.Same(DatatypeErrors.UnknownType, error);
        var other = new NumericDatatypeProvider(); Assert.True(other.TryParse(DatatypeTypes.All["decimal"], "1", Context, Operation(), out var otherValue, out _));
        Assert.Throws<ArgumentException>(() => DatatypeValues.ValueEquals(Parse("1"), otherValue!, Operation()));
        Assert.Throws<ArgumentNullException>(() => Provider.TryParse(DatatypeTypes.All["decimal"], "1", null!, Operation(), out _, out _));
    }
    [Theory]
    [InlineData("123.40", "decimal")][InlineData("123", "integer")][InlineData("255", "unsignedByte")]
    [InlineData("0.000", "decimal")][InlineData("1.2345e20", "double")][InlineData("NaN", "float")]
    public void ExactWorkComponentsAndOneOverRefuseBeforeSuccess(string lexical, string type)
    {
        var counter = new CountingOperation(); Parse(lexical, type, counter);
        var exact = Operation(counter.Work, counter.Components); Parse(lexical, type, exact); Assert.Equal(0, exact.RemainingWork);
        Assert.False(Provider.TryParse(DatatypeTypes.All[type], lexical, Context, Operation(counter.Work - 1), out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error);
        var shortComponents = Operation(components: counter.Components); shortComponents.ChargeComponents(1, default);
        Assert.False(Provider.TryParse(DatatypeTypes.All[type], lexical, Context, shortComponents, out value, out error)); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error);
    }
    [Fact]
    public void ComparisonHashBudgetCancellationAndCallerExceptions()
    {
        var a = Parse(new string('1', 500)); var b = Parse(new string('1', 499) + "2");
        var counter = new CountingOperation(); Assert.Equal(XmlValueOrder.Less, DatatypeValues.Compare(a, b, counter));
        Assert.Equal(XmlValueOrder.Less, DatatypeValues.Compare(a, b, Operation(counter.Work)));
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.Compare(a, b, Operation(counter.Work - 1)));
        counter = new(); DatatypeValues.ValueHashCode(a, counter);
        DatatypeValues.ValueHashCode(a, Operation(counter.Work)); Assert.Throws<SchemaCompileException>(() => DatatypeValues.ValueHashCode(a, Operation(counter.Work - 1)));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => Parse("", operation: OperationWithToken(canceled.Token)));
        Assert.Throws<OperationCanceledException>(() => DatatypeValues.ValueHashCode(a, OperationWithToken(canceled.Token)));
        using var middle = new CancellationTokenSource();
        var midway = new CountingOperation { Callback = n => { if (n == 64) middle.Cancel(); }, Token = middle.Token };
        Assert.Throws<OperationCanceledException>(() => Parse(new string('1', 1000), operation: midway));
        var sentinel = new InvalidOperationException("caller"); var throwing = new CountingOperation { Callback = _ => throw sentinel };
        Assert.Same(sentinel, Assert.Throws<InvalidOperationException>(() => Parse("1", operation: throwing)));
    }
    private sealed class CountingOperation : ISchemaOperation
    {
        public long Work; public int Components; public Action<long>? Callback; public CancellationToken Token;
        public XmlResourceLimits Limits { get; } = new(); public CancellationToken CancellationToken => Token;
        public bool IsComplete => true; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation location) { Token.ThrowIfCancellationRequested(); Work += work; Callback?.Invoke(Work); Token.ThrowIfCancellationRequested(); return true; }
        public bool ChargeComponents(int count, XmlLocation location) { Components += count; return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException();
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException();
    }
}

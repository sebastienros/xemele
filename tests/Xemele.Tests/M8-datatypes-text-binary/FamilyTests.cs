using System.Text.Json;
using Xunit;
using Xemele.Validation;
namespace Xemele.Tests.M8DatatypesTextBinary;

public sealed class FamilyTests
{
    private static readonly TextBinaryDatatypeProvider Provider = new();
    private static readonly SchemaNamespaceContext Empty = new(new Dictionary<string, string>(), "", null);
    private static SchemaCompileOperation Op(long? work = null, int? components = null) => new(new()
    { MaxOperations = work, MaxComponents = components, CancellationToken = TestContext.Current.CancellationToken, Limits = new() { EvaluationWork = null } });
    private static DatatypeAtomicValue Parse(string type, string text, SchemaNamespaceContext? context = null)
    {
        var op = Op(); var metadata = DatatypeTypes.All[type];
        string normalized = DatatypeNormalization.Normalize(text, metadata.Whitespace, op);
        Assert.True(Provider.TryParse(metadata, normalized, context ?? Empty, op, out var value, out var error), error?.Code);
        Assert.Null(error); Assert.NotNull(value); Assert.True(op.IsComplete); return value;
    }
    [Theory]
    [InlineData("string", "", 0)]
    [InlineData("string", "\t根😀\r\n", 5)]
    [InlineData("string", "\u007f\u0085\u00a0\U0010ffff", 4)]
    [InlineData("normalizedString", "a\tb\r\nc", 6)]
    [InlineData("token", "\t 根😀  value\r", 8)]
    [InlineData("language", "en-US", 5)]
    [InlineData("language", "abcdefgh-01234567-x", 19)]
    [InlineData("Name", ":a:b", 4)]
    [InlineData("Name", "根_é", 3)]
    [InlineData("NCName", "_a-1.é", 6)]
    [InlineData("NMTOKEN", "9:a-\u0300", 5)]
    [InlineData("ID", "根", 1)]
    [InlineData("IDREF", "a", 1)]
    [InlineData("ENTITY", "unparsed", 8)]
    [InlineData("anySimpleType", "\t😀  根\r", 6)]
    public void TextScalarsAndCoreWhitespace(string type, string input, long length)
    { var value = Parse(type, input); Assert.Equal(length, DatatypeValues.ValueLength(value, Op())); Assert.Null(DatatypeValues.TotalDigits(value, Op())); Assert.Null(DatatypeValues.FractionDigits(value, Op())); }

    [Theory]
    [InlineData("language", "")]
    [InlineData("language", "a-")]
    [InlineData("language", "-en")]
    [InlineData("language", "en--US")]
    [InlineData("language", "1en")]
    [InlineData("language", "abcdefghi")]
    [InlineData("language", "en-123456789")]
    [InlineData("language", "é")]
    [InlineData("Name", "")]
    [InlineData("Name", "1abc")]
    [InlineData("Name", "\u0300a")]
    [InlineData("Name", "\u037fa")]
    [InlineData("Name", "\U00010000")]
    [InlineData("NCName", ":a")]
    [InlineData("ID", "a:b")]
    [InlineData("IDREF", "a b")]
    [InlineData("ENTITY", "a:b")]
    [InlineData("NMTOKEN", "")]
    [InlineData("NMTOKEN", "a b")]
    [InlineData("string", "\0")]
    [InlineData("string", "\u000b")]
    [InlineData("string", "\ufffe")]
    [InlineData("string", "\uffff")]
    public void InvalidTextLexicals(string type, string input)
    {
        var metadata = DatatypeTypes.All[type]; var op = Op();
        var normalized = DatatypeNormalization.Normalize(input, metadata.Whitespace, op);
        Assert.False(Provider.TryParse(metadata, normalized, Empty, op, out var value, out var error));
        Assert.Null(value); Assert.Same(DatatypeErrors.InvalidLexical, error); Assert.True(op.IsComplete);
    }
    [Fact]
    public void UnpairedSurrogatesRejected()
    {
        foreach (string input in new[] { new string((char)0xd800, 1), new string(['x', (char)0xdc00]), new string([(char)0xd800, 'x']) })
        {
            Assert.False(DatatypeNormalization.TryMeasure(input, XmlWhitespaceRule.Preserve, Op(), out _, out _, out var error)); Assert.Same(DatatypeErrors.InvalidUnicode, error);
            Assert.False(Provider.TryParse(DatatypeTypes.All["string"], input, Empty, Op(), out var value, out error)); Assert.Null(value); Assert.Same(DatatypeErrors.InvalidUnicode, error);
        }
    }
    [Fact]
    public void NamesExactlyMatchPinnedSecondEditionForEveryBmpScalar()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        Assert.NotNull(dir);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "tests/fixtures/M8-datatypes-text-binary/xml2-name-ranges.json")));
        var productions = document.RootElement.GetProperty("productions");
        bool[] start = new bool[65536], rest = new bool[65536];
        foreach (string n in new[] { "BaseChar", "Ideographic", "CombiningChar", "Digit", "Extender" })
        foreach (var range in productions.GetProperty(n).EnumerateArray())
        for (int c = range[0].GetInt32(); c <= range[1].GetInt32(); c++)
        { rest[c] = true; if (n is "BaseChar" or "Ideographic") start[c] = true; }
        start[':'] = start['_'] = rest[':'] = rest['_'] = rest['-'] = rest['.'] = true;
        var operation = Op();
        for (int c = 0; c < 65536; c++)
        { Assert.Equal(start[c], TextBinaryNames.Contains(c, false, operation)); Assert.Equal(rest[c], TextBinaryNames.Contains(c, true, operation)); }
        foreach (int c in new[] { 0x10000, 0x10400, 0x1f600, 0x10ffff })
        { Assert.False(TextBinaryNames.Contains(c, false, operation)); Assert.False(TextBinaryNames.Contains(c, true, operation)); }
    }

    [Theory]
    [InlineData("true", "1")]
    [InlineData("false", "0")]
    [InlineData(" true\t", "1")]
    public void BooleanCanonicalIdentity(string a, string b)
    {
        var x = Parse("boolean", a); var y = Parse("boolean", b);
        Assert.True(DatatypeValues.ValueEquals(x, y, Op())); Assert.Equal(DatatypeValues.ValueHashCode(x, Op()), DatatypeValues.ValueHashCode(y, Op())); Assert.Null(DatatypeValues.ValueLength(x, Op()));
        Assert.Equal(XmlValueOrder.Equal, DatatypeValues.Compare(x, y, Op()));
        Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(Parse("boolean", "true"), Parse("boolean", "false"), Op()));
    }
    [Theory]
    [InlineData("")][InlineData("TRUE")][InlineData("True")][InlineData("+1")][InlineData("2")][InlineData("01")][InlineData("true x")][InlineData("\u00a0true")]
    public void InvalidBoolean(string input) => Invalid("boolean", input);

    [Theory]
    [InlineData("hexBinary", "", "", 0)]
    [InlineData("hexBinary", "00aAfF", "00AAff", 3)]
    [InlineData("base64Binary", "", "", 0)]
    [InlineData("base64Binary", "Zg==", " Z g = = \t", 1)]
    [InlineData("base64Binary", "Zm8=", "Z m\n8 =", 2)]
    [InlineData("base64Binary", "Zm9v", "Z m 9 v", 3)]
    [InlineData("base64Binary", "AAH+/w==", "A A H + / w = =", 4)]
    public void ExactBinaryIdentity(string type, string a, string b, long length)
    {
        var x = Parse(type, a); var y = Parse(type, b);
        Assert.True(DatatypeValues.ValueEquals(x, y, Op())); Assert.Equal(length, DatatypeValues.ValueLength(x, Op()));
        Assert.Equal(DatatypeValues.ValueHashCode(x, Op()), DatatypeValues.ValueHashCode(y, Op()));
        int expected = 17;
        foreach (byte octet in type == "hexBinary" ? Convert.FromHexString(a) : Convert.FromBase64String(a)) expected = unchecked(expected * 31 + octet);
        Assert.Equal(unchecked(((int)x.Primitive + 1) * 397 ^ expected), DatatypeValues.ValueHashCode(x, Op()));
    }
    [Theory]
    [InlineData("hexBinary", "0")][InlineData("hexBinary", "0g")][InlineData("hexBinary", "00 01")][InlineData("hexBinary", "０0")]
    [InlineData("base64Binary", "A")][InlineData("base64Binary", "AA")][InlineData("base64Binary", "AAA")][InlineData("base64Binary", "AAAAA")]
    [InlineData("base64Binary", "A===")][InlineData("base64Binary", "=AAA")][InlineData("base64Binary", "AA=A")][InlineData("base64Binary", "AA==AAAA")]
    [InlineData("base64Binary", "AB==")][InlineData("base64Binary", "AAB=")][InlineData("base64Binary", "Zg=")][InlineData("base64Binary", "Zg===")]
    [InlineData("base64Binary", "Zg==!")][InlineData("base64Binary", "Zg\u00a0==")][InlineData("base64Binary", "____")][InlineData("base64Binary", "----")]
    public void InvalidBinary(string type, string input) => Invalid(type, input);
    [Fact]
    public void EveryBase64UnusedBitCombinationIsChecked()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        for (int digit = 0; digit < 64; digit++)
        {
            foreach (int padding in new[] { 1, 2 })
            {
                string input = padding == 1 ? "AA" + chars[digit] + "=" : "A" + chars[digit] + "==";
                bool expected = (digit & (padding == 1 ? 3 : 15)) == 0;
                Assert.Equal(expected, Provider.TryParse(DatatypeTypes.All["base64Binary"], input, Empty, Op(), out _, out _));
            }
        }
    }
    [Fact]
    public void AllOctetsRoundTripAndBinaryPrimitivesStayDisjoint()
    {
        byte[] bytes = Enumerable.Range(0, 256).Select(n => (byte)n).ToArray();
        string hex = Convert.ToHexString(bytes), b64 = Convert.ToBase64String(bytes);
        var a = Parse("hexBinary", hex); var b = Parse("base64Binary", b64);
        Assert.Equal(256L, DatatypeValues.ValueLength(a, Op())); Assert.Equal(256L, DatatypeValues.ValueLength(b, Op()));
        Assert.False(DatatypeValues.ValueEquals(a, b, Op())); Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(a, b, Op()));
        for (int n = 0; n < bytes.Length; n++)
        { Assert.True(DatatypeValues.ValueEquals(Parse("hexBinary", hex.Substring(n * 2, 2)), Parse("hexBinary", bytes[n].ToString("x2")), Op())); }
    }

    [Theory]
    [InlineData("")][InlineData("#fragment")][InlineData("?query")][InlineData("../a b/根😀")][InlineData("http://example.org/a%20b?q=x#f")]
    [InlineData("urn:example:test")][InlineData("mailto:a@example.org")][InlineData("http://[::1]:80/path")][InlineData("//user:pass@[::ffff:192.0.2.1]:/p")]
    [InlineData("http://[1:2:3:4:5:6:7:8]")][InlineData("//")][InlineData("a/b:c")][InlineData("./1:a")][InlineData("a?[]#[]")]
    [InlineData("a{}<>|\\^`\"")][InlineData("a%ff")][InlineData("unknown:opaque?[]")][InlineData("//reg_name:arbitrary-port")]
    public void UriGrammarAndLexicalLength(string input)
    {
        var value = Parse("anyURI", input); Assert.Equal((long)input.EnumerateRunes().Count(), DatatypeValues.ValueLength(value, Op()));
        Assert.True(DatatypeValues.ValueEquals(value, Parse("anyURI", input, Empty with { BaseUri = new("http://base.example/") }), Op()));
        Assert.False(DatatypeValues.ValueEquals(value, Parse("string", input), Op()));
    }
    [Theory]
    [InlineData("%")][InlineData("%0")][InlineData("%GG")][InlineData("a#b#c")][InlineData(":x")][InlineData("1:x")][InlineData("ht tp:x")]
    [InlineData("a[b]")][InlineData("http://[::1")][InlineData("http://[1:2:3]")][InlineData("http://[1:2:3:4:5:6:7:8:9]")]
    [InlineData("http://[1::2::3]")][InlineData("http://[::ffff:256.0.0.1]")][InlineData("http://[::ffff:0000.0.0.1]")]
    [InlineData("http://[fe80::1%25zone]")][InlineData("http://[::1]:a")][InlineData("http://x[::1]")][InlineData("http://[::1]suffix")][InlineData("urn:")]
    public void InvalidUri(string input) => Invalid("anyURI", input);
    [Fact]
    public void UriValuesDoNotResolveFoldCaseOrDecodeEscapes()
    {
        foreach (var pair in new[] { ("a b", "a%20b"), ("http://A/", "http://a/"), ("%aa", "%AA"), ("a/../b", "b") })
        { Assert.False(DatatypeValues.ValueEquals(Parse("anyURI", pair.Item1), Parse("anyURI", pair.Item2), Op())); }
    }
    [Fact]
    public void ExpandedQNameIdentityDefaultNamespaceAndImmutableContext()
    {
        var dictionary = new Dictionary<string, string> { ["p"] = "urn:根😀", ["q"] = "urn:根😀", ["other"] = "urn:other" };
        var context = new SchemaNamespaceContext(dictionary, "urn:根😀", null);
        dictionary["p"] = "urn:changed";
        foreach (string type in new[] { "QName", "NOTATION" })
        {
            var a = Parse(type, "p:根", context); var b = Parse(type, "q:根", context); var c = Parse(type, "根", context);
            Assert.True(DatatypeValues.ValueEquals(a, b, Op())); Assert.True(DatatypeValues.ValueEquals(a, c, Op()));
            Assert.Equal(DatatypeValues.ValueHashCode(a, Op()), DatatypeValues.ValueHashCode(c, Op())); Assert.Null(DatatypeValues.ValueLength(a, Op()));
            Assert.False(DatatypeValues.ValueEquals(a, Parse(type, "other:根", context), Op()));
            Assert.False(DatatypeValues.ValueEquals(a, Parse(type, "根"), Op()));
            Assert.True(DatatypeValues.ValueEquals(Parse(type, "xml:lang"), Parse(type, "lang", Empty with { DefaultNamespace = "http://www.w3.org/XML/1998/namespace" }), Op()));
        }
        Assert.False(DatatypeValues.ValueEquals(Parse("QName", "p:根", context), Parse("NOTATION", "p:根", context), Op()));
    }
    [Theory]
    [InlineData("")][InlineData(":a")][InlineData("a:")][InlineData("a:b:c")][InlineData("p:1")][InlineData("p:a")][InlineData("xmlns:a")][InlineData("1:a")][InlineData("😀")]
    public void InvalidQNames(string input) { Invalid("QName", input); Invalid("NOTATION", input); }
    [Fact]
    public void DerivedRetypedAndOwnedInputIdentity()
    {
        var a = Parse("string", "根😀"); var b = Parse("token", "根😀");
        var derived = DatatypeValues.Retype(new("urn:custom", "derived"), b, Op());
        Assert.True(DatatypeValues.ValueEquals(a, derived, Op())); Assert.Equal(DatatypeValues.ValueHashCode(a, Op()), DatatypeValues.ValueHashCode(derived, Op()));
        char[] source = "root😀".ToCharArray();
        Assert.True(Provider.TryParse(DatatypeTypes.All["string"], source, Empty, Op(), out var owned, out _)); Array.Fill(source, 'x');
        Assert.True(DatatypeValues.ValueEquals(owned!, Parse("string", "root😀"), Op()));
        Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(a, Parse("string", "another"), Op()));
    }
    [Fact]
    public void RejectsUnknownFabricatedAndComposerListMetadata()
    {
        foreach (var type in new[] { DatatypeTypes.All["decimal"], DatatypeTypes.All["NMTOKENS"], DatatypeTypes.All["IDREFS"], DatatypeTypes.All["ENTITIES"], DatatypeTypes.All["string"] with { BaseType = "token" }, default })
        { Assert.False(Provider.TryParse(type, "a", Empty, Op(), out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.UnknownType, error); }
        var x = Parse("string", "a"); var peer = new TextBinaryDatatypeProvider();
        Assert.True(peer.TryParse(DatatypeTypes.All["string"], "a", Empty, Op(), out var y, out _));
        Assert.Throws<ArgumentException>(() => DatatypeValues.ValueEquals(x, y!, Op()));
    }
    [Theory]
    [InlineData("string", "根😀", 5, 2)]
    [InlineData("string", "", 1, 1)]
    [InlineData("boolean", "true", 6, 1)]
    [InlineData("hexBinary", "0011AA", 14, 2)]
    [InlineData("base64Binary", "AAE=", 12, 2)]
    [InlineData("base64Binary", "A A E =", 18, 2)]
    public void IndependentlyExpectedExactParseWorkAndComponents(string type, string input, long work, int components)
    {
        var exact = Op(work, components); Assert.True(Provider.TryParse(DatatypeTypes.All[type], input, Empty, exact, out _, out _)); Assert.Equal(0, exact.RemainingWork);
        if (work > 1) { var shortWork = Op(work - 1, components); Assert.False(Provider.TryParse(DatatypeTypes.All[type], input, Empty, shortWork, out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error); Assert.False(shortWork.IsComplete); }
        if (components > 1) { var shortComponents = Op(work, components - 1); Assert.False(Provider.TryParse(DatatypeTypes.All[type], input, Empty, shortComponents, out _, out var error)); Assert.Same(DatatypeErrors.Stopped, error); }
    }
    [Fact]
    public void ExactMeasuresHashAndScalarComparisonWork()
    {
        var x = Parse("string", "根😀"); var y = Parse("string", "根😀");
        var comparison = Op(5); Assert.True(DatatypeValues.ValueEquals(x, y, comparison)); Assert.Equal(0, comparison.RemainingWork);
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.Compare(x, y, Op(4)));
        var hash = Op(4); _ = DatatypeValues.ValueHashCode(x, hash); Assert.Equal(0, hash.RemainingWork);
        var length = Op(2); Assert.Equal(2L, DatatypeValues.ValueLength(x, length)); Assert.Equal(0, length.RemainingWork);
        var binary = Parse("hexBinary", "000102"); var bh = Op(5); _ = DatatypeValues.ValueHashCode(binary, bh); Assert.Equal(0, bh.RemainingWork);
        var bc = Op(5); Assert.True(DatatypeValues.ValueEquals(binary, binary, bc)); Assert.Equal(0, bc.RemainingWork);
    }
    [Fact]
    public void OwnedGrowthBoundaryAndCompletionRefusal()
    {
        foreach (int length in new[] { 255, 256, 257, 4097 })
        { string input = new('a', length); var exact = Op(1 + 2L * length, 1 + (length + 255) / 256); Assert.True(Provider.TryParse(DatatypeTypes.All["string"], input, Empty, exact, out _, out _)); }
        var final = new CallbackOperation(Op()) { RefuseAtZero = 2 };
        Assert.False(Provider.TryParse(DatatypeTypes.All["boolean"], "true", Empty, final, out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error);
    }
    [Fact]
    public void CancellationAndRefusalInterruptLongScansConversionsAndComparisons()
    {
        foreach (var pair in new[] { ("string", new string('a', 8192)), ("Name", new string('a', 8192)), ("hexBinary", new string('0', 8192)), ("base64Binary", new string('A', 8192)), ("anyURI", new string('a', 8192)), ("QName", new string('a', 8192)) })
        {
            using var source = new CancellationTokenSource(); var wrapper = new CallbackOperation(new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null }, CancellationToken = source.Token })) { CancelAtWork = 40, Source = source };
            Assert.Throws<OperationCanceledException>(() => Provider.TryParse(DatatypeTypes.All[pair.Item1], pair.Item2, Empty, wrapper, out _, out _)); Assert.InRange(wrapper.WorkCalls, 40, 41);
            var limited = Op(40); Assert.False(Provider.TryParse(DatatypeTypes.All[pair.Item1], pair.Item2, Empty, limited, out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error);
        }
        var text = Parse("string", new('a', 8192)); var binary = Parse("hexBinary", new('0', 8192));
        foreach (var value in new[] { text, binary })
        {
            using var source = new CancellationTokenSource(); var wrapper = new CallbackOperation(new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null }, CancellationToken = source.Token })) { CancelAtWork = 40, Source = source };
            Assert.Throws<OperationCanceledException>(() => DatatypeValues.ValueEquals(value, value, wrapper)); Assert.InRange(wrapper.WorkCalls, 40, 41);
            Assert.Throws<SchemaCompileException>(() => DatatypeValues.ValueHashCode(value, Op(40)));
        }
        // Cancellation during the second, copying/conversion pass, not only lexical validation.
        foreach (var pair in new[] { ("string", new string('a', 1024), 1050), ("hexBinary", new string('0', 1024), 1050), ("base64Binary", new string('A', 1024), 1050) })
        {
            using var source = new CancellationTokenSource(); var wrapper = new CallbackOperation(new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null }, CancellationToken = source.Token })) { CancelAtWork = pair.Item3, Source = source };
            Assert.Throws<OperationCanceledException>(() => Provider.TryParse(DatatypeTypes.All[pair.Item1], pair.Item2, Empty, wrapper, out _, out _));
        }
    }
    private static void Invalid(string type, string input)
    {
        var op = Op(); var metadata = DatatypeTypes.All[type]; string text = DatatypeNormalization.Normalize(input, metadata.Whitespace, op);
        Assert.False(Provider.TryParse(metadata, text, Empty, op, out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.InvalidLexical, error); Assert.True(op.IsComplete);
    }
    private sealed class CallbackOperation(SchemaCompileOperation inner) : ISchemaOperation
    {
        internal int WorkCalls, ZeroCalls; internal int CancelAtWork { get; init; } internal int RefuseAtZero { get; init; } internal CancellationTokenSource? Source { get; init; }
        public Xemele.XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken; public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, Xemele.XmlLocation location)
        { if (work > 0 && ++WorkCalls == CancelAtWork) Source!.Cancel(); if (work == 0 && ++ZeroCalls == RefuseAtZero) inner.Refuse("SCHEMA.Limit", "test completion refusal", default); return inner.Charge(work, location); }
        public bool ChargeComponents(int count, Xemele.XmlLocation location) => inner.ChargeComponents(count, location);
        public bool AddDiagnostic(Xemele.XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
        public void Refuse(string code, string message, Xemele.XmlLocation location) => inner.Refuse(code, message, location);
    }
}

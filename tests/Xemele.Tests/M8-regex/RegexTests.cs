using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8Regex;
public sealed class RegexTests
{
    private static readonly XmlSchemaRegexEngine Engine = new();
    private static XmlSchemaRegexProgram Compile(string pattern) => Engine.Compile(pattern, default, new SchemaCompileOperation());
    private static bool Match(string pattern, string input) => Engine.IsMatch(Compile(pattern), input, new SchemaValidationOperation());
    [Theory]
    [InlineData("", "", true)] [InlineData("", "a", false)]
    [InlineData("a|", "", true)] [InlineData("|a", "a", true)]
    [InlineData("abc", "xabc", false)] [InlineData("abc", "abcx", false)]
    [InlineData("^a$", "^a$", true)] [InlineData("^a$", "a", false)]
    [InlineData("(ab|cd)+e?", "abcdabe", true)] [InlineData("(ab|cd)+e?", "abce", false)]
    [InlineData("a{0}", "", true)] [InlineData("a{2,4}", "aaa", true)] [InlineData("a{2,4}", "aaaaa", false)]
    [InlineData("(a?){2,}", "", true)] [InlineData("(a?){2,}", "aaaa", true)]
    [InlineData("(a|)*", "aaa", true)] [InlineData("(a*)*b", "aaaab", true)]
    [InlineData("a{0002}", "aa", true)] [InlineData("a{1,}", "", false)]
    [InlineData("a{2,2}", "aa", true)] [InlineData("a{0,2}", "a", true)]
    [InlineData(".", "\n", false)] [InlineData(".", "\r", false)] [InlineData(".", "😀", true)]
    [InlineData("..", "😀", false)] [InlineData("[😀-🙏]", "😁", true)]
    [InlineData("[a-z-[aeiou]]+", "bcdfz", true)] [InlineData("[a-z-[aeiou]]+", "bad", false)]
    [InlineData("[a-z-[d-f-[e]]]", "e", true)] [InlineData("[a-z-[d-f-[e]]]", "d", false)]
    [InlineData("[^a-[b]]", "c", true)] [InlineData("[^a-[b]]", "b", false)]
    [InlineData("[-a]+", "-a", true)] [InlineData("[a-]+", "a-", true)]
    [InlineData("[a--[a]]", "-", true)] [InlineData("[a--[a]]", "a", false)]
    [InlineData("[a^]", "^", true)] [InlineData("[.]", ".", true)] [InlineData("[.]", "a", false)]
    [InlineData("[\\n-\\r]", "\t", false)] [InlineData("\\s+", " \t\r\n", true)]
    [InlineData("\\s", "\u00a0", false)] [InlineData("\\S", "\u00a0", true)]
    [InlineData("\\d+", "١२3", true)] [InlineData("\\D", "Ⅳ", true)]
    [InlineData("\\w", "_", false)] [InlineData("\\w", "😀", true)] [InlineData("\\W", "!", true)]
    [InlineData("\\i\\c*", "名:abc-._7", true)] [InlineData("\\i", "7", false)]
    [InlineData("\\I", "7", true)] [InlineData("\\C", "!", true)]
    [InlineData("\\i", "😀", false)] [InlineData("\\i", "\u037f", false)]
    [InlineData("\\p{Lu}", "Ω", true)] [InlineData("\\p{Lo}", "名", true)]
    [InlineData("\\p{L}", "𐐀", true)] [InlineData("\\P{L}", "😀", true)]
    [InlineData("\\p{IsBasicLatin}+", "ABC~", true)] [InlineData("\\p{IsBasicLatin}", "é", false)]
    [InlineData("\\p{IsGreek}", "Ω", true)] [InlineData("\\p{IsEmoticons}", "😀", true)]
    [InlineData("\\p{IsSpecials}", "\ufeff", true)] [InlineData("\\p{IsSpecials}", "\ufffd", true)]
    [InlineData("\\p{IsPrivateUse}", "\ue000", true)]
    [InlineData("\\p{IsCombiningMarksforSymbols}", "\u20d0", true)]
    [InlineData("\\P{IsBasicLatin}", "😀", true)]
    
    [InlineData(".*", "\u0000", false)] [InlineData(".*", "\uffff", false)]
    [InlineData("[\\p{L}\\p{N}]", "7", true)]
    [InlineData("\\n\\r\\t\\\\\\|\\.\\?\\*\\+\\(\\)\\{\\}\\-\\[\\]\\^", "\n\r\t\\|.?*+(){}-[]^", true)]
    public void AppendixFLanguage(string pattern, string input, bool expected) => Assert.Equal(expected, Match(pattern, input));

    [Theory]
    [InlineData("[")] [InlineData("[]")] [InlineData("[^]")] [InlineData("[z-a]")]
    [InlineData("[a-b-c]")] [InlineData("[a-\\d]")] [InlineData("[\\d-a]")]
    [InlineData("[a-[b]c]")] [InlineData("[a[b]]")] [InlineData("[a--b]")]
    [InlineData("[\\p{IsBasicLatin}-z]")] [InlineData("[a-\\p{IsLatin-1Supplement}]")]
    [InlineData("\\")] [InlineData("\\a")] [InlineData("\\b")] [InlineData("\\u0041")]
    [InlineData("\\x41")] [InlineData("\\$")] [InlineData("\\1")]
    [InlineData("\\p{Cs}")] [InlineData("\\p{}")] [InlineData("\\p{Invalid}")]
    [InlineData("\\p{IsNoSuchBlock}")] [InlineData("\\p{IsHighSurrogates}")]
    [InlineData("(")] [InlineData(")")] [InlineData("a)")] [InlineData("(?=a)")]
    [InlineData("a**")] [InlineData("a+?")] [InlineData("?a")] [InlineData("a{,3}")]
    [InlineData("a{3,2}")] [InlineData("a{2")] [InlineData("a{2, 3}")] [InlineData("a{}")]
    [InlineData("a{١}")] [InlineData("a}")] [InlineData("\u0000")]
    public void InvalidGrammarIsOwnedSyntaxFailure(string pattern)
    {
        var at = new XmlLocation(new Uri("urn:regex:source"), 3, 7, null); var op = new SchemaCompileOperation();
        var ex = Assert.Throws<SchemaCompileException>(() => Engine.Compile(pattern, at, op));
        var d = Assert.Single(ex.Diagnostics); Assert.Equal("REGEX.Syntax", d.Code); Assert.Equal(at, d.Location);
        Assert.Equal(XmlDiagnosticSeverity.Error, d.Severity); Assert.True(op.IsComplete);
    }

    [Theory]
    [InlineData("Lu", "A")] [InlineData("Ll", "a")] [InlineData("Lt", "ǅ")] [InlineData("Lm", "ʰ")] [InlineData("Lo", "名")]
    [InlineData("Mn", "\u0300")] [InlineData("Mc", "\u0903")] [InlineData("Me", "\u20dd")]
    [InlineData("Nd", "1")] [InlineData("Nl", "Ⅳ")] [InlineData("No", "²")]
    [InlineData("Zs", " ")] [InlineData("Zl", "\u2028")] [InlineData("Zp", "\u2029")]
    [InlineData("Cc", "\t")] [InlineData("Cf", "\u200e")] [InlineData("Co", "\ue000")] [InlineData("Cn", "\u0378")]
    [InlineData("Pc", "_")] [InlineData("Pd", "-")] [InlineData("Ps", "(")] [InlineData("Pe", ")")]
    [InlineData("Pi", "“")] [InlineData("Pf", "”")] [InlineData("Po", "!")]
    [InlineData("Sm", "+")] [InlineData("Sc", "$")] [InlineData("Sk", "^")] [InlineData("So", "😀")]
    public void EveryScalarCategoryHasPositiveAndComplementCases(string category, string scalar)
    { Assert.True(Match("\\p{" + category + "}", scalar)); Assert.False(Match("\\P{" + category + "}", scalar)); }

    [Fact]
    public void EveryPinnedUnicodeBlockHasScalarBoundaryAndOutsideCases()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-regex/block-boundaries.tsv"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var lines = File.ReadAllLines(Path.Combine(directory.FullName, "tests/fixtures/M8-regex/block-boundaries.tsv"));
        Assert.True(lines.Length > 300);
        foreach (string line in lines)
        {
            var fields = line.Split('\t'); int first = Convert.ToInt32(fields[1], 16), last = Convert.ToInt32(fields[2], 16);
            var program = Compile("\\p{Is" + fields[0] + "}");
            foreach (int scalar in new[] { first, last, first - 1, last + 1 })
            {
                if (!System.Text.Rune.IsValid(scalar)) continue;
                bool expected = RegexCharacters.IsXml(scalar) && scalar >= first && scalar <= last;
                Assert.Equal(expected, Engine.IsMatch(program, char.ConvertFromUtf32(scalar).AsSpan(), new SchemaValidationOperation()));
            }
        }
    }
    [Fact]
    public void IndependentFiniteLanguageOracleExercisesComposition()
    {
        var languages = new (string Pattern, string[] Words)[]
        { ("a", ["a"]), ("b?", ["", "b"]), ("a|bb", ["a", "bb"]), ("[ab]", ["a", "b"]), ("(a?){0,2}", ["", "a", "aa"]) };
        foreach (var left in languages) foreach (var right in languages)
        {
            var program = Compile("(" + left.Pattern + ")(" + right.Pattern + ")");
            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in left.Words) foreach (var b in right.Words) words.Add(a + b);
            for (int length = 0; length <= 5; length++) for (int bits = 0; bits < 1 << length; bits++)
            {
                string input = string.Create(length, bits, (span, value) => { for (int i = 0; i < span.Length; i++) span[i] = (value & (1 << i)) == 0 ? 'a' : 'b'; });
                Assert.Equal(words.Contains(input), Engine.IsMatch(program, input, new SchemaValidationOperation()));
            }
        }
    }
    [Fact]
    public void SharedConstructionAndTransitionQuotasHaveExactBoundaries()
    {
        var counter = new CountingOperation(); Engine.Compile("(ab|c){2,4}", default, counter);
        var exact = new CountingOperation(counter.Work, counter.Components); Engine.Compile("(ab|c){2,4}", default, exact); Assert.True(exact.IsComplete);
        foreach (var op in new[] { new CountingOperation(counter.Work - 1), new CountingOperation(long.MaxValue, counter.Components - 1) })
        { Assert.Throws<SchemaCompileException>(() => Engine.Compile("(ab|c){2,4}", default, op)); Assert.False(op.IsComplete); }
        var program = Compile("(ab|c){2,4}"); var run = new CountingOperation(); Assert.True(Engine.IsMatch(program, "abc".AsSpan(), run));
        var runExact = new CountingOperation(run.Work); Assert.True(Engine.IsMatch(program, "abc".AsSpan(), runExact)); Assert.Equal(0, runExact.RemainingWork);
        var oneShort = new CountingOperation(run.Work - 1); Assert.False(Engine.IsMatch(program, "abc".AsSpan(), oneShort)); Assert.False(oneShort.IsComplete);
        var shared = new CountingOperation(run.Work * 2); Assert.True(Engine.IsMatch(program, "abc".AsSpan(), shared)); Assert.True(Engine.IsMatch(program, "abc".AsSpan(), shared)); Assert.False(Engine.IsMatch(program, "abc".AsSpan(), shared));
        var real = new SchemaCompileOperation(new() { MaxComponents = 20 }); Assert.Throws<SchemaCompileException>(() => Engine.Compile("a{1000000}", default, real)); Assert.False(real.IsComplete);
        Assert.Throws<SchemaCompileException>(() => Compile("a{9999999999999999999999999999999}"));
    }
    [Fact]
    public void ScalarLengthAndNestingRefuseOneOverAndUnlimitedStillHasRepresentationBounds()
    {
        var op = new SchemaCompileOperation(new() { Limits = new() { TokenCharacters = 1 } }); Engine.Compile("😀", default, op);
        Assert.Throws<SchemaCompileException>(() => Engine.Compile("😀a", default, new SchemaCompileOperation(new() { Limits = new() { TokenCharacters = 1 } })));
        var program = Compile(".*"); var exact = new SchemaValidationOperation(new() { Limits = new() { TokenCharacters = 2 } }); Assert.True(Engine.IsMatch(program, "😀a".AsSpan(), exact));
        var shortOp = new SchemaValidationOperation(new() { Limits = new() { TokenCharacters = 1 } }); Assert.False(Engine.IsMatch(program, "😀a".AsSpan(), shortOp)); Assert.False(shortOp.IsComplete);
        Engine.Compile("((a))", default, new SchemaCompileOperation(new() { Limits = new() { Depth = 3 } }));
        Assert.Throws<SchemaCompileException>(() => Engine.Compile("(((a)))", default, new SchemaCompileOperation(new() { Limits = new() { Depth = 3 } })));
        Assert.Throws<SchemaCompileException>(() => Engine.Compile(new string('(', 600) + "a" + new string(')', 600), default, new SchemaCompileOperation(new() { Limits = new() { Depth = null } })));
    }
    [Fact]
    public void EntryAndMidOperationCancellationPropagateAndRefusedLedgerCannotSucceed()
    {
        using var source = new CancellationTokenSource(); var op = new CountingOperation(cancelAt: 5, source: source);
        Assert.Throws<OperationCanceledException>(() => Engine.Compile("[a-z-[bc]]+", default, op));
        var program = Compile("a+"); using var runSource = new CancellationTokenSource();
        var run = new CountingOperation(cancelAt: 8, source: runSource);
        Assert.Throws<OperationCanceledException>(() => Engine.IsMatch(program, new string('a', 100).AsSpan(), run));
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); var entry = new CountingOperation(source: canceled);
        Assert.Throws<OperationCanceledException>(() => Engine.IsMatch(program, "a".AsSpan(), entry));
        Assert.Throws<OperationCanceledException>(() => Engine.Compile("a", default, entry));
        var refused = new CountingOperation(0); Assert.False(refused.Charge(1, default)); Assert.False(Engine.IsMatch(Compile(""), ReadOnlySpan<char>.Empty, refused));
    }
    [Fact]
    public async Task OneProgramSupportsConcurrentIndependentRunsIncludingPooledScratch()
    {
        var program = Compile("[a-z-[aeiou]]{1,200}");
        var tasks = Enumerable.Range(0, 16).Select(i => Task.Run(() =>
        { for (int j = 0; j < 25; j++) { var op = new SchemaValidationOperation(); Assert.Equal(i % 2 == 0, Engine.IsMatch(program, (i % 2 == 0 ? "bcdf" : "aeiou").AsSpan(), op)); Assert.True(op.IsComplete); } }, TestContext.Current.CancellationToken));
        await Task.WhenAll(tasks);
    }
    [Fact]
    public void RuntimeConstructedSurrogatesAndInvalidEscapedScalarsReject()
    {
        foreach (char c in new[] { (char)0xD800, (char)0xDC00 })
        {
            string input = new(c, 1);
            Assert.False(Match(".*", input));
            Assert.Throws<SchemaCompileException>(() => Compile(input));
        }
        foreach (int scalar in new[] { 0x1005E, 0x1002E })
        {
            string escape = "\\" + char.ConvertFromUtf32(scalar);
            Assert.Throws<SchemaCompileException>(() => Compile(escape));
            Assert.Throws<SchemaCompileException>(() => Compile("[" + escape + "]"));
        }
    }
    [Fact]
    public void NullAndForeignProgramsRejectBeforeWork()
    {
        Assert.Throws<ArgumentNullException>(() => Engine.Compile(null!, default, new SchemaCompileOperation()));
        Assert.Throws<ArgumentNullException>(() => Engine.Compile("", default, null!));
        Assert.Throws<ArgumentNullException>(() => Engine.IsMatch(null!, "".AsSpan(), new SchemaValidationOperation()));
        Assert.Throws<ArgumentException>(() => Engine.IsMatch(new ForeignProgram(), "".AsSpan(), new SchemaValidationOperation()));
    }
    private sealed class ForeignProgram : XmlSchemaRegexProgram { }
    private sealed class CountingOperation(long max = long.MaxValue, int componentMax = int.MaxValue, long cancelAt = long.MaxValue, CancellationTokenSource? source = null) : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new() { TokenCharacters = null, EvaluationWork = null };
        public CancellationToken CancellationToken => source?.Token ?? default;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => max - Work;
        internal long Work; internal int Components;
        public bool Charge(long work, XmlLocation location)
        {
            if (Work >= cancelAt) source?.Cancel(); CancellationToken.ThrowIfCancellationRequested();
            if (!IsComplete) return false; if (work > max - Work) { Refuse("SCHEMA.Limit", "work", location); return false; } Work += work; return true;
        }
        public bool ChargeComponents(int count, XmlLocation location)
        { CancellationToken.ThrowIfCancellationRequested(); if (!IsComplete) return false; if (count > componentMax - Components) { Refuse("SCHEMA.Limit", "components", location); return false; } Components += count; return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => true;
        public void Refuse(string code, string message, XmlLocation location) => IsComplete = false;
    }
}

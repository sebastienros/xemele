using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Xemele.Html;
namespace Xemele.Benchmarks.HtmlTokenization;

[MemoryDiagnoser, ShortRunJob, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TokenizerBenchmarks
{
    [Params("short-hit", "short-miss", "long-late-hit", "long-miss", "dense", "multilingual")]
    public string Case { get; set; } = "";
    private string input = "";
    private string owned = "";
    [GlobalSetup]
    public void Setup()
    {
        input = PrimitiveInput(Case);
        owned = OwnedFixture(Case);
    }
    [Benchmark(Baseline = true), BenchmarkCategory("primitive")] public int FrameworkBaseline() => FrameworkDigits(input);
    [Benchmark, BenchmarkCategory("primitive")] public int RangeDigits() => Digits(input);
    internal static string PrimitiveInput(string kind) => kind switch
    {
        "short-hit" => "123aF",
        "short-miss" => "xYZ!?",
        "long-late-hit" => new string('x', 4095) + "F",
        "long-miss" => new string('x', 4096),
        "dense" => string.Concat(Enumerable.Repeat("0123456789abcdefABCDEF;#&", 128)),
        "multilingual" => "123尾😀éＦff",
        _ => throw new ArgumentException(),
    };
    internal static string ReferenceInput(string kind) => kind switch
    {
        "short-hit" => "amp;",
        "short-miss" => "xyz;",
        "long-late-hit" => "CounterClockwiseContourIntegral;",
        "long-miss" => new string('x', 4096),
        "dense" => "NotEqualTilde;",
        "multilingual" => "amp;尾😀",
        _ => throw new ArgumentException(),
    };
    internal static int Digits(ReadOnlySpan<char> input)
    {
        var result = 0;
        foreach (var c in input)
        {
            result = unchecked(result * 17 + HtmlTokenizer.Digit(c, 16));
        }

        return result;
    }
    internal static int FrameworkDigits(ReadOnlySpan<char> input)
    {
        var result = 0;
        foreach (var c in input)
        {
            var value = char.IsAsciiHexDigit(c) ? c <= '9' ? c - '0' : (c | 32) - 'a' + 10 : -1;
            result = unchecked(result * 17 + value);
        }
        return result;
    }
    internal const string OwnedInput = "<!doctype html><?Build raw &amp; 😀?><div a='&amp;' b='😀'>owned multilingual 尾巴 &notin;<!-- comment --><script><!--<script>x</script>--></script></div>";
    internal static string OwnedFixture(string kind) => kind switch
    {
        "short-hit" => "a&amp;b",
        "short-miss" => "&notit; &unknown;",
        "long-late-hit" => new string('a', 4095) + "&amp;",
        "long-miss" => new string('a', 4096),
        "dense" => OwnedInput,
        "multilingual" => "<p a='😀' a=ignored>尾巴&#x1F600;</p>",
        _ => throw new ArgumentException(),
    };
    [Benchmark(Baseline = true), BenchmarkCategory("owned")] public IReadOnlyList<HtmlToken> WholeInput() => Html5Tokenizer.Tokenize(owned);
    [Benchmark, BenchmarkCategory("owned")] public IReadOnlyList<HtmlToken> ChunkedInput() => ChunkedTokens(owned);
    internal static IReadOnlyList<HtmlToken> ChunkedTokens(string input)
    {
        var tokens = new List<HtmlToken>();
        var tokenizer = new HtmlTokenizer(tokens.Add, new());
        for (var i = 0; i < input.Length; i++)
        {
            tokenizer.Feed(input.AsSpan(i, 1));
        }

        tokenizer.Complete();
        return tokens.AsReadOnly();
    }
    internal static int OwnedCallback(string input)
    {
        var result = 0;
        Html5Tokenizer.Tokenize(input, t => result += t.Name.Length + t.Data.Length + t.Attributes.Count);
        return result;
    }
}

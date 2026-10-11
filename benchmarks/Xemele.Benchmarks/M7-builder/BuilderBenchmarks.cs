using System.Buffers;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Xemele.Html;

namespace Xemele.Benchmarks.HtmlTreeConstruction;

[MemoryDiagnoser, ShortRunJob, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BuilderBenchmarks
{
    static BuilderBenchmarks() { }
    [Params("short-hit", "short-miss", "long-late-hit", "long-miss", "dense", "multilingual")]
    public string Case { get; set; } = "";
    private byte[] prescan = [], bytes = [];
    private string label = "", owned = "";
    private byte[] labelBytes = [];
    private HtmlFragmentContext context = null!;
    private static readonly SearchValues<byte> Markup = SearchValues.Create("<"u8);
    private static (string Label, byte[] Bytes, string Encoding)[] labels = [];
    internal const string Composition = "<!doctype html><body><select><button><selectedcontent></button><option selected><b>owned</b> 尾😀</option></select><div><?marker name='x'?></div><template for=x><i>patch</i></template><table>x<tr><td>cell</table><svg><foreignObject><p>foreign</svg><template><div>inert</template>";

    [GlobalSetup]
    public void Setup()
    {
        InitializeLabels(); prescan = PrescanInput(Case); label = LabelInput(Case); labelBytes = Encoding.UTF8.GetBytes(label);
        owned = OwnedInput(Case); bytes = Encoding.UTF8.GetBytes("<meta charset=utf-8>" + owned);
        context = new(new HtmlDocument().CreateElement("body"));
    }
    internal static void InitializeLabels()
    {
        if (labels.Length != 0) return;
        using var json = JsonDocument.Parse(File.ReadAllText("tests/fixtures/M7-builder/encoding/encodings.json"));
        var list = new List<(string, byte[], string)>();
        foreach (var group in json.RootElement.EnumerateArray()) foreach (var encoding in group.GetProperty("encodings").EnumerateArray())
            foreach (var label in encoding.GetProperty("labels").EnumerateArray())
                list.Add((label.GetString()!, Encoding.ASCII.GetBytes(label.GetString()!), encoding.GetProperty("name").GetString()!.ToLowerInvariant()));
        labels = list.ToArray();
    }
    internal static string LabelInput(string kind) => kind switch
    {
        "short-hit" => "utf-8", "short-miss" => "nope", "long-late-hit" => "  CsIsOlAtIn1  ",
        "long-miss" => new string('x', 128), "dense" => "\t\r\nwindows-1252\f ", "multilingual" => "utf-8尾😀", _ => throw new ArgumentException()
    };
    internal static byte[] PrescanInput(string kind) => Encoding.UTF8.GetBytes(kind switch
    {
        "short-hit" => "<meta charset=UTF-8>", "short-miss" => "<meta charset=unknown>",
        "long-late-hit" => new string('x', 970) + "<meta charset=utf-8>", "long-miss" => new string('x', 1024),
        "dense" => string.Concat(Enumerable.Repeat("<i a=b a=c>", 40)) + "<meta http-equiv=content-type content='text/html; charset=windows-1251'>",
        "multilingual" => "尾😀<meta 情報='é尾😀' CHARSET='UtF-8' charset=ignored>", _ => throw new ArgumentException()
    });
    internal static string OwnedInput(string kind) => kind switch
    {
        "short-hit" => "<p>a&amp;b", "short-miss" => "<p>&unknown; &notit;",
        "long-late-hit" => "<p>" + new string('x', 4096) + "<b>end", "long-miss" => new string('x', 4096),
        "dense" => Composition, "multilingual" => "<p a='尾😀' a=duplicate>😀&NotEqualTilde;尾</p>", _ => throw new ArgumentException()
    };
    internal static string? LinearLabel(ReadOnlySpan<byte> input)
    {
        input = input.Trim((ReadOnlySpan<byte>)[9,10,12,13,32]);
        foreach (var row in labels) if (HtmlAscii.Equals(input, row.Bytes)) return row.Encoding;
        return null;
    }
    internal static string? LinearLabel(ReadOnlySpan<char> input)
    {
        input = input.Trim("\t\n\f\r ".AsSpan());
        foreach (var row in labels) if (HtmlAscii.Equals(input, row.Label)) return row.Encoding;
        return null;
    }
    internal static int ScalarScan(ReadOnlySpan<byte> input) { for (int i=0;i<input.Length;i++) if(input[i]=='<') return i; return -1; }
    internal static int IndexScan(ReadOnlySpan<byte> input) => input.IndexOf((byte)'<');
    internal static int SmallSetScan(ReadOnlySpan<byte> input) => input.IndexOfAny((byte)'<', (byte)'<');
    internal static int SearchValuesScan(ReadOnlySpan<byte> input) => input.IndexOfAny(Markup);

    [Benchmark(Baseline=true), BenchmarkCategory("prescan")] public (string,int) AllocatingBaseline() => AllocatingPrescanBaseline.Sniff(prescan);
    [Benchmark, BenchmarkCategory("prescan")] public (string,int) BorrowedPrescan() => HtmlByteInput.Sniff(prescan);
    [Benchmark(Baseline=true), BenchmarkCategory("label")] public int LinearByteAndCharLabels() => (LinearLabel(labelBytes)?.Length??0)+(LinearLabel(label)?.Length??0);
    [Benchmark, BenchmarkCategory("label")] public int GeneratedByteAndCharLabels() => (HtmlEncodingTables.Label(labelBytes)?.Length??0)+(HtmlEncodingTables.Label(label)?.Length??0);
    [Benchmark(Baseline=true), BenchmarkCategory("scan")] public int Scalar() => ScalarScan(prescan);
    [Benchmark, BenchmarkCategory("scan")] public int IndexOf() => IndexScan(prescan);
    [Benchmark, BenchmarkCategory("scan")] public int SmallSet() => SmallSetScan(prescan);
    [Benchmark, BenchmarkCategory("scan")] public int CachedSearchValues() => SearchValuesScan(prescan);
    [Benchmark(Baseline=true), BenchmarkCategory("owned")] public HtmlDocument CharacterDocument() => Html5Parser.Parse(owned);
    [Benchmark, BenchmarkCategory("owned")] public HtmlDocument ByteDocument() => Html5Parser.Parse(bytes);
    [Benchmark, BenchmarkCategory("owned")] public HtmlDocument CallerStreamDocument() { using var stream=new MemoryStream(bytes,false); return Html5Parser.Load(stream); }
    [Benchmark, BenchmarkCategory("owned")] public HtmlNode ContextualFragment() => Html5Parser.ParseFragment(owned,context);
}

using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Xemele.Css;
using Xemele.Html;
namespace Xemele.Benchmarks.Css;

[MemoryDiagnoser, ShortRunJob]
public class TokenScanBenchmarks
{
    private static readonly char[] Separators = [' ', '\t', '\r', '\n', '\f'];
    private static readonly SearchValues<char> Stops = SearchValues.Create(" \t\r\n\f");
    public static readonly Func<string, string, bool> Production = typeof(CssSelector).GetMethod("ContainsToken", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<string, string, bool>>();
    [Params(0, 1, 2, 3)] public int Scenario { get; set; }
    private string value = "", token = "";
    [GlobalSetup] public void Setup()
    {
        (value, token) = Inputs[Scenario];
        bool expected = SplitBaseline();
        if (Production(value, token) != expected || CachedStops() != expected || SmallStops() != expected) throw new Exception("Token oracle mismatch.");
    }
    public static readonly (string Value, string Token)[] Inputs =
    [ ("one two", "two"), (new string('a', 256) + "\ttarget\n" + new string('b', 256), "target"),
      ("é\u00a0x\t日本語\r😀\n" + new string('x', 128), "missing"), ("target\f" + new string('z', 1024), "target") ];
    [Benchmark(Baseline = true)] public bool SplitBaseline()
    {
        if (token.Length == 0) return false;
        foreach (var part in value.Split(Separators, StringSplitOptions.RemoveEmptyEntries)) if (part == token) return true;
        return false;
    }
    [Benchmark] public bool ProductionLoop() => Production(value, token);
    [Benchmark] public bool CachedStops() => Scan(true);
    [Benchmark] public bool SmallStops() => Scan(false);
    private bool Scan(bool cached)
    {
        if (token.Length == 0) return false;
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            int stop = cached ? remaining.IndexOfAny(Stops) : remaining.IndexOfAny(" \t\r\n\f");
            if (stop < 0) return remaining.SequenceEqual(token);
            if (remaining[..stop].SequenceEqual(token)) return true;
            remaining = remaining[(stop + 1)..];
        }
        return false;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class CompositionBenchmarks
{
    private XmlDocument xml = null!;
    private HtmlDocument html = null!;
    private CssSelector selector = null!;
    [GlobalSetup] public void Setup()
    {
        (xml, html) = Graph(); selector = CssSelector.Compile("root > item.selected:nth-child(2n+1):not([data=absent]), #hit");
        if (selector.Select(xml.DocumentNode).Count != 17 || selector.Select(html.DocumentNode).Count != 17) throw new Exception("Composition oracle mismatch.");
    }
    public static (XmlDocument, HtmlDocument) Graph()
    {
        var x = new XmlDocument(); var h = new HtmlDocument();
        var xr = x.CreateElement("root"); var hr = h.CreateElement("root"); x.DocumentNode.AppendChild(xr); h.DocumentNode.AppendChild(hr);
        for (int i = 0; i < 64; i++)
        {
            var xe = x.CreateElement("item"); var he = h.CreateElement("item"); xr.AppendChild(xe); hr.AppendChild(he);
            string classes = i % 4 == 0 ? "ordinary selected 日本語" : "ordinary";
            xe.SetAttribute("class", classes); he.SetAttribute(new("class", classes));
            xe.SetAttribute("data", "en-US"); he.SetAttribute(new("data", "en-US"));
            if (i == 63) { xe.SetAttribute("id", "hit"); he.SetAttribute(new("id", "hit")); }
        }
        return (x, h);
    }
    [Benchmark] public bool XmlMatches() => selector.Matches(xml.RootElement.FirstChild);
    [Benchmark] public bool HtmlMatches() => selector.Matches(html.RootElement.FirstChild!);
    [Benchmark] public IReadOnlyList<XmlNode> XmlQuerySnapshot() => selector.Select(xml.DocumentNode);
    [Benchmark] public IReadOnlyList<HtmlNode> HtmlQuerySnapshot() => selector.Select(html.DocumentNode);
    [Benchmark] public CssSelector OwnedCompile() => CssSelector.Compile("root > item.selected:nth-child(2n+1):not([data=absent]), #hit");
}

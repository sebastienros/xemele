using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using HtmlParser = AngleSharp.Html.Parser.HtmlParser;
using BenchmarkDotNet.Attributes;
using Xemele.Css;
using Xemele.Html;
using XDocument = Xemele.Html.HtmlDocument;

namespace HtmlComparison;

// Each pair returns a fresh owned DOM, result snapshot, or string. Corpus creation,
// parsing for queries/output, context creation and all oracles are outside timing.
[MemoryDiagnoser, ShortRunJob]
public class ParseBenchmarks
{
    [ParamsSource(typeof(Fixtures), nameof(Fixtures.Names))] public string Corpus { get; set; } = "Small";
    private string input = null!;
    private HtmlParser parser = null!;
    [GlobalSetup]
    public void Setup()
    {
        input = Fixtures.Document(Corpus);
        parser = Fixtures.Parser();
        Qualification.Document(input);
    }
    [Benchmark(Baseline = true)] public IDocument AngleSharp() => parser.ParseDocument(input);
    [Benchmark] public XDocument Xemele() => Html5Parser.Parse(input);
}

[MemoryDiagnoser, ShortRunJob]
public class StreamParseBenchmarks
{
    [ParamsSource(typeof(Fixtures), nameof(Fixtures.Names))] public string Corpus { get; set; } = "Small";
    private byte[] input = null!;
    private HtmlParser parser = null!;
    [GlobalSetup]
    public void Setup()
    {
        input = Encoding.UTF8.GetBytes(Fixtures.Document(Corpus));
        parser = Fixtures.Parser();
        Qualification.Stream(input);
    }
    [Benchmark(Baseline = true)]
    public IDocument AngleSharp()
    {
        using var s = new MemoryStream(input, false);
        return parser.ParseDocument(s);
    }
    [Benchmark]
    public XDocument Xemele()
    {
        using var s = new MemoryStream(input, false);
        return Html5Parser.Load(s);
    }
}

[MemoryDiagnoser, ShortRunJob]
public class QueryBenchmarks
{
    [ParamsSource(typeof(Fixtures), nameof(Fixtures.Names))] public string Corpus { get; set; } = "Small";
    private IDocument angle = null!;
    private XDocument xemele = null!;
    [GlobalSetup]
    public void Setup()
    {
        string input = Fixtures.Document(Corpus);
        Qualification.Document(input);
        angle = Fixtures.Parser().ParseDocument(input);
        xemele = Html5Parser.Parse(input);
        Qualification.Query(angle, xemele, Fixtures.Selector);
    }
    // Use public string-selector APIs, retaining each library's default internal caching.
    [Benchmark(Baseline = true)] public IHtmlCollection<IElement> AngleSharp() => angle.QuerySelectorAll(Fixtures.Selector);
    [Benchmark] public IReadOnlyList<HtmlNode> Xemele() => CssSelector.Compile(Fixtures.Selector).Select(xemele.DocumentNode);
}

[MemoryDiagnoser, ShortRunJob]
public class SerializeBenchmarks
{
    [ParamsSource(typeof(Fixtures), nameof(Fixtures.Names))] public string Corpus { get; set; } = "Small";
    private IDocument angle = null!;
    private XDocument xemele = null!;
    [GlobalSetup]
    public void Setup()
    {
        string input = Fixtures.Document(Corpus);
        Qualification.Document(input);
        angle = Fixtures.Parser().ParseDocument(input);
        xemele = Html5Parser.Parse(input);
        Qualification.Equal(angle.ToHtml(), HtmlSerializer.Serialize(xemele), "serialization");
    }
    [Benchmark(Baseline = true)] public string AngleSharp() => angle.ToHtml();
    [Benchmark] public string Xemele() => HtmlSerializer.Serialize(xemele);
}

[MemoryDiagnoser, ShortRunJob]
public class FragmentBenchmarks
{
    [Params("Table", "Textarea", "Template")] public string Context { get; set; } = "Table";
    private string input = null!;
    private IElement angleContext = null!;
    private HtmlFragmentContext xemeleContext = null!;
    private HtmlParser parser = null!;
    [GlobalSetup]
    public void Setup()
    {
        var fixture = Fixtures.Fragment(Context);
        input = fixture.Input;
        parser = Fixtures.Parser();
        angleContext = parser.ParseDocument("<!doctype html>").CreateElement(fixture.Context);
        xemeleContext = new(new XDocument().CreateElement(fixture.Context));
        Qualification.Fragment(input, fixture.Context);
    }
    [Benchmark(Baseline = true)] public INodeList AngleSharp() => parser.ParseFragment(input, angleContext);
    [Benchmark] public HtmlNode Xemele() => Html5Parser.ParseFragment(input, xemeleContext);
}

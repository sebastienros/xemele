using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.Html;

namespace Xemele.Benchmarks.HtmlParsing;

// Unlimited-depth documents whose open-element stack grows with N. Before open-element marks and
// per-name open counts, adoption, reconstruction and "p in button scope" scanned the stack per token.
[MemoryDiagnoser, ShortRunJob]
public class DeepNestingBenchmarks
{
    private static readonly Html5ParseOptions Unlimited = new() { Limits = new() { Depth = null } };
    [Params("adoption", "reconstruct", "blocks")]
    public string Shape { get; set; } = "";
    [Params(1_000, 10_000, 50_000)]
    public int N { get; set; }
    private string input = "";
    [GlobalSetup]
    public void Setup() => input = Input(Shape, N);
    [Benchmark]
    public HtmlDocument Parse() => Html5Parser.Parse(input, Unlimited);
    internal static string Input(string shape, int n)
    {
        var s = new StringBuilder();
        switch (shape)
        {
            case "adoption":
                // Each </b> runs the adoption agency for a formatting element at the top of a deep stack.
                for (var i = 0; i < n; i++)
                {
                    s.Append("<div><b>t</b>");
                }
                break;
            case "reconstruct":
                // Each start tag reconstructs formatting elements while an open <b> sits on a deep stack.
                s.Insert(0, "<div>", n).Append("<b>");
                for (var i = 0; i < n; i++)
                {
                    s.Append("<span>x</span>");
                }
                break;
            case "blocks":
                // Each block start tag asks whether a p element is in button scope.
                for (var i = 0; i < n; i++)
                {
                    s.Append("<div>x");
                }
                break;
            default:
                throw new ArgumentException(shape);
        }
        return s.ToString();
    }
}

// Pages whose attribute values carry terminated references, which the whole-tag fast path resolves inline.
[MemoryDiagnoser, ShortRunJob]
public class ReferenceAttributeBenchmarks
{
    [Params("dense", "sparse")]
    public string Case { get; set; } = "";
    private string input = "";
    [GlobalSetup]
    public void Setup() => input = Input(Case);
    [Benchmark]
    public HtmlDocument Parse() => Html5Parser.Parse(input);
    internal static string Input(string kind)
    {
        var s = new StringBuilder("<!doctype html><html><head><meta charset=utf-8><title>Results &amp; more</title></head><body><ul class=results>");
        for (var i = 0; i < 128; i++)
        {
            if (kind == "dense")
            {
                s.Append("<li class=\"item\" data-id=\"").Append(i).Append("\"><a href=\"/search?q=item-").Append(i)
                    .Append("&amp;page=2&amp;sort=asc\" title=\"&quot;Item ").Append(i).Append("&quot; &mdash; it&#39;s &lt;new&gt;\">Item ")
                    .Append(i).Append(" &amp; more</a> <img src='/i/").Append(i).Append(".png' alt='&quot;thumb&quot;'></li>");
            }
            else
            {
                s.Append("<li class=\"item\" data-id=\"").Append(i).Append("\"><a href=\"/item/").Append(i).Append(i % 8 == 0 ? "?a=1&amp;b=2" : "")
                    .Append("\" title=\"Item ").Append(i).Append("\">Item ").Append(i).Append("</a> <img src='/i/").Append(i).Append(".png' alt=thumb></li>");
            }
        }
        return s.Append("</ul></body></html>").ToString();
    }
}

using BenchmarkDotNet.Attributes;
using System.Text.Json;
using Xemele.Html;
namespace Xemele.Benchmarks.HtmlModel;

[MemoryDiagnoser, ShortRunJob]
public class ModelBenchmarks
{
    [Params("amp;", "CounterClockwiseContourIntegral;", "notin;", "notit;", "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz;", "é;")] public string Input { get; set; } = "";
    private KeyValuePair<string, string>[] entries = null!;
    [GlobalSetup]
    public void Setup()
    {
        using var j = JsonDocument.Parse(File.ReadAllText("tests/fixtures/M7-html-model/entities.json"));
        entries = [.. j.RootElement.EnumerateObject().Select(e => new KeyValuePair<string, string>(e.Name[1..], e.Value.GetProperty("characters").GetString()!))];
    }
    [Benchmark(Baseline = true)]
    public string Baseline()
    {
        foreach (var entry in entries)
        {
            if (entry.Key.Length == Input.Length && Input.AsSpan().SequenceEqual(entry.Key))
            {
                return entry.Value;
            }
        }

        return "";
    }
    [Benchmark]
    public string BorrowedBinary()
    {
        HtmlNamedReferences.TryGet(Input, out var value);
        return value;
    }
    public static HtmlDocument OwnedGraph()
    {
        var d = new HtmlDocument();
        var root = d.CreateElement("html");
        d.DocumentNode.AppendChild(root);
        for (var i = 0; i < 32; i++)
        {
            var e = d.CreateElement("p");
            root.AppendChild(e);
            e.SetAttribute(new("class", "same"));
            e.AppendChild(d.CreateText("owned multilingual text 尾巴"));
        }
        return d;
    }
}

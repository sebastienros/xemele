using System.Buffers;
using BenchmarkDotNet.Attributes;

namespace Xemele.XmlPerformance;

[MemoryDiagnoser, ShortRunJob]
public class AttributeScans
{
    private static readonly SearchValues<char> Stops = SearchValues.Create("\"&<\t\n\r");
    private string value = "";
    [Params(8, 4096)] public int Length { get; set; }
    [Params("miss", "last", "dense", "multilingual")] public string Shape { get; set; } = "miss";
    [GlobalSetup] public void Setup()
    {
        value = new string('a', Length);
        if (Shape == "last") value = value[..^1] + "&";
        if (Shape == "dense") value = "abc&" + value[4..];
        if (Shape == "multilingual") value = string.Concat(Enumerable.Repeat("α日本語😀", Length / 6 + 1))[..Length];
        if (Loop() != Cached() || Loop() != SpanSet()) throw new InvalidOperationException("Scan oracle differs.");
    }
    [Benchmark(Baseline = true)] public int Loop()
    {
        var input = value.AsSpan();
        for (int i = 0; i < input.Length; i++) if (input[i] is '\"' or '&' or '<' or '\t' or '\n' or '\r') return i;
        return -1;
    }
    [Benchmark] public int Cached() => value.AsSpan().IndexOfAny(Stops);
    [Benchmark] public int SpanSet() => value.AsSpan().IndexOfAny("\"&<\t\n\r");
}

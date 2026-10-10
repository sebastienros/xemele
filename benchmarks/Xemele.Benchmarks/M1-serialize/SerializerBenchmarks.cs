using System.Buffers;
using BenchmarkDotNet.Attributes;
namespace Xemele.SerializationMeasurements;
[MemoryDiagnoser, ShortRunJob]
public class SerializerBenchmarks
{
    [Params("short", "long", "multilingual", "namespaces")] public string Corpus { get; set; } = "short";
    private XmlDocument document = null!;
    private XmlSaveOptions options = null!;
    [GlobalSetup]
    public void Setup()
    {
        document = new(); var r = document.CreateElement("r"); document.DocumentNode.AppendChild(r);
        if (Corpus == "namespaces")
        {
            for (int i = 0; i < 64; i++) r.SetAttribute("a" + i, "v", "urn:" + i);
        }
        else r.AppendChild(document.CreateText(Corpus switch { "long" => new string('x', 65536), "multilingual" => string.Concat(Enumerable.Repeat("界🙂&<\r", 4096)), _ => "a&<b" }));
        options = new() { OmitXmlDeclaration = true, NamespaceHandling = XmlNamespaceHandling.Repair };
    }
    [Benchmark] public int OwnedString() => document.ToXmlString(options).Length;
    [Benchmark] public void IncrementalStream() => document.Save(Stream.Null, options);
}
// Alternatives have identical delimiter-recognition semantics. Setup and input
// generation stay outside timing; no custom SIMD or token hashes are proposed.
[MemoryDiagnoser, ShortRunJob]
public class EscapeScanBenchmarks
{
    private static readonly SearchValues<char> Stops = SearchValues.Create("&<\"\t\n\r>");
    [Params("short-hit", "long-miss", "long-hit", "multilingual")] public string Corpus { get; set; } = "short-hit";
    private string value = "";
    [GlobalSetup] public void Setup() => value = Corpus switch { "short-hit" => "a&b", "long-miss" => new string('x', 8192), "long-hit" => new string('x', 8191) + "&", _ => new string('界', 8191) + "\r" };
    [Benchmark(Baseline = true)] public int ScalarLoop()
    {
        for (int i = 0; i < value.Length; i++) if (value[i] is '&' or '<' or '"' or '\t' or '\n' or '\r' or '>') return i;
        return -1;
    }
    [Benchmark] public int CachedSearchValues() => value.AsSpan().IndexOfAny(Stops);
    [Benchmark] public int RuntimeIndexOfAny() => value.AsSpan().IndexOfAny("&<\"\t\n\r>");
}

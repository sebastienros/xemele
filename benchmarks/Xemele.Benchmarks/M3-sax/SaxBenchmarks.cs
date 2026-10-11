using BenchmarkDotNet.Attributes;
using System.Text;
using Xemele.Streaming;

namespace Xemele.Benchmarks.Sax;

[MemoryDiagnoser]
public class SaxBenchmarks
{
    [Params(32, 16384)] public int Characters { get; set; }
    private string xml = "";
    private byte[] bytes = [];
    private readonly CountingHandler handler = new();
    [GlobalSetup] public void Setup()
    {
        xml = "<r>" + new string('x', Characters) + "β😀</r>";
        bytes = Encoding.UTF8.GetBytes(xml);
    }
    [Benchmark(Baseline = true)] public int StringSax() { handler.Count = 0; XmlSaxParser.Parse(xml, handler); return handler.Count; }
    [Benchmark] public int BytesSax() { handler.Count = 0; XmlSaxParser.Parse(bytes, handler); return handler.Count; }
    [Benchmark] public int StreamSax() { handler.Count = 0; using var input = new MemoryStream(bytes, writable: false); XmlSaxParser.Parse(input, handler); return handler.Count; }
    [Benchmark] public XmlDocument StringDom() => XmlDocument.Parse(xml);
    private sealed class CountingHandler : XmlSaxHandler
    {
        internal int Count;
        public override void Text(ReadOnlySpan<char> text) => Count += text.Length;
    }
}

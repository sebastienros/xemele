using System.Text;
using BenchmarkDotNet.Attributes;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class DomStorageBenchmarks
{
    [Params("repeated", "distinct", "multilingual", "small")]
    public string Shape { get; set; } = "repeated";
    private byte[] input = [];

    [GlobalSetup]
    public void Setup()
    {
        var xml = new StringBuilder("<r xmlns:p='urn:p'>");
        int count = Shape == "small" ? 1 : 1000;
        for (int i = 0; i < count; i++)
        {
            string name = Shape == "distinct" ? "n" + i : "n";
            xml.Append("<p:").Append(name).Append(" a='v' p:b='w'>")
                .Append(Shape == "multilingual" ? "根😀 &amp; café" : "text")
                .Append("</p:").Append(name).Append('>');
        }
        xml.Append("</r>"); input = Encoding.UTF8.GetBytes(xml.ToString());
    }

    [Benchmark]
    public XmlNode ParseOwnedDom() => XmlDocument.Parse(input).RootElement;
}

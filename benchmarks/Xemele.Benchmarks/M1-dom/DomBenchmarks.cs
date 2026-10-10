using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Xemele;

namespace Xemele.DomMeasurements;

[MemoryDiagnoser, ShortRunJob]
public class DomBenchmarks
{
    [Params("Ascii", "Unicode", "LongText", "Deep")]
    public string Shape { get; set; } = "Ascii";
    private string source = "";
    private byte[] bytes = [];
    private XmlParseOptions options = new() { Limits = new() { Depth = null } };

    [GlobalSetup]
    public void Setup()
    {
        source = Shape switch
        {
            "Ascii" => "<r>" + string.Concat(Enumerable.Repeat("<c a='value' xmlns:p='u' p:a='v'>text&amp;</c>", 128)) + "</r>",
            "Unicode" => "<?xml version='1.0' encoding='UTF-8'?><r>" + string.Concat(Enumerable.Repeat("<c a='β😀'>α😀&amp;\r\n<![CDATA[β]]></c>", 128)) + "</r>",
            "LongText" => "<r>" + new string('x', 131072) + "</r>",
            _ => string.Concat(Enumerable.Repeat("<r>", 2048)) + "value" + string.Concat(Enumerable.Repeat("</r>", 2048))
        };
        bytes = Encoding.UTF8.GetBytes(source);
        string expected = ParseCharacters().RootElement.GetTextContent();
        if (ParseBytes().RootElement.GetTextContent() != expected || LoadStream().RootElement.GetTextContent() != expected || DecodeWholeSourceThenParse().RootElement.GetTextContent() != expected)
            throw new InvalidOperationException("DOM workload oracle mismatch.");
    }
    [Benchmark(Baseline = true)] public XmlDocument DecodeWholeSourceThenParse() => XmlDocument.Parse(Encoding.UTF8.GetString(bytes), options);
    [Benchmark] public XmlDocument ParseCharacters() => XmlDocument.Parse(source.AsSpan(), options);
    [Benchmark] public XmlDocument ParseBytes() => XmlDocument.Parse(bytes.AsSpan(), options);
    [Benchmark] public XmlDocument LoadStream()
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return XmlDocument.Load(stream, options);
    }
}

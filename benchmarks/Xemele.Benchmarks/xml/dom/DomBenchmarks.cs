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
    private readonly XmlParseOptions options = new() { Limits = new() { Depth = null } };

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
        var expected = ParseCharacters();
        Equivalent(expected, ParseBytes());
        Equivalent(expected, LoadStream());
        Equivalent(expected, DecodeWholeSourceThenParse());
    }
    private static void Equivalent(XmlDocument expected, XmlDocument actual)
    {
        XmlNode a = expected.DocumentNode, b = actual.DocumentNode;
        while (true)
        {
            if (a.Kind != b.Kind || a.LocalName != b.LocalName || a.NamespaceUri != b.NamespaceUri || a.Prefix != b.Prefix || a.Value != b.Value || a.Children.Count != b.Children.Count || a.Attributes.Count != b.Attributes.Count || a.NamespaceDeclarations.Count != b.NamespaceDeclarations.Count)
            {
                throw new InvalidOperationException("DOM workload node oracle mismatch.");
            }

            var attributes = b.Attributes.GetEnumerator();
            foreach (var item in a.Attributes)
            {
                if (!attributes.MoveNext())
                {
                    throw new InvalidOperationException("DOM workload attribute count mismatch.");
                }

                var other = attributes.Current;
                if (item.LocalName != other.LocalName || item.NamespaceUri != other.NamespaceUri || item.Prefix != other.Prefix || item.Value != other.Value)
                {
                    throw new InvalidOperationException("DOM workload attribute oracle mismatch.");
                }
            }
            var declarations = b.NamespaceDeclarations.GetEnumerator();
            foreach (var item in a.NamespaceDeclarations)
            {
                if (!declarations.MoveNext() || item.Prefix != declarations.Current.Prefix || item.Value != declarations.Current.Value)
                {
                    throw new InvalidOperationException("DOM workload namespace oracle mismatch.");
                }
            }
            bool moreA = Advance(ref a), moreB = Advance(ref b);
            if (moreA != moreB)
            {
                throw new InvalidOperationException("DOM workload topology mismatch.");
            }

            if (!moreA)
            {
                return;
            }
        }
    }
    private static bool Advance(ref XmlNode node)
    {
        if (!node.FirstChild.IsDefault)
        {
            node = node.FirstChild;
            return true;
        }
        while (!node.Parent.IsDefault && node.NextSibling.IsDefault)
        {
            node = node.Parent;
        }

        if (node.NextSibling.IsDefault)
        {
            return false;
        }

        node = node.NextSibling;
        return true;
    }
    [Benchmark(Baseline = true)] public XmlDocument DecodeWholeSourceThenParse() => XmlDocument.Parse(Encoding.UTF8.GetString(bytes), options);
    [Benchmark] public XmlDocument ParseCharacters() => XmlDocument.Parse(source.AsSpan(), options);
    [Benchmark] public XmlDocument ParseBytes() => XmlDocument.Parse(bytes.AsSpan(), options);
    [Benchmark]
    public XmlDocument LoadStream()
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return XmlDocument.Load(stream, options);
    }
}

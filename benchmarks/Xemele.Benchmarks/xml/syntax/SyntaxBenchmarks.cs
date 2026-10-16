using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Xemele;
using Xemele.Parsing.Syntax;

[MemoryDiagnoser, ShortRunJob]
public class DelimiterBenchmarks
{
    private static readonly SearchValues<char> Stops = SearchValues.Create("<&\r\n]");
    private string input = "";
    [Params("short-hit", "short-miss", "long-hit", "long-miss", "dense", "multilingual")]
    public string Corpus { get; set; } = "";
    [GlobalSetup]
    public void Setup()
    {
        input = Corpus switch
        {
            "short-hit" => "abc&def",
            "short-miss" => "abcdefg",
            "long-hit" => new string('a', 4095) + "<",
            "long-miss" => new string('a', 4096),
            "dense" => "&<&<&<\r\n]",
            _ => "α😀漢字α😀漢字α😀漢字<"
        };
        var expected = Loop();
        if (SmallSet() != expected || Cached() != expected)
        {
            throw new Exception("Delimiter oracle mismatch.");
        }
    }
    [Benchmark(Baseline = true)]
    public int Loop()
    {
        var span = input.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] is '<' or '&' or '\r' or '\n' or ']')
            {
                return i;
            }
        }

        return -1;
    }
    [Benchmark] public int SmallSet() => input.AsSpan().IndexOfAny("<&\r\n]".AsSpan());
    [Benchmark] public int Cached() => input.AsSpan().IndexOfAny(Stops);
}

[MemoryDiagnoser, ShortRunJob]
public class SyntaxBenchmarks
{
    private XmlParserCore core = null!;
    private readonly NullSink sink = new();
    private const string Repeated = "<c a='v' xmlns:p='u' p:a='w'/>α😀&amp;\r\n";
    [GlobalSetup]
    public void Setup()
    {
        core = new(new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null
            }
        });
        Check(core.Append("<r>\n", [], false, sink, default), ScanStep.NeedInput);
        Check(core.Append(Repeated, [], false, sink, default), ScanStep.NeedInput);
    }
    [GlobalCleanup] public void Cleanup() => core.Dispose();
    [Benchmark]
    public int WarmRepeatedTagAndUnicode()
    {
        var r = core.Append(Repeated, [], false, sink, default);
        Check(r, ScanStep.NeedInput);
        return r.Consumed;
    }
    [Benchmark]
    public long ColdDocument()
    {
        using var parser = new XmlParserCore(new());
        var r = parser.Append("<r><c a='v'/>α😀&amp;\r\n<![CDATA[x]]><!--ok--><?p x?></r>", [], true, sink, default);
        Check(r, ScanStep.Completed);
        return sink.Units;
    }
    [Benchmark]
    public int StructuredRejection()
    {
        using var parser = new XmlParserCore(new());
        var r = parser.Append("<r a='&unknown;'/>", [], true, sink, default);
        Check(r, ScanStep.Rejected);
        return r.Consumed;
    }
    private static void Check(ScanResult r, ScanStep expected)
    {
        if (r.Step != expected)
        {
            throw new Exception("Grammar benchmark failed.");
        }
    }
    internal sealed class NullSink : IXmlEventSink
    {
        internal long Units;
        public bool StartDocument(XmlLocation l) => true;
        public bool Declaration(XmlDeclarationView d) => true;
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) => true;
        public bool NamespaceDeclaration(XmlNamespaceView n) => true;
        public bool Attribute(XmlAttributeView a) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView n, XmlLocation l) => true;
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l) => true;
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l)
        {
            Units += t.Length;
            return true;
        }
        public bool EndDocument(XmlLocation l) => true;
    }
}

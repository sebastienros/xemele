using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;

[MemoryDiagnoser, ShortRunJob]
public class DeclarationVersionBenchmarks
{
    [Params("1.0", "1.10", "long", "invalid")] public string Corpus { get; set; } = "1.0";
    private string xml = "", version = "";
    private VersionOracleSink sink = null!;
    [GlobalSetup]
    public void Setup()
    {
        version = Corpus switch { "long" => "1." + new string('9', 4096), "invalid" => "2.0", _ => Corpus };
        xml = $"<?xml version='{version}'?><r/>"; sink = new(version);
        ParseDocument();
    }
    [Benchmark]
    public int ParseDocument()
    {
        sink.Reset();
        using var core = new XmlParserCore(new());
        var result = core.Append(xml, [], true, sink, default);
        if (Corpus == "invalid")
        { if (result.Step != ScanStep.Rejected || result.Diagnostic?.Code != "XML.Syntax" || sink.Callbacks != 1) throw new Exception("Invalid version oracle."); }
        else if (result.Step != ScanStep.Completed || result.Consumed != xml.Length || sink.Callbacks != 6) throw new Exception("Document oracle.");
        return result.Consumed + sink.Callbacks;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class TextDeclarationVersionBenchmarks
{
    [Params("1.0", "1.10", "long", "absent-version", "invalid")] public string Corpus { get; set; } = "1.0";
    [Params(false, true)] public bool Chunked { get; set; }
    private string input = "";
    private string? version;
    [GlobalSetup]
    public void Setup()
    {
        version = Corpus switch { "long" => "1." + new string('9', 4096), "absent-version" => null, "invalid" => "2.0", _ => Corpus };
        input = "<?xml " + (version is null ? "" : $"version='{version}' ") + "encoding='UTF-8'?>";
        FrameOwned();
    }
    [Benchmark]
    public int FrameOwned()
    {
        using var framer = new XmlTextDeclarationFramer(new());
        TextDeclarationResult result = default; int consumed = 0;
        while (consumed < input.Length)
        {
            result = framer.Append(input.AsSpan(consumed, Chunked ? 1 : input.Length - consumed), [], !Chunked || consumed == input.Length - 1, default);
            consumed += result.Consumed;
            if (result.Step != TextDeclarationStep.NeedInput) break;
        }
        if (Corpus == "invalid")
        { if (result.Step != TextDeclarationStep.Rejected || result.Diagnostic?.Code != "XML.Syntax") throw new Exception("Invalid framing oracle."); }
        else if (result.Step != TextDeclarationStep.Declared || consumed != input.Length || result.Declaration?.Version != version || result.Declaration?.EncodingName != "UTF-8") throw new Exception("Owned framing oracle.");
        return consumed;
    }
}

internal sealed class VersionOracleSink(string version) : IXmlEventSink
{
    internal int Callbacks;
    internal void Reset() => Callbacks = 0;
    public bool StartDocument(XmlLocation location) { Callbacks++; return true; }
    public bool Declaration(XmlDeclarationView value)
    {
        if (!value.Version.SequenceEqual(version) || !value.EncodingName.IsEmpty || value.Standalone is not null || value.Location != new XmlLocation(null, 1, 1, null)) throw new Exception("Exact borrowed version oracle.");
        Callbacks++; return true;
    }
    public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
    { if (!empty || !name.LocalName.SequenceEqual("r") || !name.NamespaceUri.IsEmpty || !name.Prefix.IsEmpty) throw new Exception("Root oracle."); Callbacks++; return true; }
    public bool NamespaceDeclaration(XmlNamespaceView value) => throw new Exception("Unexpected namespace.");
    public bool Attribute(XmlAttributeView value) => throw new Exception("Unexpected attribute.");
    public bool StartElementContent() { Callbacks++; return true; }
    public bool EndElement(XmlNameView name, XmlLocation location) { Callbacks++; return true; }
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => throw new Exception("Unexpected text.");
    public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => throw new Exception("Unexpected fragment.");
    public bool EndDocument(XmlLocation location) { Callbacks++; return true; }
}

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.Html;
namespace Xemele.Benchmarks.HtmlSerialization;

[MemoryDiagnoser, ShortRunJob]
public class EscapeBenchmarks
{
    [Params("short-hit", "short-miss", "long-late-hit", "long-miss", "dense", "multilingual")]
    public string Case { get; set; } = "";
    [Params(false, true)] public bool Legacy { get; set; }
    private string input = "";
    private readonly CountingWriter sink = new();
    private static readonly SearchValues<char> Html5Stops = SearchValues.Create("&<>\u00a0");
    private static readonly SearchValues<char> LegacyStops = SearchValues.Create("&<>\0");
    [GlobalSetup] public void Setup() => input = Input(Case);
    internal static string Input(string kind) => kind switch
    {
        "short-hit" => "x&", "short-miss" => "abc",
        "long-late-hit" => new string('x', 1023) + "&", "long-miss" => new string('x', 1024),
        "dense" => string.Concat(Enumerable.Repeat("<&>\"\u00a0", 120)),
        "multilingual" => string.Concat(Enumerable.Repeat("é尾😀", 120)) + "\u00a0&", _ => throw new ArgumentException()
    };
    [Benchmark(Baseline = true)] public int Scalar() => HtmlSerializer.Stop(input, false, Legacy);
    [Benchmark] public int Index() => IndexScan(input, Legacy);
    [Benchmark] public int SmallSet() => SmallSetScan(input, Legacy);
    [Benchmark] public int Cached() => CachedScan(input, Legacy);
    [Benchmark] public long BorrowedEscapedOutput() { sink.Reset(); HtmlSerializer.Escape(sink, input, false, Legacy, default); return sink.Count; }
    internal static int CachedScan(ReadOnlySpan<char> input, bool legacy) => input.IndexOfAny(legacy ? LegacyStops : Html5Stops);
    internal static int SmallSetScan(ReadOnlySpan<char> input, bool legacy) => input.IndexOfAny(legacy ? "&<>\0" : "&<>\u00a0");
    internal static int IndexScan(ReadOnlySpan<char> input, bool legacy)
    {
        int best = input.IndexOf('&');
        int next = input.IndexOf('<'); if (next >= 0 && (best < 0 || next < best)) best = next;
        next = input.IndexOf('>'); if (next >= 0 && (best < 0 || next < best)) best = next;
        next = input.IndexOf(legacy ? '\0' : '\u00a0'); if (next >= 0 && (best < 0 || next < best)) best = next;
        return best;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class OutputBenchmarks
{
    [Params("small", "large", "multilingual")] public string Case { get; set; } = "";
    [Params(HtmlSerializationMode.Html5, HtmlSerializationMode.Legacy)] public HtmlSerializationMode Mode { get; set; }
    private HtmlDocument document = null!;
    private HtmlSaveOptions options = null!;
    private readonly CountingWriter sink = new();
    private readonly CountingStream stream = new();
    [GlobalSetup] public void Setup() { document = Document(Case); options = new() { Mode = Mode }; }
    internal static HtmlDocument Document(string kind)
    {
        var d = new HtmlDocument(new() { TokenCharacters = null }); var html = d.CreateElement("html"); d.DocumentNode.AppendChild(d.CreateDocumentType(new("html", "-//owned//", "about:legacy-compat", false))); d.DocumentNode.AppendChild(html);
        var body = d.CreateElement("body"); html.AppendChild(body);
        int count = kind == "large" ? 1024 : kind == "multilingual" ? 64 : 1;
        for (int i = 0; i < count; i++)
        {
            var p = d.CreateElement("p"); p.SetAttribute(new("title", "<>&\"\u00a0")); p.SetAttribute(new("disabled", "disabled")); p.SetAttribute(new("href", " /é尾😀?a=1&b=2")); body.AppendChild(p);
            p.AppendChild(d.CreateText(kind == "multilingual" ? "é尾😀<&>\u00a0" : "text<&>\u00a0"));
            var template = d.CreateElement("template"); body.AppendChild(template); template.TemplateContent!.AppendChild(d.CreateProcessingInstruction("marker", ""));
            template.TemplateContent.AppendChild(d.CreateComment("bad-->tail-"));
        }
        var script = d.CreateElement("script"); script.AppendChild(d.CreateText("if(a<b)c&d")); body.AppendChild(script); return d;
    }
    [Benchmark] public long BorrowedTreeOutput() { sink.Reset(); HtmlSerializer.Write(document.DocumentNode, sink, Mode, default); return sink.Count; }
    [Benchmark] public string OwnedResult() => HtmlSerializer.Serialize(document, options);
    [Benchmark] public long Utf8Save() { stream.Reset(); HtmlSerializer.Save(document, stream, options); return stream.Count; }
}

[MemoryDiagnoser, ShortRunJob]
public class BooleanBenchmarks
{
    [Params("checked", "CHECKED", "noresize", "selectedx", "selecte", "unknown", "ſelected", "")]
    public string Name { get; set; } = "";
    private static readonly string[] Names = ["checked", "compact", "declare", "defer", "disabled", "ismap", "multiple", "nohref", "noresize", "noshade", "nowrap", "readonly", "selected"];
    [Benchmark] public bool Dispatch() => HtmlSerializer.Boolean(Name);
    [Benchmark(Baseline = true)] public bool Linear() => LinearMatch(Name);
    internal static bool LinearMatch(ReadOnlySpan<char> value)
    {
        foreach (string name in Names)
        {
            if (name.Length != value.Length) continue;
            bool equal = true;
            for (int i = 0; i < name.Length; i++) { char c = value[i]; if (c is >= 'A' and <= 'Z') c = (char)(c + 32); if (c != name[i]) { equal = false; break; } }
            if (equal) return true;
        }
        return false;
    }
}
internal sealed class CountingWriter : TextWriter
{
    public long Count; public override Encoding Encoding => Encoding.UTF8;
    public void Reset() => Count = 0;
    public override void Write(char value) => Count++;
    public override void Write(string? value) => Count += value?.Length ?? 0;
    public override void Write(ReadOnlySpan<char> buffer) => Count += buffer.Length;
}
internal sealed class CountingStream : Stream
{
    public long Count; public ulong Hash = 14695981039346656037UL;
    public void Reset() { Count = 0; Hash = 14695981039346656037UL; }
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void Write(ReadOnlySpan<byte> buffer) { Count += buffer.Length; foreach (byte b in buffer) Hash = unchecked((Hash ^ b) * 1099511628211UL); }
    public override void Flush() { }
    public override bool CanRead => false; public override bool CanWrite => true; public override bool CanSeek => false;
    public override long Length => Count; public override long Position { get => Count; set => throw new NotSupportedException(); }
    public override int Read(byte[] b, int o, int c) => throw new NotSupportedException(); public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long n) => throw new NotSupportedException();
}

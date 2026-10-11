using BenchmarkDotNet.Attributes;
namespace Xemele.M3WriterMeasurements;

// Prebuilt in-process toolchain lets coordinator hold an exact measurement
// lease with all restore/build/fixture preparation outside the timing window.
[MemoryDiagnoser, InProcess]
public class WriterBenchmarks
{
    [Params("short", "long-ascii", "multilingual")] public string Corpus { get; set; } = "short";
    private string text = "";
    private XmlDocument document = null!;
    private readonly XmlSaveOptions options = new() { OmitXmlDeclaration = true };
    private readonly CountStream stream = new();
    [GlobalSetup]
    public void Setup()
    {
        text = Corpus switch { "long-ascii" => new string('x', 65536), "multilingual" => string.Concat(Enumerable.Repeat("界😀&<\r]]>", 4096)), _ => "hello&<]]>" };
        document = new(); var root = document.CreateElement("r"); document.DocumentNode.AppendChild(root);
        for (int i = 0; i < 16; i++) { var child = document.CreateElement("x"); child.SetAttribute("a", "v"); child.AppendChild(document.CreateText(text)); root.AppendChild(child); }
        long a = Forward(); long b = Dom(); if (a != b) throw new InvalidOperationException("Byte counts differ.");
    }
    [Benchmark] public long Forward()
    {
        stream.Count = 0;
        using var writer = XmlWriter.Create(stream, options);
        writer.WriteStartElement("r");
        for (int i = 0; i < 16; i++)
        { writer.WriteStartElement("x"); writer.WriteAttribute("a", "v"); writer.WriteText(text); writer.WriteEndElement(); }
        writer.WriteEndElement(); writer.WriteEndDocument(); return stream.Count;
    }
    [Benchmark(Baseline = true)] public long Dom()
    { stream.Count = 0; XmlSerializer.Serialize(document, stream, options); return stream.Count; }
    private sealed class CountStream : Stream
    {
        internal long Count;
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => Count; public override long Position { get => Count; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => Count += count;
        public override void Write(ReadOnlySpan<byte> buffer) => Count += buffer.Length;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

[MemoryDiagnoser, InProcess]
public class WriterScanBenchmarks
{
    private static readonly System.Buffers.SearchValues<char> Stops = System.Buffers.SearchValues.Create("&<\r]>" );
    [Params("short-miss", "short-hit", "long-miss", "long-hit", "dense", "multilingual")]
    public string Corpus { get; set; } = "short-miss";
    private string value = "";
    [GlobalSetup] public void Setup()
    {
        value = Corpus switch
        {
            "short-miss" => "hello", "short-hit" => "hi&", "long-miss" => new string('x', 8192),
            "long-hit" => new string('x', 8191) + "&", "dense" => "&<\r]]>",
            _ => new string('界', 8191) + "\r"
        };
        int expected = Scalar();
        if (Cached() != expected || Runtime() != expected) throw new InvalidOperationException("Scan mismatch.");
    }
    [Benchmark(Baseline = true)] public int Scalar()
    { for (int i = 0; i < value.Length; i++) if (value[i] is '&' or '<' or '\r' or ']' or '>') return i; return -1; }
    [Benchmark] public int Cached() => value.AsSpan().IndexOfAny(Stops);
    [Benchmark] public int Runtime() => value.AsSpan().IndexOfAny("&<\r]>");
}

using BenchmarkDotNet.Attributes;
namespace Xemele.M3AsyncSerializationMeasurements;

// Sole job is supplied by CLI; no duplicate attribute job.
[MemoryDiagnoser]
public class AsyncSerializationBenchmarks
{
    [Params("short", "long-ascii", "multilingual")] public string Corpus { get; set; } = "short";
    private XmlDocument document = null!;
    private readonly XmlSaveOptions options = new() { OmitXmlDeclaration = true, Indent = true };
    private readonly Sink sink = new();
    [GlobalSetup]
    public void Setup()
    {
        var text = Corpus switch
        {
            "long-ascii" => new string('x', 65536),
            "multilingual" => string.Concat(Enumerable.Repeat("界😀&<\r]]>", 4096)),
            _ => "hello&<]]>"
        };
        document = new XmlDocument();
        var root = document.CreateElement("r");
        document.DocumentNode.AppendChild(root);
        for (var i = 0; i < 16; i++)
        {
            var child = document.CreateElement("x");
            child.SetAttribute("a", "v");
            root.AppendChild(child);
            child.AppendChild(document.CreateText(text));
        }
        root.AppendChild(document.CreateText("tail")); // DOM lookahead after structural children.
        var sync = Synchronous();
        var asyncBytes = ImmediateAsync().GetAwaiter().GetResult();
        if (sync != asyncBytes)
        {
            throw new InvalidOperationException("Output mismatch");
        }
    }
    [Benchmark(Baseline = true)]
    public long Synchronous()
    {
        sink.Count = 0;
        document.Save(sink, options);
        return sink.Count;
    }
    [Benchmark]
    public async Task<long> ImmediateAsync()
    {
        sink.Count = 0;
        await document.SaveAsync(sink, options);
        return sink.Count;
    }
    private sealed class Sink : Stream
    {
        internal long Count;
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => Count; public override long Position
        {
            get => Count; set => throw new NotSupportedException();
        }
        public override void Write(ReadOnlySpan<byte> buffer) => Count += buffer.Length;
        public override void Write(byte[] buffer, int offset, int count) => Count += count;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Count += buffer.Length;
            return default;
        }
        public override void Flush() => throw new InvalidOperationException("Implicit flush");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

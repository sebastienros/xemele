using BenchmarkDotNet.Attributes;
namespace Xemele.M3AsyncWriterMeasurements;

[MemoryDiagnoser, InProcess]
public class AsyncWriterBenchmarks
{
    [Params("short", "long-ascii", "multilingual")] public string Corpus { get; set; } = "short";
    private string text = "";
    private readonly XmlSaveOptions options = new() { OmitXmlDeclaration = true };
    private readonly CountSink sink = new();
    [GlobalSetup] public void Setup()
    {
        text = Corpus switch { "long-ascii" => new string('x', 65536), "multilingual" => string.Concat(Enumerable.Repeat("界😀&<\r]]>", 4096)), _ => "hello&<]]>" };
        long sync = Synchronous(); long asyncBytes = ImmediateAsync().GetAwaiter().GetResult();
        if (sync != asyncBytes) throw new InvalidOperationException("Output count mismatch.");
    }
    [Benchmark(Baseline = true)] public long Synchronous()
    {
        sink.Count = 0; using var w = XmlWriter.Create(sink, options); w.WriteStartElement("r");
        for (int i = 0; i < 16; i++) { w.WriteStartElement("x"); w.WriteAttribute("a", "v"); w.WriteText(text); w.WriteEndElement(); }
        w.WriteEndElement(); w.WriteEndDocument(); return sink.Count;
    }
    [Benchmark] public async Task<long> ImmediateAsync()
    {
        sink.Count = 0; await using var w = XmlWriter.Create(sink, options); await w.WriteStartElementAsync("r");
        for (int i = 0; i < 16; i++) { await w.WriteStartElementAsync("x"); await w.WriteAttributeAsync("a", "v"); await w.WriteTextAsync(text.AsMemory()); await w.WriteEndElementAsync(); }
        await w.WriteEndElementAsync(); await w.WriteEndDocumentAsync(); return sink.Count;
    }
    private sealed class CountSink : Stream
    {
        internal long Count;
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => Count; public override long Position { get => Count; set => throw new NotSupportedException(); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Count += buffer.Length; return default; }
        public override void Write(ReadOnlySpan<byte> buffer) => Count += buffer.Length;
        public override void Write(byte[] buffer, int offset, int count) => Count += count;
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

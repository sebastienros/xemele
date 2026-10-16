using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Resolution.Core;

[MemoryDiagnoser, ShortRunJob]
public class ResolverBenchmarks
{
    [Params(1, 4096)]
    public int ChunkSize
    {
        get; set;
    }
    private byte[] buffer = null!;
    private Stream stream = null!;
    private XmlResolutionSession session = null!;
    private XmlResourceHandle handle = null!;
    private static readonly Uri Source = new("urn:fixture:benchmark");
    private static readonly XmlResourceRequest Request = new(Source, null, XmlResourceKind.ExternalEntity);
    [GlobalSetup]
    public void Setup()
    {
        buffer = new byte[ChunkSize];
        stream = new RepeatingStream();
        session = new(new Resolver(), new XmlResourceLimits { ExternalBytes = null, ExternalResources = null }, default);
        handle = session.Open(Request, default).Resource!;
    }
    [Benchmark(Baseline = true)] public int DirectRead() => stream.Read(buffer.AsSpan());
    [Benchmark] public int BoundedRead() => handle.Read(buffer, default).Count;
    [Benchmark] public async ValueTask<int> BoundedAsyncRead() => (await handle.ReadAsync(buffer, default)).Count;
    [Benchmark]
    public long SessionOpenReadDispose()
    {
        using var operation = new XmlResolutionSession(new Resolver(), new XmlResourceLimits(), default);
        using var resource = operation.Open(Request, default).Resource!;
        resource.Read(buffer, default);
        return operation.BytesRead;
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        session.Dispose();
        stream.Dispose();
    }
    private sealed class Resolver : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => new(new RepeatingStream(), Source);
    }
    private sealed class RepeatingStream : Stream
    {
        public override bool CanRead => true; public override bool CanWrite => false; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }
        public override int Read(Span<byte> buffer)
        {
            buffer.Clear();
            return buffer.Length;
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(Read(buffer.Span));
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

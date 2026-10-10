using System.Text.Json;
using Xemele;
using Xemele.Resolution.Core;
using Xemele.Tests.Resolution;

if (args.Length != 1 || args[0] is not ("original" or "equivalent" or "helper"))
    throw new ArgumentException("original|equivalent|helper");
var source = new Uri("https://fixture.invalid/final/root.dtd");
var location = new XmlLocation(source, 3, 7, 20);
var cancellation = CancellationToken.None; // Standalone diagnostic has no xUnit context; actual Fact retains its original context token.
long before = GC.GetAllocatedBytesForCurrentThread();
var resolver = new DelegateResolver(_ => new(new ZeroStream(), source));
using var session = new XmlResolutionSession(resolver, new XmlResourceLimits { ExternalBytes = null, ExternalResources = 8 }, cancellation);
using var handle = session.Open(new(new("https://fixture.invalid/original"), source, XmlResourceKind.ExternalSubset), location).Resource!;
byte[] buffer = new byte[4096];
long setup = GC.GetAllocatedBytesForCurrentThread() - before;
before = GC.GetAllocatedBytesForCurrentThread();
if (args[0] == "original") for (int i = 0; i < 1000; i++) handle.Read(buffer, location);
else ResolverAllocationProbe.ReadChecked(session, handle, buffer, location, cancellation, 1000);
long warm = GC.GetAllocatedBytesForCurrentThread() - before;
long warmReadBytes = session.BytesRead, primitive = 0;
before = GC.GetAllocatedBytesForCurrentThread();
if (args[0] == "helper") primitive = ResolverAllocationProbe.ReadChecked(session, handle, buffer, location, cancellation, 10000);
else for (int i = 0; i < 10000; i++)
{
    var result = handle.Read(buffer, location);
    if (args[0] == "equivalent" && (result.Count != 4096 || result.Diagnostic is not null)) throw new InvalidOperationException("Resolver read progress.");
}
long steady = GC.GetAllocatedBytesForCurrentThread() - before;
long measuredBytes = session.BytesRead - warmReadBytes;
Console.WriteLine(JsonSerializer.Serialize(new { mode = args[0], runtime = Environment.Version.ToString(), warmIterations = 1000,
    measuredIterations = 10000, bufferSize = buffer.Length, setupBytes = setup, warmBytes = warm, steadyBytes = steady,
    primitiveBytes = args[0] == "helper" ? (long?)primitive : null, warmReadBytes, measuredBytes, totalBytes = session.BytesRead,
    resourcesOpened = session.ResourcesOpened, resolverCalls = resolver.Calls,
    scope = "Standalone bounded comparison; same original source/location/default token/1000 warm/10000 measured/4096 buffer; caller vs linked helper counters, no subtraction or timing; historical MTP cause unresolved" }));
if (warmReadBytes != 4096000 || measuredBytes != 40960000 || session.BytesRead != 45056000 || session.ResourcesOpened != 1 || resolver.Calls != 1) return 1;
return args[0] == "helper" && primitive != 0 ? 1 : 0;
sealed class DelegateResolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
{
    internal int Calls;
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) { Calls++; return resolve(request); }
}
sealed class ZeroStream : Stream
{
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(Span<byte> buffer) { buffer.Clear(); return buffer.Length; }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset,count));
    public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

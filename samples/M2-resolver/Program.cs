using System.Text;
using System.Text.Json;
using Xemele;
using Xemele.Resolution.Core;

var source = new Uri("https://fixture.invalid/final/entity.xml");
var request = new XmlResourceRequest(new Uri("https://fixture.invalid/original/entity.xml"), null, XmlResourceKind.ExternalEntity)
    { SystemId = "entity.xml", PublicId = "offline-example" };
var resolver = new OfflineResolver(source);
long coldBefore = GC.GetAllocatedBytesForCurrentThread();
using (var firstSession = new XmlResolutionSession(resolver, new XmlResourceLimits(), default))
{
    using var firstResource = firstSession.Open(request, default).Resource!;
}
long firstSessionOpenDisposeBytes = GC.GetAllocatedBytesForCurrentThread()-coldBefore;
await using (var resource = await resolver.ResolveAsync(request, default))
{
    if (resource is null || resource.SourceUri != source) throw new Exception("Offline grant failed.");
    using var reader = new StreamReader(resource.Content, Encoding.UTF8, leaveOpen: true);
    var xml = await reader.ReadToEndAsync();
    _ = XmlDocument.Parse(xml);
    Console.WriteLine($"Offline resolver final URI: {resource.SourceUri}; M1 strict parse preserved.");
}
using (var session = new XmlResolutionSession(resolver, new XmlResourceLimits { ExternalBytes = 4, ExternalResources = 1 }, default))
{
    using var handle = session.Open(request, default).Resource!;
    byte[] buffer = new byte[100];
    if (handle.Read(buffer, default).Count != 4 || handle.Read(buffer, default).Count != 0) throw new Exception("Exact byte bound failed.");
    if (session.Open(request, default).Diagnostic?.Code != "XML.ExternalResourcesLimit") throw new Exception("Open bound failed.");
}
// Allocation observations are not latency measurements; initialization and
// retained state are accounted separately from the borrowed read primitive.
var allocations = new List<long>();
for (int i=0; i<1001; i++)
{
    long before = GC.GetAllocatedBytesForCurrentThread();
    using var operation = new XmlResolutionSession(resolver, new XmlResourceLimits(), default);
    using var handle = operation.Open(request, default).Resource!;
    allocations.Add(GC.GetAllocatedBytesForCurrentThread()-before);
}
long setupMean = (long)allocations.Skip(1).Average();
var retained = new List<XmlResolutionSession>(1000);
long beforeRetained = GC.GetTotalMemory(true);
for (int i=0; i<1000; i++)
{
    var operation = new XmlResolutionSession(resolver, new XmlResourceLimits(), default);
    operation.Open(request, default); retained.Add(operation);
}
long live = GC.GetTotalMemory(true);
foreach (var operation in retained) operation.Dispose();
retained.Clear();
long cleaned = GC.GetTotalMemory(true);
Console.WriteLine(JsonSerializer.Serialize(new { firstSessionOpenDisposeBytes, warmSetupBytes=setupMean,
    retained1000SessionsBytes=live-beforeRetained, residualAfterDisposeBytes=cleaned-beforeRetained,
    note="GC heap deltas are noisy; retained sessions own keys/handles/resource buffers, no session read buffer. Async allocation requires BDN process diagnostics." }));
GC.KeepAlive(retained);

sealed class OfflineResolver(Uri finalUri) : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.SystemId == "entity.xml" ? new(new MemoryStream("<r/>"u8.ToArray()), finalUri) : null;
    }
}

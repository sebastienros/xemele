using System.Runtime.InteropServices;
using System.Text;
using Xemele;
if (RuntimeInformation.FrameworkDescription != ".NET 10.0.11") throw new Exception("Actual runtime must be .NET 10.0.11");
var source = XmlDocument.Parse("<r xmlns:xi='http://www.w3.org/2001/XInclude'><xi:include href='child'/></r>");
var options = new XIncludeOptions { BaseUri = new("https://sample.test/source"), Resolver = new OfflineResolver() };
if (args.Length == 0)
{
    var result = XIncludeProcessor.Process(source, options);
    var asyncResult = await XIncludeProcessor.ProcessAsync(source, options);
    if (!result.Success || !asyncResult.Success || result.IncludedResources != 1 || result.Document!.RootElement.FirstChild.LocalName != "child" || source.RootElement.FirstChild.LocalName != "include")
        throw new Exception("Sample mismatch");
    Console.WriteLine($"PASS: independent XInclude XML, async, owned output; {RuntimeInformation.FrameworkDescription}");
    return;
}
if (args is not ["--measure"]) throw new ArgumentException();
// The process-wide allocation counter includes async continuations. Exclusive
// lease excludes competing managed workloads; setup fixtures precede counters.
long before = GC.GetTotalAllocatedBytes(true);
var cold = XIncludeProcessor.Process(source, options);
long coldBytes = GC.GetTotalAllocatedBytes(true) - before;
if (!cold.Success) throw new Exception();
for (int i = 0; i < 32; i++) { _ = XIncludeProcessor.Process(source, options); _ = await XIncludeProcessor.ProcessAsync(source, options); }
const int repetitions = 128;
before = GC.GetTotalAllocatedBytes(true);
for (int i = 0; i < repetitions; i++) if (!XIncludeProcessor.Process(source, options).Success) throw new Exception();
long sync = GC.GetTotalAllocatedBytes(true) - before;
before = GC.GetTotalAllocatedBytes(true);
for (int i = 0; i < repetitions; i++) if (!(await XIncludeProcessor.ProcessAsync(source, options)).Success) throw new Exception();
long asyncBytes = GC.GetTotalAllocatedBytes(true) - before;
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long retainedBefore = GC.GetTotalMemory(true);
var retained = XIncludeProcessor.Process(source, options);
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long retainedBytes = GC.GetTotalMemory(true) - retainedBefore;
GC.KeepAlive(retained);
before = GC.GetTotalAllocatedBytes(true);
var positive = new byte[1024];
GC.KeepAlive(positive);
long control = GC.GetTotalAllocatedBytes(true) - before;
Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; cold-owned={coldBytes}; warm-sync-owned={sync / repetitions}; warm-async-owned={asyncBytes / repetitions}; retained-one-result-delta={retainedBytes}; positive1024={control}; repetitions={repetitions}");
sealed class OfflineResolver : XmlResourceResolver
{
    private static readonly byte[] Content = "<child>owned text</child>"u8.ToArray();
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new MemoryStream(Content, false), request.Uri);
    }
    public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return new(new MemoryStream(Content, false), request.Uri);
    }
}

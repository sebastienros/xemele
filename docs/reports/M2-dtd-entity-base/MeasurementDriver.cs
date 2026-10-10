using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Cryptography;
using BenchmarkDotNet.Running;
using Xemele;
using Xemele.Benchmarks;

if (args.Contains("--identity"))
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        driver = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
        product = typeof(XmlDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
        runtime = RuntimeInformation.FrameworkDescription,
        coreRuntimeBuild = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        coreLibrary = typeof(object).Assembly.Location,
        coreLibrarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(object).Assembly.Location))).ToLowerInvariant()
    })); return;
}
if (args.Contains("--correctness"))
{
    var fixture = new EntityBaseBenchmarks(); fixture.Check();
    Console.WriteLine("Complete authored model and exact redirected resolver/output oracles passed; counters disabled."); return;
}
if (args.Contains("--allocation"))
{
    long start = GC.GetAllocatedBytesForCurrentThread();
    var fixture = new EntityBaseBenchmarks(); fixture.Check();
    long coldSetupAndOracles = GC.GetAllocatedBytesForCurrentThread() - start;
    for (int i = 0; i < 128; i++) { GC.KeepAlive(fixture.InternalNestedModel()); GC.KeepAlive(fixture.RedirectedComposition()); }
    start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1024; i++) GC.KeepAlive(fixture.InternalNestedModel());
    long ownedModels = GC.GetAllocatedBytesForCurrentThread() - start;
    start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1024; i++) GC.KeepAlive(fixture.RedirectedComposition());
    long ownedDocuments = GC.GetAllocatedBytesForCurrentThread() - start;
    start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1024; i++) GC.KeepAlive(new byte[128]);
    long positiveControl = GC.GetAllocatedBytesForCurrentThread() - start;
    if (positiveControl == 0) throw new Exception("allocation positive control");
    Console.WriteLine(JsonSerializer.Serialize(new { iterations = 1024, coldSetupAndOracles, ownedModels, ownedDocuments, positiveControl })); return;
}
BenchmarkSwitcher.FromAssembly(typeof(EntityBaseBenchmarks).Assembly).Run(args);

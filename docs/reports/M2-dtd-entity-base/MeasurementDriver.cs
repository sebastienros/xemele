using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Cryptography;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.CsProj;
using Xemele;
using Xemele.Benchmarks;

if (args is ["--generate-build-only", var artifacts])
{
    // Same immutable declared cases/configuration as the later BDN invocation.
    // Generator and Builder are invoked directly; no Executor, engine, setup,
    // workload, warmup, allocation counter or measurement is invoked here.
    var config = ManualConfig.Create(DefaultConfig.Instance).WithArtifactsPath(artifacts)
        .WithOptions(ConfigOptions.KeepBenchmarkFiles | ConfigOptions.StopOnFirstError).WithBuildTimeout(TimeSpan.FromSeconds(240));
    using var info = BenchmarkConverter.TypeToBenchmarks(typeof(EntityBaseBenchmarks), config);
    var resolver = new CompositeResolver(EnvironmentResolver.Instance, InfrastructureResolver.Instance);
    var partitions = BenchmarkPartitioner.CreateForBuild([info], resolver);
    if (partitions.Length != 1 || partitions[0].Benchmarks.Length != 2) throw new Exception("Exact generated partition/case count required.");
    var toolchain = CsProjCoreToolchain.NetCoreApp10_0;
    var generated = toolchain.Generator.GenerateProject(partitions[0], ConsoleLogger.Default, artifacts);
    if (!generated.IsGenerateSuccess) throw new Exception("Build-only code generation failed.", generated.GenerateException);
    var built = toolchain.Builder.Build(generated, partitions[0], ConsoleLogger.Default);
    if (!built.IsBuildSuccess) throw new Exception("Independent generated build failed.");
    var paths = generated.ArtifactsPaths;
    var proof = EntityBaseProvenance.Capture(paths.BinariesDirectoryPath, paths.ExecutablePath, paths.BuildArtifactsDirectoryPath,
        Path.Combine(paths.BinariesDirectoryPath, "Xemele.Benchmarks.dll"), Path.Combine(paths.BinariesDirectoryPath, "Xemele.dll"));
    Console.WriteLine("ENTITY_BASE_BUILD_ONLY " + JsonSerializer.Serialize(new
    {
        countersDisabled = true, executorInvoked = false, cases = new[] { "InternalNestedModel", "RedirectedComposition" },
        buildRoot = paths.BuildArtifactsDirectoryPath, generatedProject = paths.ProjectFilePath,
        generatedCode = paths.ProgramCodePath, generatedScript = paths.BuildScriptFilePath, proof
    })); return;
}

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

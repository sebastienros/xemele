using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.ConsoleArguments;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using BenchmarkDotNet.Toolchains.Parameters;
using BenchmarkDotNet.Diagnosers;

if (args.Length == 1 && args[0] == "--runtime-identity")
{ Console.WriteLine(JsonSerializer.Serialize(RuntimePayload.Capture())); return; }

if (args.Length >= 2 && args[0] is "--lifecycle" or "--lifecycle-oracle")
{
    bool counters = args[0] == "--lifecycle";
    bool positive = args.Length == 3 && args[2] == "positive";
    string library = Path.GetFullPath(args[1]);
    string directory = Path.GetDirectoryName(library)!;
    System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
    {
        string candidate = Path.Combine(directory, name.Name + ".dll");
        return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
    };
    var product = Assembly.LoadFrom(Path.Combine(directory, "Xemele.dll"));
    var actual = Assembly.LoadFrom(library);
    static object Metadata(Assembly assembly) => new { Name = assembly.GetName().Name, Version = assembly.GetName().Version?.ToString(), FullVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, Sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
    var records = new List<object>();
    foreach (string typeName in new[] { "DeclarationVersionBenchmarks", "TextDeclarationVersionBenchmarks" })
    foreach (string corpus in typeName == "DeclarationVersionBenchmarks" ? new[] { "1.0", "1.10", "long", "invalid" } : new[] { "1.0", "1.10", "long", "absent-version", "invalid" })
    foreach (bool? chunked in typeName == "DeclarationVersionBenchmarks" ? new bool?[] { null } : new bool?[] { false, true })
    {
        var type = actual.GetType(typeName, true)!;
        var instance = Activator.CreateInstance(type)!;
        type.GetProperty("Corpus")!.SetValue(instance, corpus);
        if (chunked is bool split) type.GetProperty("Chunked")!.SetValue(instance, split);
        type.GetMethod("Setup")!.Invoke(instance, null);
        string method = typeName == "DeclarationVersionBenchmarks" ? "ParseDocument" : "FrameOwned";
        var invoke = (Func<int>)type.GetMethod(method)!.CreateDelegate(typeof(Func<int>), instance);
        int versionLength = corpus == "long" ? 4098 : corpus.Length;
        int expectedValue = typeName == "DeclarationVersionBenchmarks" ? corpus == "invalid" ? 22 : versionLength + 28 : corpus == "invalid" ? 16 : corpus == "absent-version" ? 24 : versionLength + 35;
        for (int i = 0; i < (counters ? 128 : 1); i++) if (invoke() != expectedValue) throw new InvalidOperationException("Lifecycle warm independent consumed/callback oracle failed.");
        object? escaped = null;
        long before = counters ? GC.GetAllocatedBytesForCurrentThread() : 0;
        long consumed = 0;
        int iterations = counters ? 512 : 1;
        for (int i = 0; i < iterations; i++)
        {
            consumed += invoke();
            if (positive) escaped = new byte[37];
        }
        long bytes = (counters ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
        if (consumed != (long)iterations * expectedValue || counters && (bytes <= 0 || bytes % 512 != 0) || positive && ((byte[])escaped!).Length != 37) throw new InvalidOperationException("Lifecycle complete output/allocation oracle failed.");
        records.Add(new { identity = new { type = typeName, method, corpus, chunked }, iterations, consumed, expectedValue, allocatedBytes = bytes, bytesPerOperation = bytes / 512 });
        GC.KeepAlive(escaped);
    }
    Console.WriteLine(JsonSerializer.Serialize(new { mode = counters ? positive ? "lifecycle-positive" : "lifecycle-real" : "lifecycle-oracle", cases = records, product = Metadata(product), benchmark = Metadata(actual), probe = Metadata(Assembly.GetExecutingAssembly()), runtime = Environment.Version.ToString(), architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), processId = Environment.ProcessId }));
    return;
}

bool measure = args.Length == 5 && args[3] == "--measure";
if (args.Length != 3 && !measure) throw new ArgumentException("Provide assembly, artifact directory, summary; measurement additionally requires --measure and frozen preparation.");
using var expected = measure ? JsonDocument.Parse(File.ReadAllText(args[4])) : null;
static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static SortedDictionary<string, string> Closure(string directory) => new(Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(p => Path.GetRelativePath(directory, p), Hash), StringComparer.Ordinal);
static SortedDictionary<string, string> Inputs(string directory) => new(Directory.EnumerateFiles(directory).Where(p => Path.GetExtension(p) is ".csproj" or ".notcs" or ".cs" or ".sh" or ".bat" or ".config").ToDictionary(p => Path.GetFileName(p), Hash), StringComparer.Ordinal);
static void Equal(JsonElement expected, SortedDictionary<string, string> actual, int? count = null)
{
    var known = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
    bool equal = (count is null || actual.Count == count) && actual.Count == known.Count && actual.All(p => known.TryGetValue(p.Key, out var value) && value == p.Value);
    Console.WriteLine("// VersionIdentityCheck: " + JsonSerializer.Serialize(new { kind = count is null ? "generated-inputs" : "child-closure", expected = known, actual, equal }));
    if (!equal) throw new InvalidOperationException("Frozen raw closure/input equality failed.");
}
static object Identity(BenchmarkCase value) => new { type = value.Descriptor.Type.Name, method = value.Descriptor.WorkloadMethod.Name, corpus = value.Parameters.Items.Single(p => p.Name == "Corpus").Value.ToString(), chunked = value.Parameters.Items.Any(p => p.Name == "Chunked") ? (bool?)value.Parameters.Items.Single(p => p.Name == "Chunked").Value : null };
var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
string[] options = ["--filter", "*DeclarationVersionBenchmarks*", "--artifacts", Path.GetFullPath(args[1]), "--keepFiles", "--buildTimeout", "240", "--stopOnFirstError"];
var parsed = ConfigParser.Parse(options, ConsoleLogger.Default);
if (!parsed.isSuccess) throw new InvalidOperationException("Exact measurement configuration did not parse.");
var infos = new[] { BenchmarkConverter.TypeToBenchmarks(assembly.GetType("DeclarationVersionBenchmarks", true)!, parsed.config), BenchmarkConverter.TypeToBenchmarks(assembly.GetType("TextDeclarationVersionBenchmarks", true)!, parsed.config) };
var resolver = new CompositeResolver(EnvironmentResolver.Instance, InfrastructureResolver.Instance);
var partitions = BenchmarkPartitioner.CreateForBuild(infos, resolver);
if (partitions.Sum(p => p.Benchmarks.Length) != 14) throw new InvalidOperationException("Expected 14 authored generation cases.");
var outputs = new List<object>();
int executed = 0;
foreach (var partition in partitions)
{
    // Use the installed runner's exact internal selector; this diagnostic never executes it.
    var selector = typeof(IToolchain).Assembly.GetType("BenchmarkDotNet.Toolchains.ToolchainExtensions", true)!
        .GetMethod("GetToolchain", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(BenchmarkCase)], null)!;
    var toolchain = (IToolchain)selector.Invoke(null, [partition.RepresentativeBenchmarkCase])!;
    var generated = toolchain.Generator.GenerateProject(partition, ConsoleLogger.Default, Path.GetFullPath(args[1]));
    if (!generated.IsGenerateSuccess) throw new InvalidOperationException("Generation failed.", generated.GenerateException);
    var built = toolchain.Builder.Build(generated, partition, ConsoleLogger.Default);
    outputs.Add(new { configurationOptions = options, generated = generated.IsGenerateSuccess, built = built.IsBuildSuccess, cases = partition.Benchmarks.Length,
        project = generated.ArtifactsPaths.ProjectFilePath, binaries = generated.ArtifactsPaths.BinariesDirectoryPath,
        executable = generated.ArtifactsPaths.ExecutablePath, benchmarkAssembly = partition.AssemblyLocation,
        identities = partition.Benchmarks.Select(info => Identity(info.BenchmarkCase)).ToArray() });
    File.WriteAllText(args[2], JsonSerializer.Serialize(outputs));
    if (!built.IsBuildSuccess) throw new InvalidOperationException("Build-only generation failed: " + built.ErrorMessage);
    if (measure)
    {
        string child = generated.ArtifactsPaths.BinariesDirectoryPath;
        string project = Path.GetDirectoryName(generated.ArtifactsPaths.ProjectFilePath)!;
        Equal(expected!.RootElement.GetProperty("childClosure"), Closure(child), 47);
        Equal(expected.RootElement.GetProperty("generatedInputIdentities"), Inputs(project));
        string revision = expected.RootElement.GetProperty("revision").GetString()!;
        foreach (string name in expected.RootElement.GetProperty("childMetadata").EnumerateObject().Select(p => p.Name))
        {
            JsonElement knownMetadata = expected.RootElement.GetProperty("childMetadata").GetProperty(name);
            Assembly actual;
            try { actual = Assembly.LoadFile(Path.Combine(child, name)); }
            catch (Exception error)
            {
                Console.WriteLine("// VersionMetadataCheck: " + JsonSerializer.Serialize(new { name, expected = knownMetadata, actual = (object?)null, equal = false, failure = new { type = error.GetType().Name, message = error.Message } }));
                throw;
            }
            string? fullVersion = actual.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var actualMetadata = new { Name = actual.GetName().Name, Version = actual.GetName().Version?.ToString(), FullVersion = fullVersion, Sha256 = Hash(Path.Combine(child, name)) };
            bool equal = fullVersion == "1.0.0+" + revision && actualMetadata.Name == knownMetadata.GetProperty("Name").GetString() && actualMetadata.Version == knownMetadata.GetProperty("Version").GetString() && actualMetadata.Sha256 == knownMetadata.GetProperty("Sha256").GetString();
            Console.WriteLine("// VersionMetadataCheck: " + JsonSerializer.Serialize(new { name, expected = knownMetadata, actual = actualMetadata, equal }));
            if (!equal) throw new InvalidOperationException("Actual child metadata differs.");
        }
        Console.WriteLine("// VersionFrozenChild: " + JsonSerializer.Serialize(new { child, project, files = Closure(child).Count, revision }));
        foreach (var info in partition.Benchmarks)
        {
            Equal(expected.RootElement.GetProperty("childClosure"), Closure(child), 47);
            Equal(expected.RootElement.GetProperty("generatedInputIdentities"), Inputs(project));
            Console.WriteLine("// VersionCaseStart: " + JsonSerializer.Serialize(Identity(info.BenchmarkCase)));
            Console.WriteLine("// Benchmark: " + info.BenchmarkCase.DisplayInfo);
            var result = toolchain.Executor.Execute(new ExecuteParameters(built, info.BenchmarkCase, info.Id, ConsoleLogger.Default, resolver, 1, MemoryDiagnoser.Default));
            Console.WriteLine("// VersionCaseResult: " + JsonSerializer.Serialize(new { identity = Identity(info.BenchmarkCase), exit = result.ExitCode, process = result.ProcessId, executableFound = result.FoundExecutable, success = result.IsSuccess, errors = result.Errors }));
            if (!result.IsSuccess || result.ExitCode != 0 || !result.FoundExecutable || result.Errors.Count != 0) throw new InvalidOperationException("Actual benchmark child failed.");
            Equal(expected.RootElement.GetProperty("childClosure"), Closure(child), 47);
            Equal(expected.RootElement.GetProperty("generatedInputIdentities"), Inputs(project));
            executed++;
        }
    }
}
if (measure)
{
    if (executed != 14) throw new InvalidOperationException("Expected exactly 14 executed declared cases.");
    Console.WriteLine("// VersionExecutedCases: " + executed);
}
else Console.WriteLine("Generated/built declared 14 cases; executor and benchmark methods were never invoked.");

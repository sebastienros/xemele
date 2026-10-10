using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

// Converter discovery only: never invoke BenchmarkRunner or execute benchmark methods.
if (args.Length < 3) throw new ArgumentException("assembly, baseline|current, output [baseline output]");
string assemblyPath = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string candidate = Path.Combine(Path.GetDirectoryName(assemblyPath)!, name.Name + ".dll");
    return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
bool baseline = args[1] == "baseline";
string[] names = Path.GetFileName(assemblyPath) switch
{
    "Tree.dll" => ["Construction", "Traversal", "Mutation", "Names", "Text"],
    "Input.dll" => ["InputBenchmarks", "ScanBenchmarks", "MatchBenchmarks", "ChunkMatchBenchmarks", "LiteralBenchmarks", "RejectionBenchmarks"],
    "Xemele.CommonBenchmarks.dll" => ["CommonBenchmarks"],
    "Xemele.InputBenchmarks.dll" => ["InputBenchmarks", "InputOwnershipBenchmarks"],
    "SerializerBenchmarks.dll" => ["SerializerBenchmarks", "EscapeScanBenchmarks"],
    "Xemele.Benchmarks.dll" => ["CommonBenchmarks", "InputBenchmarks", "InputOwnershipBenchmarks", "SerializerBenchmarks", "EscapeScanBenchmarks"],
    _ => throw new ArgumentException("Unknown harness")
};
var types = assembly.GetTypes().Where(t => names.Contains(t.Name)).OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
if (types.Length != names.Length) throw new Exception("Missing/duplicate benchmark classes");
var rows = new List<ClassResult>();
foreach (var type in types)
{
    IConfig config = DefaultConfig.Instance;
    if (Path.GetFileName(assemblyPath) == "Tree.dll") config = config.AddExporter(JsonExporter.Full);
    if (baseline) config = config.AddJob(Job.ShortRun);
    else if (!type.IsDefined(typeof(ShortRunJobAttribute), false) || !type.IsDefined(typeof(MemoryDiagnoserAttribute), false))
        throw new Exception($"Missing class attributes: {type}");
    var discovered = BenchmarkConverter.TypeToBenchmarks(type, config);
    var cases = discovered.BenchmarksCases;
    var jobs = discovered.Config.GetJobs().ToArray();
    if (jobs.Length != 1 || jobs[0].Run.LaunchCount != 1 || jobs[0].Run.WarmupCount != 3 || jobs[0].Run.IterationCount != 3)
        throw new Exception($"Unexpected job configuration: {type}");
    var diagnosers = discovered.Config.GetDiagnosers().ToArray();
    if (diagnosers.Count(d => d is MemoryDiagnoser) != 1) throw new Exception($"Missing/duplicate memory diagnoser: {type}");
    if (cases.Length == 0 || cases.Any(c => c.Job.Run.LaunchCount != 1 || c.Job.Run.WarmupCount != 3 || c.Job.Run.IterationCount != 3 || c.Config.GetDiagnosers().Count(d => d is MemoryDiagnoser) != 1))
        throw new Exception($"Invalid individual cases: {type}");
    string[] inventory = cases.Select(c => c.Descriptor.WorkloadMethod.Name + "|" + c.Parameters.DisplayInfo).Order(StringComparer.Ordinal).ToArray();
    rows.Add(new(type.FullName!, jobs.Length, jobs[0].Run.LaunchCount, jobs[0].Run.WarmupCount, jobs[0].Run.IterationCount,
        diagnosers.Select(d => d.GetType().FullName!).Order(StringComparer.Ordinal).ToArray(), inventory));
    Console.WriteLine($"{type.FullName}: {inventory.Length} cases, {jobs.Length} job, launch/warmup/iteration=1/3/3, memory diagnoser");
}
string json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
if (!baseline && args.Length == 4 && json != File.ReadAllText(args[3])) throw new Exception("Compiled case/configuration inventory changed");
File.WriteAllText(args[2], json);
Console.WriteLine($"PASS: {rows.Count} classes, {rows.Sum(r => r.Inventory.Length)} compiled cases; no timing executed.");
record ClassResult(string Type, int Jobs, int LaunchCount, int WarmupCount, int IterationCount, string[] Diagnosers, string[] Inventory);

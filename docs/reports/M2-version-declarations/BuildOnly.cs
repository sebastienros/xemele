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

if (args.Length != 3) throw new ArgumentException("Provide actual benchmark assembly, temporary artifact directory and summary path.");
var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
string[] options = ["--filter", "*DeclarationVersionBenchmarks*", "--artifacts", Path.GetFullPath(args[1]), "--keepFiles", "--buildTimeout", "240", "--stopOnFirstError"];
var parsed = ConfigParser.Parse(options, ConsoleLogger.Default);
if (!parsed.isSuccess) throw new InvalidOperationException("Exact measurement configuration did not parse.");
var infos = new[] { BenchmarkConverter.TypeToBenchmarks(assembly.GetType("DeclarationVersionBenchmarks", true)!, parsed.config), BenchmarkConverter.TypeToBenchmarks(assembly.GetType("TextDeclarationVersionBenchmarks", true)!, parsed.config) };
var resolver = new CompositeResolver(EnvironmentResolver.Instance, InfrastructureResolver.Instance);
var partitions = BenchmarkPartitioner.CreateForBuild(infos, resolver);
if (partitions.Sum(p => p.Benchmarks.Length) != 14) throw new InvalidOperationException("Expected 14 authored generation cases.");
var outputs = new List<object>();
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
        executable = generated.ArtifactsPaths.ExecutablePath, benchmarkAssembly = partition.AssemblyLocation });
    File.WriteAllText(args[2], JsonSerializer.Serialize(outputs));
    if (!built.IsBuildSuccess) throw new InvalidOperationException("Build-only generation failed: " + built.ErrorMessage);
}
Console.WriteLine("Generated/built declared 14 cases; executor and benchmark methods were never invoked.");

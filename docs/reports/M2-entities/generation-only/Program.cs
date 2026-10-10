using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using System.Text.Json;
using Xemele.Benchmarks;

if (args.Length != 2) throw new ArgumentException("Provide temporary artifact directory and summary path.");
var infos = new[] { BenchmarkConverter.TypeToBenchmarks(typeof(EntityCompositionBenchmarks)), BenchmarkConverter.TypeToBenchmarks(typeof(EntityLookupBenchmarks)) };
var resolver = new CompositeResolver(EnvironmentResolver.Instance, InfrastructureResolver.Instance);
var partitions = BenchmarkPartitioner.CreateForBuild(infos, resolver);
if (partitions.Sum(p => p.Benchmarks.Length) != 6) throw new InvalidOperationException("Expected six authored generation cases.");
var outputs = new List<object>();
foreach (var partition in partitions)
{
    var toolchain = partition.RepresentativeBenchmarkCase.GetToolchain();
    var generated = toolchain.Generator.GenerateProject(partition, ConsoleLogger.Default, Path.GetFullPath(args[0]));
    if (!generated.IsGenerateSuccess) throw new InvalidOperationException("Generation failed.", generated.GenerateException);
    var built = toolchain.Builder.Build(generated, partition, ConsoleLogger.Default);
    outputs.Add(new { generated = generated.IsGenerateSuccess, built = built.IsBuildSuccess, cases = partition.Benchmarks.Length,
        project = generated.ArtifactsPaths.ProjectFilePath, binaries = generated.ArtifactsPaths.BinariesDirectoryPath,
        executable = generated.ArtifactsPaths.ExecutablePath, benchmarkAssembly = partition.AssemblyLocation });
    File.WriteAllText(args[1], JsonSerializer.Serialize(outputs));
    if (!built.IsBuildSuccess) throw new InvalidOperationException("Build-only generation failed: " + built.ErrorMessage);
}
Console.WriteLine("Generated/built declared six cases; executor and benchmark methods were never invoked.");

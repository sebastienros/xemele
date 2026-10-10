using BenchmarkDotNet.Running;
using Xemele.Benchmarks;

if (args is ["--oracle"])
{
    foreach (int length in new[] { 16, 4096 })
    { var benchmark = new EntityCompositionBenchmarks { Length = length }; benchmark.Setup(); benchmark.FullOutputOracle(); }
    var lookup = new EntityLookupBenchmarks(); lookup.Setup(); lookup.FullOutputOracle();
    Console.WriteLine("Real timed methods/full DOM, immutable model, entity/default lookup oracles passed; no measurements.");
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

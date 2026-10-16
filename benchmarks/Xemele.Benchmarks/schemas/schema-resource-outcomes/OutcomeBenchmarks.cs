using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M8SchemaResourceOutcomes;

[MemoryDiagnoser]
[ShortRunJob]
public class OutcomeBenchmarks
{
    private OutcomeFixture fixture = null!;
    [GlobalSetup] public void Setup() => fixture = new();
    [GlobalCleanup] public void Cleanup() => fixture.Dispose();
    [Benchmark(Baseline = true)] public void DirectLegacyDenied() => fixture.RunPrimitive(0);
    [Benchmark] public void AdaptedDenied() => fixture.RunPrimitive(1);
    [Benchmark] public void ResultFactory() => fixture.RunPrimitive(2);
    [Benchmark] public object OptionalUnavailable() => fixture.RunOwned(0);
    [Benchmark] public object OptionalResolved() => fixture.RunOwned(1);
}

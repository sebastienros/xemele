using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M8XsdMissingComponents;

[MemoryDiagnoser]
[ShortRunJob]
public class MissingBenchmarks
{
    private MissingFixture fixture = null!;
    [Params(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)] public int Route { get; set; }
    [GlobalSetup] public void Setup() => fixture = new();
    [Benchmark] public object Run() => fixture.Run(Route);
}

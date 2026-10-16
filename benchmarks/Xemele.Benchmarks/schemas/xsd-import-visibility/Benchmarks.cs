using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M8XsdImportVisibility;

[MemoryDiagnoser]
[ShortRunJob]
public class ImportBenchmarks
{
    private ImportFixture fixture = null!;
    [Params(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23)]
    public int Route
    {
        get; set;
    }
    [GlobalSetup] public void Setup() => fixture = new();
    [Benchmark] public object Compile() => fixture.Run(Route);
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
namespace Xemele.Benchmarks.M8XsdSchemaSyntax;
[MemoryDiagnoser, ShortRunJob]
public class SyntaxBenchmarks
{
    private SyntaxFixture fixture = null!;
    [Params(0,1,2,3,4,5,6,7,8,9,10,11)] public int Route { get; set; }
    [GlobalSetup] public void Setup() => fixture = new();
    [Benchmark] public object OwnedOutput() => fixture.Run(Route);
}

using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M8XsdContentFailureAssessment;

[MemoryDiagnoser]
[ShortRunJob]
public class ContinuationBenchmarks
{
    private ContinuationFixture fixture = null!;
    [GlobalSetup] public void Setup() => fixture = new();
    [Benchmark] public object Valid() => fixture.RunOwned(0);
    [Benchmark] public object Rejected1025() => fixture.RunOwned(2);
    [Benchmark] public object Rejected2048() => fixture.RunOwned(3);
}

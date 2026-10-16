using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
namespace Xemele.Benchmarks.M8XsdInstanceAssessment;

[MemoryDiagnoser, ShortRunJob]
public class AssessmentBenchmarks
{
    private AssessmentFixture fixture = null!;
    [Params(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15)]
    public int Route
    {
        get; set;
    }
    [GlobalSetup] public void Setup() => fixture = new();
    [Benchmark] public object OwnedValidation() => fixture.RunOwned(Route);
}

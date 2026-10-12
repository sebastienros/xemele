using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M8XsdInstanceHintOrdering;
[MemoryDiagnoser]
[ShortRunJob]
public class HintBenchmarks
{
    private HintFixture fixture = null!;
    [GlobalSetup] public void Setup() => fixture = new();
    [Benchmark] public int NamespaceToken() => fixture.Borrowed(0);
    [Benchmark] public int LongLocationToken() => fixture.Borrowed(1);
    [Benchmark] public int LongWhitespace() => fixture.Borrowed(2);
    [Benchmark] public int LongComparisonHit() => fixture.Borrowed(3);
    [Benchmark] public int LongComparisonMiss() => fixture.Borrowed(4);
    [Benchmark] public int TableHit() => fixture.Borrowed(5);
    [Benchmark] public object TimelyDom() => fixture.Owned(1);
    [Benchmark] public object LateDom() => fixture.Owned(2);
    [Benchmark] public object DistinctNamespaces() => fixture.Owned(4);
}

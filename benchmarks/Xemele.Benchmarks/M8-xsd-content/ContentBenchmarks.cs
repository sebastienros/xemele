using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M8XsdContent;

[MemoryDiagnoser]
[ShortRunJob]
public class ContentBenchmarks
{
    private ContentFixture fixture = null!;
    [GlobalSetup] public void Setup() => fixture = new();
    [Benchmark(Baseline = true)] public int DictionaryBaseline() { fixture.Run(0); return fixture.Consume(); }
    [Benchmark] public int BorrowedNameHitMiss() { fixture.Run(1); return fixture.Consume(); }
    [Benchmark] public int DeclarationCursor() { fixture.Run(2); return fixture.Consume(); }
    [Benchmark] public int NullableGroupCursor() { fixture.Run(3); return fixture.Consume(); }
    [Benchmark] public int WildcardCursor() { fixture.Run(4); return fixture.Consume(); }
}

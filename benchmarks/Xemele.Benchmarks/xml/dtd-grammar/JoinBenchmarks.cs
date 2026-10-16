using BenchmarkDotNet.Attributes;
using Xemele.DtdGrammarJoin;
namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class DtdGrammarJoinBenchmarks
{
    [Benchmark]
    public object EffectiveDefaults() => Join.Run().Defaults;
}

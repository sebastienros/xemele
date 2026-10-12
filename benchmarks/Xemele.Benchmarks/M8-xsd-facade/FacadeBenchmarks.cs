using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M8XsdFacade;
// BDN is an optional separately granted run, never invoked by the control probe.
[MemoryDiagnoser]
[ShortRunJob]
public class FacadeBenchmarks
{
    private FacadeFixture fixture = null!;
    private FacadePrimitiveFixture primitives = null!;
    [GlobalSetup] public void Setup() { fixture = new(); primitives = new(); }
    [Benchmark] public object Compile() => fixture.RunOwned(13);
    [Benchmark] public object ValidateDom() => fixture.RunOwned(0);
    [Benchmark] public object ValidateString() => fixture.RunOwned(9);
    [Benchmark] public object ValidateInvalid() => fixture.RunOwned(1);
    [Benchmark] public void BorrowedList() => primitives.Run(0);
    [Benchmark] public void BorrowedQNameDelimiter() => primitives.Run(1);
}

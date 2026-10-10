using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Xemele;
using Xemele.Internal.Operations;
using Xemele.Resolution.Core;

[MemoryDiagnoser, ShortRunJob]
public class CancellationBindingBenchmarks
{
    private XmlResolutionSession session = null!;
    private OperationBudget budget = null!;
    private CancellationTokenSource source = null!;
    private CancellationToken token;
    [GlobalSetup]
    public void Setup()
    {
        source = new(); token = source.Token;
        budget = new(new XmlParseOptions(), default);
        session = new(null, budget, default);
        // Correctness oracle runs outside measurement and throws on failure.
        session.BindCancellationToken(token);
        if (session.ResourcesOpened != 0 || session.BytesRead != 0) throw new InvalidOperationException();
    }
    [Benchmark(Baseline = true)] public void DirectCancellationChecks() { token.ThrowIfCancellationRequested(); budget.CheckCancellation(); }
    [Benchmark] public void BindExactToken() => session.BindCancellationToken(token);
    [Benchmark] public void BindAlternatingToken()
    {
        token = token.CanBeCanceled ? default : source.Token;
        session.BindCancellationToken(token);
    }
    [GlobalCleanup] public void Cleanup() { session.Dispose(); source.Dispose(); }
}

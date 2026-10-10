using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Internal.Operations;

[MemoryDiagnoser]
public class CommonBenchmarks
{
    private OperationBudget budget = null!;
    private long baseline;
    private CancellationToken token;
    private static readonly XmlDiagnostic Diagnostic = new("benchmark.syntax", XmlDiagnosticSeverity.Fatal,
        "Example bounded rejection", new(null,1,1,null));
    [GlobalSetup]
    public void Setup() { budget=new(null,default); token=default; }
    [Benchmark(Baseline=true)]
    public long StraightforwardCounter()
    {
        token.ThrowIfCancellationRequested();
        if(baseline >= 1048576) throw new InvalidOperationException();
        baseline++; baseline--; return baseline;
    }
    [Benchmark]
    public long BoundedCounter()
    {
        budget.CheckCancellation();
        if(!budget.TryCharge(Quota.TokenCharacters,1)) throw new InvalidOperationException();
        budget.Release(Quota.TokenCharacters,1); return budget.Count(Quota.TokenCharacters);
    }
    [Benchmark]
    public object OperationSetup() => new OperationBudget(null,default);
    [Benchmark]
    public object OwnedExceptionSnapshot() => new XmlParseException([Diagnostic]);
    [Benchmark]
    public object BoundedDiagnosticTruncation()
    {
        var diagnostics=new DiagnosticBuffer(1); diagnostics.Add(Diagnostic); diagnostics.Add(Diagnostic);
        return diagnostics.Snapshot();
    }
}

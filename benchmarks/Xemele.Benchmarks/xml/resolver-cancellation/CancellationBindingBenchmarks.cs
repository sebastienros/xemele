using BenchmarkDotNet.Attributes;
using System.Reflection;
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
        source = new();
        token = source.Token;
        budget = new(new XmlParseOptions(), default);
        session = new(null, budget, default);
        // Exercise every measured method, including both alternating states.
        VerifyAllMethods();
    }
    [Benchmark(Baseline = true)]
    public void DirectCancellationChecks()
    {
        token.ThrowIfCancellationRequested();
        budget.CheckCancellation();
    }
    [Benchmark] public void BindExactToken() => session.BindCancellationToken(token);
    [Benchmark]
    public void BindAlternatingToken()
    {
        token = token.CanBeCanceled ? default : source.Token;
        session.BindCancellationToken(token);
    }
    public void VerifyAllMethods()
    {
        var field = typeof(XmlResolutionSession).GetField("cancellation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        DirectCancellationChecks();
        BindExactToken();
        if ((CancellationToken)field.GetValue(session)! != token || !token.CanBeCanceled)
        {
            throw new InvalidOperationException("Exact benchmark token lost.");
        }

        BindAlternatingToken();
        if ((CancellationToken)field.GetValue(session)! != default || token.CanBeCanceled)
        {
            throw new InvalidOperationException("Default alternating token lost.");
        }

        BindAlternatingToken();
        if ((CancellationToken)field.GetValue(session)! != source.Token || token != source.Token)
        {
            throw new InvalidOperationException("Second alternating token lost.");
        }

        if (session.ResourcesOpened != 0 || session.BytesRead != 0)
        {
            throw new InvalidOperationException("Binding changed counters.");
        }
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        session.Dispose();
        source.Dispose();
    }
}

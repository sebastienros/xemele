#if BINDING_STANDALONE
using BenchmarkDotNet.Running;
using Xemele.Internal.Operations;
using Xemele.Resolution.Core;

public static class BindingBenchmarkProgram
{
    public static void Main(string[] args)
    {
        if (args is ["--oracle"])
        {
            var methods = new CancellationBindingBenchmarks();
            methods.Setup();
            try { methods.VerifyAllMethods(); } finally { methods.Cleanup(); }
            using var a = new CancellationTokenSource(); using var b = new CancellationTokenSource();
            using var session = new XmlResolutionSession(null, new OperationBudget(new(), default), a.Token);
            session.BindCancellationToken(b.Token); a.Cancel(); session.BindCancellationToken(default);
            if (session.ResourcesOpened != 0 || session.BytesRead != 0) throw new InvalidOperationException();
            b.Cancel();
            try { session.BindCancellationToken(b.Token); throw new InvalidOperationException("Expected cancellation."); }
            catch (OperationCanceledException error) when (error.CancellationToken == b.Token) { }
            try { session.BindCancellationToken(default); throw new Exception("Expected faulted session rejection."); }
            catch (InvalidOperationException) { }
            Console.WriteLine("Standalone binding benchmark correctness oracle passed."); return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(BindingBenchmarkProgram).Assembly).Run(args);
    }
}

#endif

#if BINDING_STANDALONE
using BenchmarkDotNet.Running;
using Xemele.Internal.Operations;
using Xemele.Resolution.Core;

public static class BindingBenchmarkProgram
{
    public static void Main(string[] args)
    {
        if (args.Length is 1 or 2 && args[0] == "--oracle")
        {
            var methods = new CancellationBindingBenchmarks();
            methods.Setup();
            try { methods.VerifyAllMethods(); } finally { methods.Cleanup(); }
            if (args.Length == 2)
            {
                var assembly = System.Reflection.Assembly.LoadFrom(args[1]);
                var type = assembly.GetType("CancellationBindingBenchmarks", throwOnError: true)!;
                var mainMethods = Activator.CreateInstance(type)!;
                type.GetMethod("Setup")!.Invoke(mainMethods, null);
                try { type.GetMethod("VerifyAllMethods")!.Invoke(mainMethods, null); }
                finally { type.GetMethod("Cleanup")!.Invoke(mainMethods, null); }
                Console.WriteLine("Main benchmark assembly all-method correctness oracle passed.");
            }
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

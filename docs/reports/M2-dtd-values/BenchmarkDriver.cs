using BenchmarkDotNet.Running;
using Xemele.Benchmarks;
if (args.Contains("--correctness"))
{
    var benchmark = new DtdValueBenchmarks(); benchmark.Setup();
    try
    {
        for (int i = 0; i < 10; i++)
        {
            if (benchmark.BorrowedDeclaration() != "text &retained; 😀 \t".Length) throw new Exception("Borrowed oracle.");
            if (benchmark.OwnedAttribute() != "中😀 \t \r") throw new Exception("Owned oracle.");
        }
    }
    finally { benchmark.Cleanup(); }
    Console.WriteLine("20 bounded benchmark correctness assertions passed."); return;
}
BenchmarkSwitcher.FromAssembly(typeof(DtdValueBenchmarks).Assembly).Run(args);

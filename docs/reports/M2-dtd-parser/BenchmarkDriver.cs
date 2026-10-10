using BenchmarkDotNet.Running;
using Xemele.Benchmarks;
using Xemele.Parsing.Dtd;
if (args.Contains("--correctness"))
{
    int assertions = 0;
    foreach (int count in new[] { 1, 64 })
    {
        var b = new DtdParserBenchmarks { Declarations = count }; b.Setup();
        try
        {
            for (int i = 0; i < 10; i++)
            {
                if (b.BorrowedScan() != "ascii中😀\r\n".Length) throw new Exception("Consumed."); assertions++;
                var whole = (DtdModel)b.OwnedGrammar(); var chunked = (DtdModel)b.ChunkedGrammar();
                if (whole.Elements.Count != count || chunked.Elements.Count != count || whole.Attributes.Count != 2 * count || chunked.Attributes.Count != 2 * count || whole.GeneralEntities[0].ReplacementText != "中😀" || chunked.GeneralEntities[0].ReplacementText != "中😀") throw new Exception("Model oracle."); assertions++;
            }
        } finally { b.Cleanup(); }
    }
    Console.WriteLine($"{assertions} benchmark correctness assertions passed."); return;
}
BenchmarkSwitcher.FromAssembly(typeof(DtdParserBenchmarks).Assembly).Run(args);

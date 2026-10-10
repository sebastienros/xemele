using BenchmarkDotNet.Running;
using Xemele.Benchmarks;
using Xemele;
using Xemele.Parsing.Dtd;
using System.Runtime.CompilerServices;
using System.Text.Json;
if (args.Contains("--correctness"))
{
    foreach (int length in new[] { 32, 4096 })
    {
        var b = new DtdCompositionBenchmarks { CommentScalars = length }; b.Setup();
        for (int i = 0; i < 10; i++)
        {
            DtdCompositionBenchmarks.Check(b.SeedAndFramedComposition());
            var seed = (DtdModel)b.SeedAndModelSnapshot();
            if (seed.Elements.Count != 0 || seed.GeneralEntities.Count != 1 || seed.ParameterEntities.Count != 1) throw new Exception("seed oracle");
        }
        var scan = new DtdFramedScanBenchmarks { CommentScalars = length }; scan.Setup();
        for (int i = 0; i < 1024; i++) if (scan.BorrowedCommentScan() != DtdParseStep.NeedInput) throw new Exception("scan oracle");
        scan.Cleanup();
    }
    Console.WriteLine("runtime=" + Environment.Version + "; two lengths, 20 composition/seed outputs and 2048 borrowed scan calls passed"); return;
}
if (args.Contains("--allocation"))
{
    // Initialization, owned seed/model output and borrowed steady scans are separate.
    long coldStart = GC.GetAllocatedBytesForCurrentThread();
    var cold = new DtdCompositionBenchmarks { CommentScalars = 32 }; cold.Setup();
    DtdCompositionBenchmarks.Check(cold.SeedAndFramedComposition());
    long coldSetupAndFirstComposition = GC.GetAllocatedBytesForCurrentThread() - coldStart;
    var b = new DtdCompositionBenchmarks { CommentScalars = 4096 }; b.Setup();
    for (int i = 0; i < 128; i++) { GC.KeepAlive(b.SeedAndFramedComposition()); GC.KeepAlive(b.SeedAndModelSnapshot()); }
    long start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1024; i++) GC.KeepAlive(b.SeedAndFramedComposition());
    long ownedComposition = GC.GetAllocatedBytesForCurrentThread() - start;
    start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1024; i++) GC.KeepAlive(b.SeedAndModelSnapshot());
    long ownedSeed = GC.GetAllocatedBytesForCurrentThread() - start;
    var scan = new DtdFramedScanBenchmarks { CommentScalars = 4096 }; scan.Setup();
    for (int i = 0; i < 128; i++) scan.BorrowedCommentScan();
    start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1024; i++) if (scan.BorrowedCommentScan() != DtdParseStep.NeedInput) throw new Exception("scan");
    long warmBorrowed = GC.GetAllocatedBytesForCurrentThread() - start; scan.Cleanup();
    start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1024; i++) GC.KeepAlive(new byte[128]);
    long positiveControl = GC.GetAllocatedBytesForCurrentThread() - start;
    var retained = Retention(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    bool inputReleased = !retained.Input.IsAlive, mappingsReleased = !retained.Maps.IsAlive;
    if (warmBorrowed != 0 || positiveControl == 0 || !inputReleased || !mappingsReleased) throw new Exception("allocation/retention boundary");
    Console.WriteLine(JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), iterations = 1024, coldSetupAndFirstComposition, ownedComposition, ownedSeed, warmBorrowed, positiveControl, inputReleased, mappingsReleased, retainedModelElements = retained.Model.Elements.Count }));
    GC.KeepAlive(retained.Model); return;
}
BenchmarkSwitcher.FromAssembly(typeof(DtdCompositionBenchmarks).Assembly).Run(args);

[MethodImpl(MethodImplOptions.NoInlining)]
static (WeakReference Input, WeakReference Maps, DtdModel Model) Retention()
{
    var ledger = new OperationLedger();
    var prior = DtdCompositionBenchmarks.MakePrior(ledger);
    using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, new(null, 1, 1, 3));
    p.SeedPriorModel(prior, default); p.SetFramedInitialSource(new(null, 1, 1, 3));
    char[] input = ("<!--" + new string('x', 8192) + "-->%x;").ToCharArray();
    long[] maps = Enumerable.Range(3, input.Length).Select(i => (long)i).ToArray();
    p.CompleteByteSource(3 + input.Length);
    if (p.Append(input, maps, true, default).Step != DtdParseStep.Completed) throw new Exception("retention");
    return (new(input), new(maps), p.Model);
}

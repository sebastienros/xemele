// Correctness preparation only: no stopwatch or BDN execution/timing.
foreach (int length in new[] { 8, 4096 })
    foreach (bool crLf in new[] { false, true })
    {
        var benchmark = new DtdFramingScanBenchmarks { Length = length, CrLf = crLf };
        benchmark.Setup();
        for (int iteration = 0; iteration < 3; iteration++)
        {
            benchmark.Reset();
            for (int call = 0; call < 8192; call++)
                if (benchmark.ScalarBaseline() != benchmark.IncrementalFramer()) throw new Exception("Consumption differs.");
            benchmark.ValidateCheckpoint();
        }
        benchmark.Cleanup();
    }
foreach (string corpus in new[] { "short", "long-label", "chunked", "absent", "multilingual", "malformed" })
{
    var benchmark = new DtdFramingOwnershipBenchmarks { Corpus = corpus };
    benchmark.Setup();
    int expected = corpus switch { "absent" or "multilingual" => 2, "malformed" => 3, _ => 1 };
    if (benchmark.FrameOwnedResult() != expected) throw new Exception("Wrong ownership workload result.");
}
Console.WriteLine("Prepared 4 scan combinations x3 iterations x8192 calls with equal cursor/consumption; 6 owned-result workloads; no timing.");

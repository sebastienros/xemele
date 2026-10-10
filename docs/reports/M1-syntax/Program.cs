using BenchmarkDotNet.Running;
using Xemele;
using Xemele.Parsing.Syntax;
if (args.Contains("--allocation"))
{
    // Fresh setup, warmed repeated setup, warmed dispatch and retention are
    // separate boundaries. No latency timing occurs in this measurement mode.
    long coldBefore = GC.GetAllocatedBytesForCurrentThread();
    using var cold = new XmlParserCore(new());
    Console.WriteLine($"fresh setup allocation: {GC.GetAllocatedBytesForCurrentThread() - coldBefore} B");
    for (int i = 0; i < 100; i++) new XmlParserCore(new()).Dispose();
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++) new XmlParserCore(new()).Dispose();
    Console.WriteLine($"warm setup allocation: {(GC.GetAllocatedBytesForCurrentThread() - before) / 10000.0} B/op");
    var sink = new SyntaxBenchmarks.NullSink();
    using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null, TokenCharacters = null } });
    core.Append("<r>", [], false, sink, default);
    const string chunk = "<c a='v' xmlns:p='u' p:a='w'/>α😀&amp;\r\n";
    for (int i = 0; i < 100; i++) core.Append(chunk, [], false, sink, default);
    before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++) if (core.Append(chunk, [], false, sink, default).Step != ScanStep.NeedInput) throw new Exception();
    Console.WriteLine($"warm dispatch allocation: {GC.GetAllocatedBytesForCurrentThread() - before} B / 10000 appends; consumed text units {sink.Units}");
    var retained = new XmlParserCore[1000];
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long Heap() { var m = GC.GetGCMemoryInfo(); return m.HeapSizeBytes - m.FragmentedBytes; }
    long baseline = Heap();
    for (int i = 0; i < retained.Length; i++) { retained[i] = new(new()); retained[i].Append("<r xmlns='u'>", [], false, sink, default); }
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    Console.WriteLine($"retained one-open-root heap-minus-fragmentation estimate: {(Heap() - baseline) / (double)retained.Length} B/op (excludes preallocated reference array; informational)");
    GC.KeepAlive(retained);
    foreach (var parser in retained) parser.Dispose();
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(SyntaxBenchmarks).Assembly).Run(args);

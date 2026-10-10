using BenchmarkDotNet.Running;
using Xemele.Internal.Operations;
if(args.Contains("--setup-allocation"))
{
    long start=GC.GetAllocatedBytesForCurrentThread();
    var cold=new OperationBudget(null,default);
    Console.WriteLine($"First operation construction on fresh process: {GC.GetAllocatedBytesForCurrentThread()-start} B (includes static/JIT setup on this thread).");
    for(int i=0;i<100;i++) GC.KeepAlive(new OperationBudget(null,default));
    start=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<10000;i++) GC.KeepAlive(new OperationBudget(null,default));
    Console.WriteLine($"Warm operation construction: {(GC.GetAllocatedBytesForCurrentThread()-start)/10000.0} B/op.");
    var retained = new OperationBudget[100000];
    long heapBefore = GC.GetTotalMemory(true);
    var gcBefore=GC.GetGCMemoryInfo();
    start=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<retained.Length;i++) retained[i]=new OperationBudget(null,default);
    long retainedAllocated=GC.GetAllocatedBytesForCurrentThread()-start;
    long heapAfter = GC.GetTotalMemory(true);
    var gcAfter=GC.GetGCMemoryInfo();
    Console.WriteLine($"Escaping construction allocation: {retainedAllocated/(double)retained.Length} B/op.");
    Console.WriteLine($"Retained operation heap estimate: {(heapAfter-heapBefore)/(double)retained.Length} B/op (GC total-memory delta; excludes preallocated reference array).");
    Console.WriteLine($"GC heap minus fragmentation delta: {((gcAfter.HeapSizeBytes-gcAfter.FragmentedBytes)-(gcBefore.HeapSizeBytes-gcBefore.FragmentedBytes))/(double)retained.Length} B/op; informational whole-process estimate.");
    GC.KeepAlive(retained); GC.KeepAlive(cold); return;
}
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

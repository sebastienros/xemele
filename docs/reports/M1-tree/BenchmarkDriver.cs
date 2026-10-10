using BenchmarkDotNet.Running;
using Xemele;
using Xemele.TreeMeasurements;
if(args.Contains("--allocation"))
{
    long begin=GC.GetAllocatedBytesForCurrentThread();var cold=TreeBenchmarks.Build();long first=GC.GetAllocatedBytesForCurrentThread()-begin;
    Console.WriteLine($"cold first representative tree allocation={first} bytes; retains document through node handle");GC.KeepAlive(cold);
    for(int i=0;i<20;i++)TreeBenchmarks.Build();
    var retained=new XmlNode[1000];GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    long heapBefore=GC.GetGCMemoryInfo().HeapSizeBytes-GC.GetGCMemoryInfo().FragmentedBytes;
    begin=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<retained.Length;i++)retained[i]=TreeBenchmarks.Build();
    long allocated=GC.GetAllocatedBytesForCurrentThread()-begin;
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();long heapAfter=GC.GetGCMemoryInfo().HeapSizeBytes-GC.GetGCMemoryInfo().FragmentedBytes;
    Console.WriteLine($"warm owned representative tree={allocated/retained.Length} bytes/tree; heap-minus-fragmentation estimate={(heapAfter-heapBefore)/retained.Length} bytes/tree excluding handle array");
    var doc=new XmlDocument();var root=doc.CreateElement("repeated","urn:r","p");doc.DocumentNode.AppendChild(root);
    doc.CreateElement("repeated","urn:r","p");begin=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)doc.FindInternedName("repeated".AsSpan());
    Console.WriteLine($"10000 warm borrowed intern hits={GC.GetAllocatedBytesForCurrentThread()-begin} bytes");
    begin=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)doc.CreateElement("repeated","urn:r","p");
    Console.WriteLine($"1000 detached repeated-name nodes including arena growth={GC.GetAllocatedBytesForCurrentThread()-begin} bytes");
    begin=GC.GetAllocatedBytesForCurrentThread();doc.CreateElement("unique-persistent-name");Console.WriteLine($"one new interned name and detached node={GC.GetAllocatedBytesForCurrentThread()-begin} bytes");
    var cloned=retained[0].Clone();var imported=new XmlDocument().ImportNode(retained[0]);
    Console.WriteLine($"clone/import semantic oracle: children={cloned.Children.Count}/{imported.Children.Count}; distinct={cloned!=retained[0] && imported!=retained[0]}");
    GC.KeepAlive(retained);GC.KeepAlive(root);GC.KeepAlive(cloned);GC.KeepAlive(imported);
}
else BenchmarkSwitcher.FromAssembly(typeof(TreeBenchmarks).Assembly).Run(args);

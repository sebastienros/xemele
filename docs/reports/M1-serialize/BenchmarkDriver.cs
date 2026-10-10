using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Xemele;
using Xemele.SerializationMeasurements;
if (args.Contains("--allocation"))
{
    long coldBefore = GC.GetAllocatedBytesForCurrentThread();
    var first = Document(1); first.Save(Stream.Null);
    Console.WriteLine($"cold setup+first stream bytes={GC.GetAllocatedBytesForCurrentThread()-coldBefore}");
    foreach (int length in new[] { 16, 1_000_000 })
    {
        var d = Document(length); var o = new XmlSaveOptions { OmitXmlDeclaration = true };
        d.Save(Stream.Null, o); d.ToXmlString(o);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 50; i++) d.Save(Stream.Null, o);
        long streamBytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 50;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++) GC.KeepAlive(d.ToXmlString(o));
        long ownedBytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 10;
        Console.WriteLine($"length={length} warm stream bytes/op={streamBytes}; owned string bytes/op={ownedBytes}");
        if (streamBytes > 10000) throw new Exception("Streaming scratch unexpectedly depends on text length");
    }
    long retainedBefore = GC.GetTotalMemory(true); var weak = SerializeTemporary();
    for (int i=0;i<3;i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    Console.WriteLine($"temporary owner retained={weak.IsAlive}; heap delta={GC.GetTotalMemory(true)-retainedBefore}");
    if (weak.IsAlive) throw new Exception("Serializer retained document");
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(SerializerBenchmarks).Assembly).Run(args, DefaultConfig.Instance.AddJob(Job.ShortRun.AsDefault()));
static XmlDocument Document(int length)
{
    var d = new XmlDocument(); var r = d.CreateElement("r"); d.DocumentNode.AppendChild(r); r.AppendChild(d.CreateText(new string('x', length))); return d;
}
[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static WeakReference SerializeTemporary() { var d=Document(1_000_000); d.Save(Stream.Null); return new(d); }

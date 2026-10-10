using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TreeSpike;

if (args.Contains("--self-check", StringComparer.Ordinal)) { Checks.Run(); return; }
if (args.Contains("--memory", StringComparer.Ordinal)) { Measurements.Run(); return; }
var job = Job.ShortRun.AsDefault();
BenchmarkSwitcher.FromAssembly(typeof(Construction).Assembly).Run(args,
    DefaultConfig.Instance.AddJob(job).AddExporter(JsonExporter.Full));

internal static class Checks
{
    public static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected rejection"); }
    public static void Run()
    {
        int cases = 0;
        foreach (int count in new[] { 2, 17, 1024, 16384 })
        foreach (bool unique in new[] { false, true })
        {
            var tokens = new Tokens(count, unique);
            long expected = 0;
            for (int i = 0; i < count; i++) expected += tokens.At(i).Length + "payload".Length;
            Require(Factory.Arena(tokens).Traverse() == expected, "arena checksum");
            Require(Factory.Objects(tokens).Traverse() == expected, "object checksum"); cases++;
        }
        var arena = new ArenaTree(); var root = arena.Add("root", "", -1); var parent = arena.Add("p", "", 0); var child = arena.Add("c", "", 1);
        for (int i = 0; i < 10000; i++) arena.Add("growth", "", 0);
        Require(child.Name == "c" && child == new Handle(arena, 2), "stable handle across growth");
        Require(child != new Handle(new ArenaTree(), 2), "document participates in identity");
        long checksum = arena.Traverse(); child.MoveTo(root); child.MoveTo(parent);
        Require(arena.Traverse() == checksum, "move checksum");
        Reject(() => parent.MoveTo(child)); Reject(() => root.MoveTo(child));
        Reject(() => child.MoveTo(new Handle(new ArenaTree(), 0)));
        // Move first/middle/last siblings and a subtree; round trips retain every node.
        for (int i = 3; i < 12; i++) { var handle = new Handle(arena, i); handle.MoveTo(parent); handle.MoveTo(root); }
        Require(arena.Traverse() == checksum, "sibling relink checksum");
        var objects = new ObjectTree(); var objectRoot = objects.Add("root", "", null); var objectParent = objects.Add("p", "", objectRoot); var objectChild = objects.Add("c", "", objectParent);
        for (int i = 0; i < 9; i++) objects.Add("growth", "", objectRoot).MoveTo(objectParent);
        long objectChecksum = objects.Traverse(); objectParent.MoveTo(objectRoot); objectChild.MoveTo(objectRoot); objectChild.MoveTo(objectParent);
        Require(objects.Traverse() == objectChecksum, "object relink");
        Reject(() => objectParent.MoveTo(objectChild)); Reject(() => objectRoot.MoveTo(objectChild));
        Reject(() => objectChild.MoveTo(new ObjectTree().Add("foreign", "", null)));
        // Iterative traversal on adversarial depth, no recursive stack dependency.
        var deepArena = new ArenaTree(); var deepObjects = new ObjectTree(); ObjectTree.Node? previous = null;
        for (int i = 0; i < 50000; i++) { deepArena.Add("x", "", i - 1); previous = deepObjects.Add("x", "", previous); }
        Require(deepArena.Traverse() == 50000 && deepObjects.Traverse() == 50000, "deep traversal");
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] samples = ["node", "NODE", "節点", "İ", "i", "", "nodeprefix", "\ud800"];
        var oldCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("tr-TR");
            foreach (string sample in samples)
            {
                string owned = Factory.Intern(names, sample.AsSpan());
                Require(owned == sample && ReferenceEquals(owned, Factory.Intern(names, sample.AsSpan())), "owned ordinal intern");
            }
            var lookup = names.GetAlternateLookup<ReadOnlySpan<char>>();
            Require(!lookup.ContainsKey("nod".AsSpan()) && !lookup.ContainsKey("unknown".AsSpan()), "prefix/unknown miss");
            Require(names.Count == samples.Length, "ordinal distinctness");
            long before = GC.GetAllocatedBytesForCurrentThread(); int sum = 0;
            for (int i = 0; i < 10000; i++) sum += lookup["node".AsSpan()].Length;
            Require(GC.GetAllocatedBytesForCurrentThread() == before && sum == 40000, "borrowed warm 0 B regression");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = oldCulture; }
        var namesBench = new Names();
        foreach (bool unique in new[] { false, true })
        foreach (int length in new[] { 8, 48 })
        {
            namesBench.Unique = unique; namesBench.Length = length; namesBench.Setup();
            Require(namesBench.BorrowedLookup() == namesBench.TemporaryLookup(), "lookup oracle");
            Require(namesBench.BorrowedIntern() == namesBench.TemporaryIntern(), "intern oracle");
        }
        var text = new Text { Length = 32 }; text.Setup(); Require(text.Owned().AsSpan().SequenceEqual(text.RetainedView().Span), "text content oracle");
        Require(Probes.LiteralMatches("node"u8) && !Probes.LiteralMatches("nodes"u8), "static literal exact match");
        Console.WriteLine($"PASS: {cases} construction fixtures; owner/identity/growth/cycle/sibling/deep-tree checks; ordinal Unicode/culture/miss oracle; 0 B warm borrowed lookup; name/text equivalence.");
    }
}

internal static class Probes
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool LiteralMatches(ReadOnlySpan<byte> token) => token.SequenceEqual("node"u8);
}

internal static class Measurements
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static void FullGc() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    private static long Allocation(Func<object> factory)
    {
        long before = GC.GetAllocatedBytesForCurrentThread(); object result = factory();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before; GC.KeepAlive(result); return bytes;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Retained(Func<object> factory)
    {
        FullGc(); long before = GC.GetGCMemoryInfo().HeapSizeBytes - GC.GetGCMemoryInfo().FragmentedBytes;
        var retained = new object[16]; for (int i = 0; i < retained.Length; i++) retained[i] = factory();
        FullGc(); long bytes = GC.GetGCMemoryInfo().HeapSizeBytes - GC.GetGCMemoryInfo().FragmentedBytes - before; GC.KeepAlive(retained);
        return (bytes - (24 + 16 * IntPtr.Size)) / 16;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Retained, WeakReference Source) TextOwnership(bool own)
    {
        string source = new('文', 1024 * 1024);
        var weak = new WeakReference(source);
        object retained = own ? new string(source.AsSpan(100, 32)) : source.AsMemory(100, 32);
        return (retained, weak);
    }
    public static void Run()
    {
        var records = new List<object>();
        foreach (bool unique in new[] { false, true })
        {
            var tokens = new Tokens(16384, unique);
            foreach (bool arena in new[] { false, true })
            {
                Func<object> factory = arena ? () => Factory.Arena(tokens) : () => Factory.Objects(tokens);
                long cold = Allocation(factory), warm = Allocation(factory);
                var samples = new long[5]; for (int i = 0; i < 5; i++) samples[i] = Retained(factory);
                Array.Sort(samples);
                records.Add(new { representation = arena ? "arena" : "objects", nodes = 16384, unique, firstCallAllocation = cold, warmAllocation = warm, retainedBytesPerTreeMedian = samples[2], retainedSortedSamples = samples });
            }
        }
        foreach (bool own in new[] { false, true })
        {
            FullGc(); long before = GC.GetGCMemoryInfo().HeapSizeBytes - GC.GetGCMemoryInfo().FragmentedBytes;
            var result = TextOwnership(own); FullGc();
            long retained = GC.GetGCMemoryInfo().HeapSizeBytes - GC.GetGCMemoryInfo().FragmentedBytes - before;
            bool sourceAlive = result.Source.IsAlive;
            Checks.Require(sourceAlive == !own, "text backing ownership retention");
            records.Add(new { textOwnership = own ? "owned-32-chars" : "borrowed-32-chars", backingSourceChars = 1024 * 1024, sourceAliveAfterFullGc = sourceAlive, retainedBytes = retained });
            GC.KeepAlive(result.Retained);
        }
        var coldNames = new Names { Unique = false, Length = 8 };
        long setupBefore = GC.GetAllocatedBytesForCurrentThread(); coldNames.Setup();
        long setupBytes = GC.GetAllocatedBytesForCurrentThread() - setupBefore;
        coldNames.BorrowedLookup(); long lookupBefore = GC.GetAllocatedBytesForCurrentThread(); coldNames.BorrowedLookup();
        long warmLookupBytes = GC.GetAllocatedBytesForCurrentThread() - lookupBefore;
        Checks.Require(warmLookupBytes == 0, "warm lookup allocation");
        records.Add(new { nameSetupFixtureAndTableAllocation = setupBytes, warmBorrowed1024LookupsAllocation = warmLookupBytes });
        Console.WriteLine(JsonSerializer.Serialize(new {
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            serverGc = System.Runtime.GCSettings.IsServerGC, records
        }, Options));
    }
}

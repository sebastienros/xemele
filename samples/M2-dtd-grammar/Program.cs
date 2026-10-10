using System.Text.Json;
using Xemele.DtdGrammarJoin;
if (args.Contains("--contract")) { Inventory.Print(); return; }
if (args.Contains("--allocations")) { Allocations.Run(); return; }
var result = Join.Run();
if (result.Model.Elements.Count != 3 || result.Model.Elements[0].Content.Kind != Xemele.Parsing.Dtd.DtdParticleKind.Sequence ||
    !result.Defaults.SequenceEqual(new[] { "x y", "中😀\t" })) throw new Exception("Independent composed output oracle.");
Console.WriteLine(JsonSerializer.Serialize(new { elements = result.Model.Elements.Select(d => d.Name), result.Defaults,
    internalPrecedence = !result.Model.GeneralEntities[0].IsExternal, rawExternalDefault = result.Model.Attributes[1].DefaultValue }));
internal static class Allocations
{
    internal static void Run()
    {
        const int count = 1000;
        long before = GC.GetAllocatedBytesForCurrentThread(); var cold = Join.Run(); long coldBytes = GC.GetAllocatedBytesForCurrentThread() - before; GC.KeepAlive(cold);
        for (int i = 0; i < count; i++) Join.Run();
        before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < count; i++) Join.Run(); long ownedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine(JsonSerializer.Serialize(new { coldBytes, ownedBytes, operations = count, ownedBytesPerOperation = ownedBytes / count,
            runtime = Environment.Version.ToString(), boundary = "Two parser setups, persisted declarations/models, effective table and owned normalized defaults; synchronous current-thread allocations. No zero-allocation composition promise. No retained-memory or cross-platform inference." }));
    }
}

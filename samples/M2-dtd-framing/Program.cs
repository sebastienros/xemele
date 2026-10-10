using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dtd;

if (args.Contains("--allocations"))
{
    AllocationProbe.Run();
    return;
}

// Character framing is independently usable; decoding/resolution is a caller's
// separate operation. The output below contains only owned values.
using var framer = new XmlTextDeclarationFramer(new(), new Uri("https://fixture.invalid/subset.dtd"));
var first = framer.Append("<?xml\r", [], false, default);
if (first.Step != TextDeclarationStep.NeedInput) throw new Exception("Expected bounded carry.");
var result = framer.Append("\nversion='1.0' encoding='UTF-8'?>body", [], true, default);
if (result.Step != TextDeclarationStep.Declared || result.Consumed != 33 || result.NextLocation.LineNumber != 2)
    throw new Exception("Invalid declaration handoff.");
Console.WriteLine(JsonSerializer.Serialize(result));
using var absent = new XmlTextDeclarationFramer(new());
var absentResult = absent.Append("<?xml-stylesheet?>", [], true, default);
char[] prefix = new char[7];
int count = absent.DrainPrefix(prefix, []);
if (count != 6 || new string(prefix, 0, count) != "<?xml-") throw new Exception("Prefix lost.");
Console.WriteLine($"Absent: prefix={new string(prefix, 0, count)}, consumed={absentResult.Consumed}, body suffix resumes at {count}.");

internal static class AllocationProbe
{
    public static void Run()
    {
        // First-use managed allocations include initialization/JIT helper effects.
        long before = GC.GetAllocatedBytesForCurrentThread();
        using var cold = new XmlTextDeclarationFramer(new());
        cold.Append("<?xml encoding='UTF-8'?>", [], true, default);
        long coldBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        for (int i = 0; i < 1000; i++) { using var warmup = new XmlTextDeclarationFramer(new()); warmup.Append("<?xml encoding='UTF-8'?>", [], true, default); }
        const int iterations = 1000;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) { using var f = new XmlTextDeclarationFramer(new()); f.Append("<?xml encoding='UTF-8'?>", [], true, default); }
        long ownedPerOperation = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
        using var primitive = new XmlTextDeclarationFramer(new() { TokenCharacters = null, ExpandedCharacters = null });
        primitive.Append("<?xml ", [], false, default);
        for (int i = 0; i < iterations; i++) primitive.Append(" \t\r\n", [], false, default);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) primitive.Append(" \t\r\n", [], false, default);
        long warmPrimitiveBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        _ = RetainResults(iterations); // Warm the persistence helper before measuring live storage.
        Collect();
        long retainedBefore = GC.GetTotalMemory(false);
        var retained = RetainResults(iterations);
        Collect();
        long retainedBytes = GC.GetTotalMemory(false) - retainedBefore;
        int declarationSize = System.Runtime.CompilerServices.Unsafe.SizeOf<XmlTextDeclaration>();
        GC.KeepAlive(retained);
        Console.WriteLine(JsonSerializer.Serialize(new { coldBytes, ownedPerOperation, warmPrimitiveBytes, retainedResults = iterations,
            retainedBytes, declarationSize, retainedMeaning = "1000 owned five-character encoding strings and result array after forced compacting collections; GC total-memory estimate, no borrowed source retained", runtime = Environment.Version.ToString() }));
        if (warmPrimitiveBytes != 0) throw new Exception("Primitive allocated.");
    }
    private static void Collect()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static XmlTextDeclaration[] RetainResults(int count)
    {
        XmlTextDeclaration[] result = new XmlTextDeclaration[count];
        for (int i = 0; i < count; i++)
        {
            using var f = new XmlTextDeclarationFramer(new());
            result[i] = f.Append("<?xml encoding='UTF-8'?>", [], true, default).Declaration!.Value;
        }
        return result;
    }
}

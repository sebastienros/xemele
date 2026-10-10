using System.Reflection;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dtd;

if (args.Contains("--contract"))
{
    foreach (Type type in new[] { typeof(DtdValueDecoder), typeof(DtdValueResult), typeof(DtdValueStep), typeof(DtdValueProcessor), typeof(DtdNormalizedValue) })
    {
        Console.WriteLine(type.FullName);
        foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m is MethodBase or PropertyInfo).OrderBy(m => m.ToString(), StringComparer.Ordinal))
            Console.WriteLine("  " + member);
    }
    return;
}
char[] scratch = new char[64];
long coldBytes = 0;
if (args.Contains("--allocations"))
{
    long coldBefore = GC.GetAllocatedBytesForCurrentThread();
    using var cold = new DtdValueDecoder(new()); cold.Decode("x&#9;", scratch, true);
    coldBytes = GC.GetAllocatedBytesForCurrentThread() - coldBefore;
}
using var decoder = new DtdValueDecoder(new());
var prefix = decoder.Decode("&#000", scratch, false);
if (prefix.Consumed != 5 || prefix.Written != 0 || prefix.Step != DtdValueStep.NeedInput) throw new Exception("Progress.");
var end = decoder.Decode("9; &later;", scratch, true);
if (end.Step != DtdValueStep.Completed) throw new Exception("Completion.");
string replacement = new(scratch, 0, end.Written);
var table = new Dictionary<string, DtdEntityDeclaration>(StringComparer.Ordinal)
{ ["outer"] = new("outer", false, replacement, null, null, null, false, default),
  ["later"] = new("later", false, "result", null, null, null, false, default) };
var normalized = DtdValueProcessor.NormalizeAttributeDefault("&outer; &#9;", DtdAttributeType.NmTokens, table, new());
if (normalized.Value != "result \t") throw new Exception("Origin normalization.");
Console.WriteLine(JsonSerializer.Serialize(new { replacement, normalized.Value }));

if (!args.Contains("--allocations")) return;
long before;
const int iterations = 1000;
for (int i = 0; i < iterations; i++) { using var setup = new DtdValueDecoder(new()); setup.Decode("x&#9;", scratch, true); }
before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < iterations; i++) { using var setup = new DtdValueDecoder(new()); setup.Decode("x&#9;", scratch, true); }
long setupBytesPerOperation = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
using var warm = new DtdValueDecoder(new() { TokenCharacters = null, ExpandedCharacters = null });
for (int i = 0; i < iterations; i++) warm.Decode("x&name;&#x1F600;", scratch, false);
before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < iterations; i++) warm.Decode("x&name;&#x1F600;", scratch, false);
long warmPrimitiveBytes = GC.GetAllocatedBytesForCurrentThread() - before;
for (int i = 0; i < iterations; i++) DtdValueProcessor.NormalizeAttributeDefault("&outer; &#9;", DtdAttributeType.NmTokens, table, new());
before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < iterations; i++) DtdValueProcessor.NormalizeAttributeDefault("&outer; &#9;", DtdAttributeType.NmTokens, table, new());
long ownedBytesPerOperation = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
// Separate persistence estimate; forced collections are outside benchmark timing.
_ = Retain(iterations, table); Collect();
long retainedBefore = GC.GetTotalMemory(false);
string[] retained = Retain(iterations, table); Collect();
long retainedBytes = GC.GetTotalMemory(false) - retainedBefore;
GC.KeepAlive(retained);
Console.WriteLine(JsonSerializer.Serialize(new { coldBytes, setupBytesPerOperation, warmPrimitiveBytes, ownedBytesPerOperation, retainedBytes,
    retainedResults = iterations, retainedMeaning = "GC total-memory estimate for 1000 owned eight-unit strings plus result array; input/table already live, transient builders/frames collected; separate from BDN GC settings", runtime = Environment.Version.ToString(),
    ownership = "Decoder setup owns bounded scalar/numeric state. Normalization owns builder, iterative frames, cycle set and final string; table belongs to caller. Cold probe is first decoder use in a fresh process." }));
if (warmPrimitiveBytes != 0) throw new Exception("Warm primitive allocation.");

static void Collect()
{
    GC.Collect(2, GCCollectionMode.Forced, true, true); GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, true, true);
}
[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
static string[] Retain(int count, Dictionary<string, DtdEntityDeclaration> table)
{
    string[] results = new string[count];
    for (int i = 0; i < count; i++) results[i] = DtdValueProcessor.NormalizeAttributeDefault("&outer; &#9;", DtdAttributeType.NmTokens, table, new()).Value!;
    return results;
}

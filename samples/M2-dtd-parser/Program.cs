using System.Reflection;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dtd;
if (args.Contains("--contract")) { Inventory.Print(); return; }
if (args.Contains("--allocations")) { Allocation.Run(); return; }
using var p = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(new Uri("https://example.invalid/doc.xml"), 5, 12, null));
const string parent = "<!ELEMENT r (#PCDATA)><!ATTLIST r lang NMTOKEN ' en '><!ENTITY % external SYSTEM 'extra.dtd'>%external;<!ENTITY e 'x&#13;&later;'>";
var request = p.Append(parent, [], true, default);
if (request.Step != DtdParseStep.NeedEntity) throw new Exception("Missing request.");
p.PushEntity(new("https://example.invalid/extra.dtd"));
var returned = p.Append("<?xml encoding='UTF-8'?><!ENTITY later 'y'>", [], true, default);
if (returned.Step != DtdParseStep.NeedInput) throw new Exception("Parent must resume.");
var done = p.Append(parent.AsSpan(request.Consumed), [], true, default);
if (done.Step != DtdParseStep.Completed || p.Model.GeneralEntities[1].ReplacementText != "x\r&later;") throw new Exception("Owned model.");
Console.WriteLine(JsonSerializer.Serialize(new { done.Step, elements = p.Model.Elements.Count, defaults = p.Model.Attributes.Count, entities = p.Model.GeneralEntities.Count, rawDefault = p.Model.Attributes[0].DefaultValue, request.Entity }));
internal static class Inventory
{
    internal static void Print()
    {
        var nullable = new NullabilityInfoContext();
        foreach (var type in new[] { typeof(DtdParser), typeof(DtdParseStep), typeof(DtdParseResult), typeof(DtdEntityRequest) }.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            Console.WriteLine($"{type.FullName}|{type.IsSealed}|{type.IsValueType}");
            if (type.IsEnum) { foreach (var name in Enum.GetNames(type)) Console.WriteLine($"  enum:{name}={Convert.ToInt32(Enum.Parse(type, name))}"); continue; }
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(c => c.IsPublic || type == typeof(DtdParser) && c.IsAssembly).OrderBy(c => c.ToString(), StringComparer.Ordinal))
                Console.WriteLine("  ctor:" + ctor.IsPublic + "|" + string.Join(';', ctor.GetParameters().Select(Param)));
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(m => m.ToString(), StringComparer.Ordinal))
                Console.WriteLine("  method:" + method + "|" + nullable.Create(method.ReturnParameter).ReadState + "|" + string.Join(';', method.GetParameters().Select(Param)));
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(p => p.Name, StringComparer.Ordinal))
                Console.WriteLine("  property:" + property.Name + "|" + property.PropertyType + "|" + nullable.Create(property).ReadState + "|set:" + (property.SetMethod is not null));
        }
        string Param(ParameterInfo p) => p.Name + ":" + p.ParameterType + ":" + nullable.Create(p).ReadState + ":optional=" + p.IsOptional + ":default=" + (p.HasDefaultValue ? p.DefaultValue ?? "null" : "none");
    }
}
internal static class Allocation
{
    internal const string Corpus = "<!ELEMENT r (a|b)*><!ATTLIST r x CDATA 'raw &later;' y (yes|no) 'yes'><!ENTITY later '中😀'><!ENTITY % p '<!NOTATION n SYSTEM &quot;x&quot;>'>";
    internal static DtdModel Parse() { using var p = new DtdParser(DtdSubsetKind.Internal, new()); var r = p.Append(Corpus, [], true, default); if (r.Step != DtdParseStep.Completed) throw new Exception("Corpus."); return p.Model; }
    internal static void Run()
    {
        const int count = 1000;
        long before = GC.GetAllocatedBytesForCurrentThread(); var cold = Parse(); long coldBytes = GC.GetAllocatedBytesForCurrentThread() - before; GC.KeepAlive(cold);
        for (int i = 0; i < count; i++) Parse();
        before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < count; i++) Parse(); long ownedBytesPerOperation = (GC.GetAllocatedBytesForCurrentThread() - before) / count;
        using var scanner = new DtdParser(DtdSubsetKind.Internal, new() { DocumentCharacters = null, TokenCharacters = null });
        scanner.Append("<!--", [], false, default);
        for (int i = 0; i < count; i++) scanner.Append("ascii中😀\r\n", [], false, default);
        before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < count; i++) scanner.Append("ascii中😀\r\n", [], false, default); long warmPrimitiveBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        _ = Retain(count); Collect(); long retainedBefore = GC.GetTotalMemory(false); var retained = Retain(count); Collect(); long retainedBytes = GC.GetTotalMemory(false) - retainedBefore; GC.KeepAlive(retained);
        Console.WriteLine(JsonSerializer.Serialize(new { coldBytes, ownedBytesPerOperation, warmPrimitiveBytes, retainedBytes, retainedResults = count, runtime = Environment.Version.ToString(), ownership = "Parser setup owns cursor/collections/builders. Declaration scratch owns bounded lexical tokens. Model owns strings, particles, arrays. Cold includes first-use helpers. Warm comment scan has no persistence. Retained GC estimate includes 1000 model snapshots and result array after transient parser scratch collected." }));
        if (warmPrimitiveBytes != 0) throw new Exception("Warm primitive allocation.");
    }
    private static void Collect() { GC.Collect(2, GCCollectionMode.Forced, true, true); GC.WaitForPendingFinalizers(); GC.Collect(2, GCCollectionMode.Forced, true, true); }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static DtdModel[] Retain(int count) { var result = new DtdModel[count]; for (int i = 0; i < count; i++) result[i] = Parse(); return result; }
}

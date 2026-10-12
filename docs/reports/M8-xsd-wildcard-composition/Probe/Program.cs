using System.Reflection;
using System.Text.Json;
using Xemele;
using Xemele.Validation;

bool measured = args.Length == 2 && args[0] == "--measure";
if (!measured && args is not ["--control"]) throw new ArgumentException("--control or --measure output required");
long coldStart = measured ? GC.GetAllocatedBytesForCurrentThread() : 0;
var fixture = new Fixture();
long cold = measured ? GC.GetAllocatedBytesForCurrentThread() - coldStart : 0;
var first = new long[5]; var rows = new List<object>();
for (int route = 0; route < 5; route++)
{
    long start = measured ? GC.GetAllocatedBytesForCurrentThread() : 0;
    fixture.Run(route);
    if (measured) first[route] = GC.GetAllocatedBytesForCurrentThread() - start;
    for (int i = 0; i < 32; i++) fixture.Run(route);
    var samples = new long[64];
    for (int i = 0; i < samples.Length; i++)
    {
        start = measured ? GC.GetAllocatedBytesForCurrentThread() : 0;
        fixture.Run(route);
        if (measured) samples[i] = GC.GetAllocatedBytesForCurrentThread() - start;
    }
    rows.Add(new { route, firstBytes = first[route], samples });
}
if (!measured) { Console.WriteLine("CONTROL PASS:5 routes consumed, counters/timing disabled"); return; }
long positiveStart = GC.GetAllocatedBytesForCurrentThread(); fixture.Sink = new byte[1024];
long positive = GC.GetAllocatedBytesForCurrentThread() - positiveStart;
var retained = new object[64]; long before = GC.GetTotalMemory(true);
for (int i = 0; i < retained.Length; i++) retained[i] = fixture.Run(0)!;
long after = GC.GetTotalMemory(true); GC.KeepAlive(retained);
File.WriteAllText(args[1], JsonSerializer.Serialize(new { cold, positive, rows, retainedCount = 64, retainedBefore = before, retainedAfter = after, retainedDelta = after - before, sourceRevision = Environment.GetEnvironmentVariable("XEMELE_WILDCARD_REVISION") }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("MEASUREMENT COMPLETE; raw results unopened by supervisor");

sealed class Fixture
{
    internal object? Sink;
    private delegate XsdWildcard Compose(XsdWildcard a, XsdWildcard b, XmlLocation at);
    private readonly Compose union, intersect;
    private readonly XsdWildcard a = new(false, ["urn:a", "urn:b"], XsdProcessContents.Strict), b = new(false, ["urn:b", "urn:c"], XsdProcessContents.Skip);
    private readonly XsdWildcard negated = new(true, ["urn:t", ""], XsdProcessContents.Strict), absent = new(false, [""], XsdProcessContents.Strict);
    private readonly XsdWildcard longA, longB;
    private const string Xml = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:attributeGroup name='g'><xs:anyAttribute namespace='urn:a urn:b'/></xs:attributeGroup><xs:complexType name='T'><xs:attributeGroup ref='g'/><xs:anyAttribute namespace='urn:b urn:c' processContents='skip'/></xs:complexType></xs:schema>";
    internal Fixture()
    {
        var compilation = new XsdCompilation(new(), new Operation());
        union = typeof(XsdCompilation).GetMethod("Union", BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate<Compose>(compilation);
        intersect = typeof(XsdCompilation).GetMethod("Intersection", BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate<Compose>(compilation);
        longA = new(false, Enumerable.Range(0, 128).Select(i => $"urn:multilingual:é:namespace:{i}").ToArray(), XsdProcessContents.Strict);
        longB = new(false, Enumerable.Range(64, 128).Select(i => $"urn:multilingual:é:namespace:{i}").ToArray(), XsdProcessContents.Strict);
    }
    internal object? Run(int route)
    {
        switch (route)
        {
            case 0: var u = union(a, b, default); if (u.Negated || u.Namespaces.Count != 3) throw new Exception("Union"); return Sink = u;
            case 1: var v = intersect(a, b, default); if (v.Negated || v.Namespaces.Count != 1 || v.Namespaces[0] != "urn:b") throw new Exception("Intersection"); return Sink = v;
            case 2: var l = intersect(longA, longB, default); if (l.Namespaces.Count != 64) throw new Exception("Long"); return Sink = l;
            case 3:
                try { union(negated, absent, default); throw new InvalidOperationException("Invalid accepted"); }
                catch (SchemaCompileException e) { if (e.Diagnostics.Count != 1 || e.Diagnostics[0].Code != "XSD.SchemaRestriction") throw; return Sink = e; }
            default:
                var context = new SchemaCompileContext(); var graph = new XmlSchemaCompiler().Compile(context.Parse(Xml).Document, context.Options, context.Operation);
                if (graph.Types.Single(t => t.Name == new SchemaName("", "T")).AnyAttribute!.ProcessContents != XsdProcessContents.Skip) throw new Exception("Graph"); return Sink = graph;
        }
    }
    private sealed class Operation : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new(); public CancellationToken CancellationToken => default;
        public bool IsComplete => true; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation at) => true; public bool ChargeComponents(int count, XmlLocation at) => true;
        public bool AddDiagnostic(XmlDiagnostic d) => true; public void Refuse(string c, string m, XmlLocation at) => throw new InvalidOperationException(c);
    }
}

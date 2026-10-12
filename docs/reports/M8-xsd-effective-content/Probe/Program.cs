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
for (int i = 0; i < retained.Length; i++) retained[i] = fixture.Run(1)!;
long after = GC.GetTotalMemory(true); GC.KeepAlive(retained);
File.WriteAllText(args[1], JsonSerializer.Serialize(new { cold, positive, rows, retainedCount = 64, retainedBefore = before, retainedAfter = after, retainedDelta = after - before, sourceRevision = Environment.GetEnvironmentVariable("XEMELE_EFFECTIVE_CONTENT_REVISION") }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("MEASUREMENT COMPLETE; raw results unopened by supervisor");

sealed class Fixture
{
    internal object? Sink;
    private readonly XmlDocument[] documents = new XmlDocument[5];
    internal Fixture()
    {
        string[] bodies = [
            "<xs:complexType name='T'/>",
            "<xs:complexType name='T' mixed='true'/>",
            "<xs:complexType name='B' mixed='true'><xs:all/></xs:complexType><xs:complexType name='T'><xs:complexContent mixed='false'><xs:extension base='B'><xs:all/></xs:extension></xs:complexContent></xs:complexType>",
            "<xs:complexType name='T' mixed='true'><xs:sequence minOccurs='0' maxOccurs='0'><xs:element name='unused'/></xs:sequence></xs:complexType>",
            "<xs:complexType name='T'><xs:sequence minOccurs='0' maxOccurs='0'/><xs:sequence/></xs:complexType>"
        ];
        for (int i=0;i<bodies.Length;i++) documents[i]=new SchemaCompileContext().Parse("<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>"+bodies[i]+"</xs:schema>").Document;
    }
    internal object? Run(int route)
    {
        var operation = new SchemaCompileOperation();
        XsdCompiledSchema graph;
        try { graph = new XmlSchemaCompiler().Compile(documents[route], new(), operation); }
        catch (SchemaCompileException e) when(route==4)
        { if(e.Diagnostics.Count!=1||e.Diagnostics[0].Code!="XSD.SchemaRestriction")throw;return Sink=e; }
        if(route==4)throw new InvalidOperationException("Duplicate body accepted");
        var t=graph.Types.Single(t=>t.Name==new SchemaName("","T"));
        if(route is 0 or 3){if(t.ContentKind!=XsdContentKind.Empty||t.Particle is not null)throw new Exception("Empty mapping");}
        else if(t.ContentKind!=XsdContentKind.Mixed||t.Particle is null)throw new Exception("Mixed mapping");
        if(route==2&&!ReferenceEquals(t.Particle,graph.Types.Single(t=>t.Name==new SchemaName("","B")).Particle))throw new Exception("Inheritance");
        return Sink=graph;
    }
}

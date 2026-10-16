using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele;
using Xemele.Validation;

if (args.Length != 2 || args[0] is not ("--control" or "--measure")) return 2;
bool measure = args[0] == "--measure";
string revision = Environment.GetEnvironmentVariable("XEMELE_RNG_REVISION") ?? throw new InvalidOperationException("Source revision required");
if (revision.Length != 40 || Environment.Version.ToString() != "10.0.11") throw new InvalidOperationException("Pinned runtime/revision required");
const string ns = "http://relaxng.org/ns/structure/1.0";
var rows = new List<object>();
long coldBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
var coldGraph = Compile("<element xmlns='" + ns + "' name='cold'><empty/></element>");
long cold = measure ? GC.GetAllocatedBytesForCurrentThread() - coldBefore : 0;
rows.Add(new { name = "cold-owned-parse-compile", bytes = measure ? (long?)cold : null, nodes = coldGraph.Patterns.Count });
var op = new SchemaCompileOperation(new() { MaxOperations = null, Limits = new() { EvaluationWork = null } });
var name = new RngName(new("urn:short", "hit"));
string longLocal = new('x', 4096);
var longName = new RngName(new("urn:long", longLocal));
var multilingual = new RngName(new("urn:根", "树😀"));
Probe("baseline-short-hit", () => ReferenceContains("urn:short", "hit", "urn:short", "hit", op) ? 1 : 0, true);
Probe("borrowed-short-hit", () => RngNameClasses.Contains(name, "urn:short", "hit", op, default) ? 1 : 0, true);
Probe("borrowed-short-miss", () => RngNameClasses.Contains(name, "urn:short", "miss", op, default) ? 0 : 1, true);
Probe("borrowed-long-hit", () => RngNameClasses.Contains(longName, "urn:long", longLocal, op, default) ? 1 : 0, true);
Probe("borrowed-multilingual", () => RngNameClasses.Contains(multilingual, "urn:根", "树😀", op, default) ? 1 : 0, true);
byte[]? positive = null;
Probe("positive-control", () => { positive = new byte[128]; positive[0] = 1; return positive[0]; }, false);
var simple = XmlDocument.Parse("<element xmlns='" + ns + "' name='e'><empty/></element>");
var composite = XmlDocument.Parse("<grammar xmlns='" + ns + "'><start><ref name='tree'/></start><define name='tree'><element name='tree'><group><optional><attribute name='a'/></optional><zeroOrMore><ref name='tree'/></zeroOrMore></group></element></define></grammar>");
Probe("owned-simple-graph", () => { var graph = FromDocument(simple); if (graph.Patterns.Count != 3) throw new Exception("simple graph"); return 1; }, false);
Probe("owned-recursive-graph", () => { var graph = FromDocument(composite); if (graph.Patterns.Count != 10) throw new Exception("recursive graph"); return 1; }, false);
Probe("owned-name-overlap-scratch", () => RngNameClasses.Overlaps(new RngAnyName(new RngNsName("urn:a", null)), new RngNsName("urn:b", null), op, default) ? 1 : 0, false);
var invalid = XmlDocument.Parse("<element xmlns='" + ns + "' name='e'><attribute name='a'><attribute name='b'/></attribute></element>");
Probe("owned-invalid-diagnostic", () => { try { FromDocument(invalid); throw new Exception("accepted invalid"); } catch (SchemaCompileException e) { if (e.Diagnostics[0].Code != "RNG.SchemaRestriction") throw; return 1; } }, false);
long retainedBefore = measure ? GC.GetTotalMemory(true) : 0;
var retained = new RngCompiledSchema[16];
for (int i = 0; i < retained.Length; i++) retained[i] = FromDocument(composite);
long retainedAfter = measure ? GC.GetTotalMemory(true) : 0;
GC.KeepAlive(retained);
GC.KeepAlive(positive);
GC.KeepAlive(coldGraph);
File.WriteAllText(args[1], JsonSerializer.Serialize(new
{
    mode = args[0],
    sourceRevision = revision,
    runtime = Environment.Version.ToString(),
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    tiered = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
    serverGc = System.Runtime.GCSettings.IsServerGC,
    rows,
    retainedGraphCount = retained.Length,
    retainedApproximateBytes = measure ? (long?)(retainedAfter - retainedBefore) : null,
    processWorkingSet = measure ? (long?)Environment.WorkingSet : null,
    note = "Compiler graph/context/diagnostic and overlap scratch are owned allocations. Retained estimate/RSS are process-local; no timing or cross-architecture claim."
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS: " + rows.Count + " consumed routes; " + (measure ? "allocation measurement" : "counters disabled"));
return 0;

void Probe(string label, Func<int> action, bool zero)
{
    for (int i = 0; i < 32; i++)
        if (action() != 1)
            throw new Exception(label + " warm");
    long before = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
    int consumed = 0;
    for (int i = 0; i < 64; i++)
        consumed += action();
    long bytes = measure ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
    if (consumed != 64 || measure && (zero ? bytes != 0 : bytes <= 0))
        throw new Exception(label + " result/allocation control");
    rows.Add(new
    {
        name = label,
        iterations = 64,
        consumed,
        bytes = measure ? (long?)bytes : null,
        bytesPerOperation = measure ? (double?)bytes / 64 : null,
        zeroExpected = zero
    });
}
static bool ReferenceContains(ReadOnlySpan<char> ns, ReadOnlySpan<char> local, string expectedNs, string expectedLocal, ISchemaOperation op)
{
    if (!op.Charge(1, default))
        throw new InvalidOperationException("probe work");
    if (ns.Length != expectedNs.Length || local.Length != expectedLocal.Length)
        return false;
    for (int i = 0; i < ns.Length; i++)
        if (ns[i] != expectedNs[i])
            return false;
    for (int i = 0; i < local.Length; i++)
        if (local[i] != expectedLocal[i])
            return false;
    return true;
}
static RngCompiledSchema Compile(string xml)
{
    var context = new SchemaCompileContext();
    var scope = context.Parse(xml);
    return new RelaxNgCompiler().Compile(scope.Document, context.Options, context.Operation);
}
static RngCompiledSchema FromDocument(XmlDocument document)
{
    var options = new SchemaCompileOptions();
    return new RelaxNgCompiler().Compile(document, options, new SchemaCompileOperation(options));
}

using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele;
using Xemele.Validation;

if (args.Length != 2 || args[0] is not ("--control" or "--measure")) return 2;
bool measure = args[0] == "--measure";
string revision = Environment.GetEnvironmentVariable("XEMELE_SCH_REVISION") ?? throw new InvalidOperationException("Source revision required");
if (revision.Length != 40 || Environment.Version.ToString() != "10.0.11") throw new InvalidOperationException("Pinned runtime/revision required");
const string ns = "http://purl.oclc.org/dsdl/schematron";
string simpleXml = "<schema xmlns='" + ns + "'><pattern><rule context='item'><assert test='@code'>code</assert></rule></pattern></schema>";
var rows = new List<object>();
long coldBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
var context = new SchemaCompileContext();
var coldSource = context.Parse(simpleXml);
var coldGraph = new SchematronCompiler().Compile(coldSource.Document, new() { Schema = context.Options }, context.Operation);
long cold = measure ? GC.GetAllocatedBytesForCurrentThread() - coldBefore : 0;
rows.Add(new { name = "cold-owned-parse-compile", bytes = measure ? (long?)cold : null, patterns = coldGraph.Patterns.Count });
byte[]? positive = null;
Probe("positive-control", () => { positive = new byte[128]; positive[0] = 1; return positive[0]; });
var simple = XmlDocument.Parse(simpleXml);
var complex = XmlDocument.Parse("<schema xmlns='" + ns + "' defaultPhase='first'><ns prefix='p' uri='urn:p'/><let name='base' value='1'/>" +
    "<phase id='first'><let name='v' value='1'/><active pattern='p'/></phase><phase id='second'><let name='v' value='2'/><active pattern='p'/></phase>" +
    "<pattern abstract='true' id='template'><rule abstract='true' id='r'><assert test='$threshold=$base' diagnostics='d' properties='prop'>" +
    "plain<emph>bold</emph><name/><value-of select='@code'/></assert></rule><rule context='$context' subject='child'><extends rule='r'/></rule></pattern>" +
    "<pattern is-a='template' id='p'><param name='context' value='p:item | @code'/><param name='threshold' value='$v'/></pattern>" +
    "<diagnostics><diagnostic id='d'>D<value-of select='$v'/></diagnostic></diagnostics><properties><property id='prop'>P<name path='@code'/></property></properties></schema>");
var multilingual = XmlDocument.Parse("<schema xmlns='" + ns + "'><pattern><title>多言語</title><rule context='item'><assert test='true()'>" + new string('界', 4096) + "😀</assert></rule></pattern></schema>");
var invalid = XmlDocument.Parse("<schema xmlns='" + ns + "'><pattern><rule context='item'><assert test='$missing'/></rule></pattern></schema>");
Probe("owned-simple-graph", () => { var graph = Compile(simple); if (graph.Patterns.Count != 1 || graph.Patterns[0].Rules.Count != 1 || graph.Patterns[0].Rules[0].Checks.Count != 1) throw new Exception("simple graph"); return 1; });
Probe("owned-abstract-phase-message-graph", () => { var graph = Compile(complex); if (graph.Phases.Count != 2 || graph.Patterns.Count != 1 || graph.Diagnostics.Count != 1 || graph.Properties.Count != 1) throw new Exception("complex graph"); return 1; });
Probe("owned-long-multilingual-graph", () => { var graph = Compile(multilingual); if (graph.Patterns[0].Name != "多言語" || graph.Patterns[0].Rules[0].Checks[0].Text.Count != 1) throw new Exception("message graph"); return 1; });
Probe("owned-invalid-diagnostic", () => { try { Compile(invalid); throw new Exception("accepted invalid"); } catch (SchemaCompileException e) { if (e.Diagnostics[0].Code != "SCH.XPath") throw; return 1; } });
long retainedBefore = measure ? GC.GetTotalMemory(true) : 0;
var retained = new SchematronCompiledModel[16];
for (int i = 0; i < retained.Length; i++) retained[i] = Compile(complex);
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
    note = "Compiler snapshot/expansion/XPath/graph/context/diagnostics are owned setup allocations. Prepared documents and closures are outside warm counters. No borrowed-zero, timing, speedup or cross-architecture claim. Retained estimate/RSS are process-local."
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS: " + rows.Count + " consumed routes; " + (measure ? "allocation measurement" : "counters disabled"));
return 0;

void Probe(string label, Func<int> action)
{
    for (int i = 0; i < 32; i++)
        if (action() != 1)
            throw new Exception(label + " warm");
    long before = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
    int consumed = 0;
    for (int i = 0; i < 64; i++)
        consumed += action();
    long bytes = measure ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
    if (consumed != 64 || measure && bytes <= 0)
        throw new Exception(label + " result/allocation control");
    rows.Add(new
    {
        name = label,
        iterations = 64,
        consumed,
        bytes = measure ? (long?)bytes : null,
        bytesPerOperation = measure ? (double?)bytes / 64 : null
    });
}
static SchematronCompiledModel Compile(XmlDocument document)
{
    var options = new SchematronCompileOptions();
    return new SchematronCompiler().Compile(document, options, new SchemaCompileOperation(options.Schema));
}

using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele;
using Xemele.Validation;
using Xemele.XPath;

if (args.Length != 2 || args[0] is not ("--control" or "--measure")) return 2;
bool measure = args[0] == "--measure";
string revision = Environment.GetEnvironmentVariable("XEMELE_SCH_VALIDATE_REVISION") ?? throw new InvalidOperationException("Source revision required");
if (revision.Length != 40 || Environment.Version.ToString() != "10.0.11") throw new InvalidOperationException("Pinned runtime/revision required");
const string ns = "http://purl.oclc.org/dsdl/schematron";
static SchematronSchema Compile(string body) => SchematronSchema.Compile("<schema xmlns='" + ns + "'>" + body + "</schema>");
const string xml = "<root><item code='x'><child/></item><item code='y'/></root>";
var document = XmlDocument.Parse(xml);
var simple = Compile("<pattern><rule context='item'><assert test='@code'/></rule></pattern>");
var assertion = Compile("<pattern><rule context='item'><assert test='false()'>missing <name/></assert></rule></pattern>");
var report = Compile("<let name='x' value='1'/><phase id='p'><let name='v' value='2'/><active pattern='a'/></phase>" +
    "<pattern id='a'><let name='x' value='$x+1'/><rule context='item'><let name='x' value='$x+1'/><report test='$v=2' subject='@code' diagnostics='d' properties='prop'>" +
    "<name/>:<value-of select='$x'/></report></rule></pattern><diagnostics><diagnostic id='d'>D<value-of select='$x'/></diagnostic></diagnostics><properties><property id='prop'>P<name/></property></properties>");
var longMessage = Compile("<pattern><rule context='item'><report test='true()'>" + new string('界', 4096) + "😀</report></rule></pattern>");
var selected = new SchematronValidationOptions { Phase = "p" };
var stopped = new SchematronValidationOptions { Validation = new() { MaxOperations = 1 } };
var expression = XPathExpression.Compile("//item");
var rows = new List<object>();
long coldBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
var coldResult = simple.Validate(document);
long cold = measure ? GC.GetAllocatedBytesForCurrentThread() - coldBefore : 0;
if (!coldResult.Validation.IsValid || coldResult.FiredRules.Count != 2) throw new Exception("cold result");
rows.Add(new { name = "cold-precompiled-document-validation", bytes = measure ? (long?)cold : null, events = coldResult.FiredRules.Count });
byte[]? positive = null;
Probe("positive-owned-control", () => { positive = new byte[128]; positive[0] = 1; return positive[0]; });
Probe("m4-selector-owned-snapshot", () => expression.Evaluate(document.DocumentNode).AsNodeSet().Count == 2 ? 1 : 0);
Probe("valid-owned-events-result", () => { var result = simple.Validate(document); return result.Validation.IsValid && result.Findings.Count == 0 && result.FiredRules.Count == 2 ? 1 : 0; });
Probe("failed-assert-owned-findings", () => { var result = assertion.Validate(document); return result.Validation.IsComplete && !result.Validation.IsValid && result.Findings.Count == 2 ? 1 : 0; });
Probe("phase-lets-subject-diagnostics-properties", () => { var result = report.Validate(document, selected); return result.Validation.IsValid && result.Findings.Count == 2 && result.Findings[0].Text == "item:3" && result.Findings[0].Diagnostics[0].Text == "D3" && result.Findings[0].Properties[0].Text == "Pitem" ? 1 : 0; });
Probe("owned-long-multilingual-message", () => { var result = longMessage.Validate(document); return result.Validation.IsValid && result.Findings.Count == 2 && result.Findings[0].Text.Length == 4098 ? 1 : 0; });
Probe("strict-string-parse-and-validation", () => simple.Validate(xml).Validation.IsValid ? 1 : 0);
Probe("owned-syntax-diagnostic", () => { var result = simple.Validate("<root>"); return !result.Validation.IsComplete && result.Validation.Diagnostics[0].Code == "SCHEMA.Syntax" ? 1 : 0; });
Probe("owned-budget-terminal", () => { var result = simple.Validate(document, stopped); return !result.Validation.IsComplete && result.Validation.Diagnostics[0].Code == "VALIDATION.Limit" ? 1 : 0; });
long retainedBefore = measure ? GC.GetTotalMemory(true) : 0;
var retained = new SchematronResult[16];
for (int i = 0; i < retained.Length; i++) retained[i] = report.Validate(document, selected);
long retainedAfter = measure ? GC.GetTotalMemory(true) : 0;
GC.KeepAlive(retained);
GC.KeepAlive(positive);
GC.KeepAlive(coldResult);
File.WriteAllText(args[1], JsonSerializer.Serialize(new
{
    mode = args[0],
    sourceRevision = revision,
    runtime = Environment.Version.ToString(),
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    tiered = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
    serverGc = System.Runtime.GCSettings.IsServerGC,
    rows,
    retainedResultCount = retained.Length,
    retainedApproximateBytes = measure ? (long?)(retainedAfter - retainedBefore) : null,
    processWorkingSet = measure ? (long?)Environment.WorkingSet : null,
    note = "Compiled schema/input setup is outside counters. M4 selector provides an independent owned-snapshot control, not a subtractive allocation oracle. Runs own variable maps, matching/ordering scratch, XPath snapshots, location/message strings, events/findings/diagnostics and detached results. No borrowed-zero, timing, speedup, BDN or cross-architecture claim; retained estimate and RSS are process-local."
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS: " + rows.Count + " consumed routes; " + (measure ? "allocation measurement" : "counters disabled"));
return 0;
void Probe(string label, Func<int> action)
{
    for (int i = 0; i < 16; i++)
        if (action() != 1)
            throw new Exception(label + " warm");
    long before = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
    int consumed = 0;
    for (int i = 0; i < 32; i++)
        consumed += action();
    long bytes = measure ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
    if (consumed != 32 || measure && bytes <= 0)
        throw new Exception(label + " result/allocation control");
    rows.Add(new
    {
        name = label,
        iterations = 32,
        consumed,
        bytes = measure ? (long?)bytes : null,
        bytesPerOperation = measure ? (double?)bytes / 32 : null
    });
}

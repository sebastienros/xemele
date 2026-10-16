using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele;
using Xemele.Validation;

Console.WriteLine($"FrameworkDescription={RuntimeInformation.FrameworkDescription}; Architecture={RuntimeInformation.ProcessArchitecture}");
if (RuntimeInformation.FrameworkDescription != ".NET 10.0.11") throw new InvalidOperationException("Pinned actual runtime required.");
if (args is not [var mode] || mode is not ("--control" or "--measure")) throw new ArgumentException();
bool measuring = mode == "--measure";
long fixtureBefore = measuring ? GC.GetAllocatedBytesForCurrentThread() : 0;
const string Dtd = "<!ELEMENT r (a*)><!ELEMENT a EMPTY><!ATTLIST a id ID #REQUIRED refs IDREFS #IMPLIED kind (one|two) 'one'>";
var schema = DtdSchema.Compile(Dtd);
var valid = XmlDocument.Parse("<r><a id='first' refs='second'/><a id='second'/></r>");
var invalid = XmlDocument.Parse("<r><a id='first' refs='missing' kind='three'/><a id='first'/></r>");
var quota = new ValidationOptions { MaxOperations = 1 };
var success = schema.Validate(valid);
var failure = schema.Validate(invalid);
var refused = schema.Validate(valid, quota);
if (!success.IsValid || !failure.IsComplete || failure.IsValid || refused.IsComplete) throw new InvalidOperationException("Fixture control failed.");
Console.WriteLine("Demonstration=valid forward IDREF; complete invalid type/duplicate ID; incomplete work limit; no DOM defaults added");
Span<char> scratch = stackalloc char[64];
var operation = new ValidationOperation(new() { MaxOperations = null, Limits = new() { EvaluationWork = null } });
var names = new DtdEvaluator.QNameBuffer(scratch, operation);
string[] prefixes = ["", "p", "名", new string('p', 512)];
string[] locals = ["short", "short", "β😀", new string('x', 512)];
long consumed = 0;
try
{
    for (int i = 0; i < prefixes.Length; i++)
        consumed += names.Make(prefixes[i], locals[i]).Length;
    if (!measuring)
        return;
    long fixtureBytes = GC.GetAllocatedBytesForCurrentThread() - fixtureBefore;
    Print("fixture-initialization", fixtureBytes, 1);
    long positiveBefore = GC.GetAllocatedBytesForCurrentThread();
    var positive = new byte[1024];
    long positiveBytes = GC.GetAllocatedBytesForCurrentThread() - positiveBefore;
    Print("positive-control", positiveBytes, 1);
    if (positiveBytes <= 0)
        throw new InvalidOperationException();
    GC.KeepAlive(positive);
    for (int scenario = 0; scenario < prefixes.Length; scenario++)
    {
        for (int i = 0; i < 10000; i++)
            consumed += names.Make(prefixes[scenario], locals[scenario]).Length;
        int count = scenario == 3 ? 10000 : 100000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < count; i++)
            consumed += names.Make(prefixes[scenario], locals[scenario]).Length;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Print("warm-borrowed-qname-" + scenario, bytes, count);
        if (bytes != 0)
            throw new InvalidOperationException("QName allocation regression.");
    }
    const int OwnedCount = 100;
    object? sink = null;
    for (int domain = 0; domain < 4; domain++)
    {
        for (int i = 0; i < 100; i++)
            Apply(domain);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < OwnedCount; i++)
            Apply(domain);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Print(new[] { "owned-valid-id-index", "owned-invalid-diagnostics", "owned-quota-diagnostic", "owned-schema-compilation" }[domain], bytes, OwnedCount);
        if (bytes <= 0)
            throw new InvalidOperationException("Owned allocation domain should allocate.");
    }
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long heapBefore = GC.GetTotalMemory(false);
    var retained = new ValidationResult[100];
    for (int i = 0; i < retained.Length; i++)
        retained[i] = schema.Validate(invalid);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long heapAfter = GC.GetTotalMemory(false);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        domain = "retained-whole-heap-results",
        beforeBytes = heapBefore,
        afterBytes = heapAfter,
        inclusiveDeltaBytes = heapAfter - heapBefore,
        denominatorResults = retained.Length
    }));
    GC.KeepAlive(retained);
    GC.KeepAlive(sink);
    void Apply(int domain) => sink = domain switch
    {
        0 => schema.Validate(valid),
        1 => schema.Validate(invalid),
        2 => schema.Validate(valid, quota),
        _ => DtdSchema.Compile(Dtd)
    };
}
finally { names.Dispose(); GC.KeepAlive(consumed); }
static void Print(string domain, long numeratorBytes, int denominatorOperations) => Console.WriteLine(JsonSerializer.Serialize(new { domain, numeratorBytes, denominatorOperations }));

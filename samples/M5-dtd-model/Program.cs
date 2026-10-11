using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele.Parsing.Dtd;
using Xemele.Validation;

Console.WriteLine($"FrameworkDescription={RuntimeInformation.FrameworkDescription}; Architecture={RuntimeInformation.ProcessArchitecture}");
if (RuntimeInformation.FrameworkDescription != ".NET 10.0.11") throw new InvalidOperationException("Explicit pinned runtime required.");
using var parser = new DtdParser(DtdSubsetKind.External, new(), new Uri("urn:sample"));
var parsed = parser.Append("<!NOTATION n SYSTEM 'n'><!ENTITY e SYSTEM 'e' NDATA n><!ELEMENT r (名*,last)><!ELEMENT 名 EMPTY><!ELEMENT last EMPTY><!ATTLIST r kind (one|two) 'one' refs IDREFS #IMPLIED>", [], true, default);
if (parsed.Step != DtdParseStep.Completed) throw new InvalidOperationException();
var source = parser.Model;
var compiled = DtdCompiler.Compile(source, new());
var operation = new ValidationOperation(new() { MaxOperations = null, Limits = new() { EvaluationWork = null } });
var content = compiled.Elements[0].Content;
int state = 0;
if (!content.TryAdvance(ref state, "名", operation) || !content.TryAdvance(ref state, "last", operation) || !content.IsAccepting(state)) throw new InvalidOperationException();
if (compiled.Attributes[0].NormalizedDefault != "one" || !compiled.HasUnparsedEntity("e", operation)) throw new InvalidOperationException();
Console.WriteLine("Demonstration=accepted 名,last; normalized default=one; unparsed entity=e; immutable schema reuse");
if (args is ["--control"]) return;
if (args is not ["--measure"]) throw new ArgumentException("Use --control or --measure.");
const int Count = 100_000;
object? sink = null; bool answer = false;
Action[] actions = [
    () => { int cursor = 0; answer = content.TryAdvance(ref cursor, "名", operation); },
    () => { int cursor = 0; answer = content.TryAdvance(ref cursor, "missing", operation); },
    () => sink = compiled.FindElement("_last_".AsSpan(1, 4), operation),
    () => sink = compiled.FindElement("Last", operation),
    () => sink = compiled.FindAttribute("r", "kind", operation),
    () => answer = compiled.Attributes[0].IsValueValid("two", compiled, operation),
    () => answer = compiled.Attributes[0].IsValueValid("TWO", compiled, operation),
    () => answer = compiled.Attributes[1].IsValueValid("one 名", compiled, operation),
    () => answer = compiled.HasUnparsedEntity("e", operation),
    () => answer = compiled.HasNotation("n", operation)
];
string[] labels = ["transition-hit", "transition-miss", "element-hit", "element-miss", "attribute-lookup", "enum-hit", "enum-miss", "list-value", "entity-hit", "notation-hit"];
for (int i = 0; i < actions.Length; i++)
{
    var action = actions[i]; for (int j = 0; j < 10_000; j++) action();
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int j = 0; j < Count; j++) action();
    long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine(JsonSerializer.Serialize(new { domain = "warm-borrowed", scenario = labels[i], numeratorBytes = bytes, denominatorOperations = Count }));
    if (bytes != 0) throw new InvalidOperationException("Borrowed allocation regression: " + labels[i]);
}
const int ColdCount = 100;
long coldBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < ColdCount; i++) sink = DtdCompiler.Compile(source, new());
long coldBytes = GC.GetAllocatedBytesForCurrentThread() - coldBefore;
Console.WriteLine(JsonSerializer.Serialize(new { domain = "owned-compilation", numeratorBytes = coldBytes, denominatorOperations = ColdCount }));
// Whole heap retention is approximate, inclusive, and disclosed separately from allocation.
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long heapBefore = GC.GetTotalMemory(false);
var retained = new CompiledDtdModel[100];
for (int i = 0; i < retained.Length; i++) retained[i] = DtdCompiler.Compile(source, new());
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long heapAfter = GC.GetTotalMemory(false);
Console.WriteLine(JsonSerializer.Serialize(new { domain = "retained-whole-heap", beforeBytes = heapBefore, afterBytes = heapAfter, inclusiveDeltaBytes = heapAfter - heapBefore, denominatorSchemas = retained.Length }));
GC.KeepAlive(retained); GC.KeepAlive(sink); GC.KeepAlive(answer);

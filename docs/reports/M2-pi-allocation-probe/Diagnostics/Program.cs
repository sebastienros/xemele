using System.Runtime.CompilerServices;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Syntax;

// Diagnostic replication of the PI test's workload; does not change test execution
// or substitute its independently run exact-zero assertion. Each mode starts fresh.
bool original = args.Length == 1 && args[0] == "original";
if (args.Length != 1 || (!original && args[0] != "equivalent")) throw new ArgumentException("original|equivalent");
const string chunk = "<?π.x-1 data?><?xml-stylesheet?><?\U00010000?>";
var cancellation = CancellationToken.None;
long before = GC.GetAllocatedBytesForCurrentThread();
using var core = new XmlParserCore(new() { Limits = new() { DocumentCharacters = null } });
var sink = new CountingSink();
long setup = GC.GetAllocatedBytesForCurrentThread() - before;
before = GC.GetAllocatedBytesForCurrentThread();
var root = core.Append("<r>", [], false, sink, cancellation);
long rootBytes = GC.GetAllocatedBytesForCurrentThread() - before;
Check(root, 3, ScanStep.NeedInput);
before = GC.GetAllocatedBytesForCurrentThread();
if (original) core.Append(chunk, [], false, sink, cancellation);
else AppendChecked(core, chunk, sink, cancellation, 1);
long firstUse = GC.GetAllocatedBytesForCurrentThread() - before;
before = GC.GetAllocatedBytesForCurrentThread();
if (original)
    for (int i = 0; i < 99; i++) core.Append(chunk, [], false, sink, cancellation);
else AppendChecked(core, chunk, sink, cancellation, 99);
long warmRemainder = GC.GetAllocatedBytesForCurrentThread() - before;
long warmCalls = sink.Calls, warmUnits = sink.Units;
before = GC.GetAllocatedBytesForCurrentThread();
if (original)
{
    for (int i = 0; i < 10000; i++)
        if (core.Append(chunk, [], false, sink, cancellation).Step != ScanStep.NeedInput) throw new InvalidOperationException();
}
else AppendChecked(core, chunk, sink, cancellation, 10000);
long steady = GC.GetAllocatedBytesForCurrentThread() - before;
long measuredCalls = sink.Calls - warmCalls, measuredUnits = sink.Units - warmUnits;
before = GC.GetAllocatedBytesForCurrentThread();
var completion = core.Append("</r>", [], true, sink, cancellation);
long completionBytes = GC.GetAllocatedBytesForCurrentThread() - before;
Check(completion, 4, ScanStep.Completed);
Console.WriteLine(JsonSerializer.Serialize(new {
    mode = args[0], runtime = Environment.Version.ToString(), warmIterations = 100, measuredIterations = 10000,
    coldSetupBytes = setup, rootFirstUseBytes = rootBytes, firstPiBytes = firstUse,
    remaining99WarmBytes = warmRemainder, steadyBytes = steady, completionBytes,
    warmCalls, warmUnits, measuredCalls, measuredUnits, completed = completion.Step.ToString(), consumedCompletion = completion.Consumed,
    runtimeSettings = "default; no tiering, JIT, GC or test parallelism overrides",
    scope = "Synchronous borrowed PI primitive; counter/validation/output outside measured region; setup includes options/core/sink, not process startup; no retained-memory estimate or timing"
}));
if (warmCalls != 1003 || warmUnits != 400 || measuredCalls != 100000 || measuredUnits != 40000 || sink.Calls != 101005 || sink.Units != 40400)
    throw new InvalidOperationException("PI callback/text progress changed.");
if (!original && steady != 0) return 1;
return 0;

[MethodImpl(MethodImplOptions.NoInlining)]
static void AppendChecked(XmlParserCore core, string chunk, CountingSink sink, CancellationToken cancellation, int iterations)
{
    for (int i = 0; i < iterations; i++)
    {
        var result = core.Append(chunk, [], false, sink, cancellation);
        if (result.Step != ScanStep.NeedInput || result.Consumed != chunk.Length || result.Diagnostic is not null)
            throw new InvalidOperationException("PI append did not consume the complete chunk.");
    }
}
static void Check(ScanResult result, int consumed, ScanStep step)
{
    if (result.Step != step || result.Consumed != consumed || result.Diagnostic is not null)
        throw new InvalidOperationException("Root/completion progress changed.");
}
sealed class CountingSink : IXmlEventSink
{
    internal long Units, Calls;
    private bool Count() { Calls++; return true; }
    public bool StartDocument(XmlLocation l) => Count();
    public bool Declaration(XmlDeclarationView d) => Count();
    public bool StartElement(XmlNameView n, bool empty, XmlLocation l) => Count();
    public bool NamespaceDeclaration(XmlNamespaceView n) => Count();
    public bool Attribute(XmlAttributeView a) => Count();
    public bool StartElementContent() => Count();
    public bool EndElement(XmlNameView n, XmlLocation l) => Count();
    public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l) => Count();
    public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) { Units += t.Length; return Count(); }
    public bool EndDocument(XmlLocation l) => Count();
}

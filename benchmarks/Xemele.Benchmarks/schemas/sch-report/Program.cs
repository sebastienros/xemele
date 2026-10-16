using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xemele;
using Xemele.Validation;

if (args.Length != 2 || args[0] is not ("--control" or "--measure")) return 2;
bool measure = args[0] == "--measure";
string revision = Environment.GetEnvironmentVariable("XEMELE_SCH_REPORT_REVISION") ?? throw new InvalidOperationException("source required");
if (revision.Length != 40 || Environment.Version.ToString() != "10.0.11") throw new InvalidOperationException("pinned runtime/revision required");
static SchematronResult Result(string text, int count = 1)
{
    var events = new List<SchematronFiredRule>();
    var findings = new List<SchematronFinding>();
    for (int i = 0; i < count; i++)
    {
        events.Add(new(i, 0, 0, "item", "focus"));
        findings.Add(new(SchematronFindingKind.SuccessfulReport, "id", "", "f", 0, i, null, "item", "true()", "subject", default, "focus", default, text, [new("d", "diagnostic")], [new("p", "property")]));
    }
    return new(new ValidationResult(true, []), "#ALL", "Title", new Dictionary<string, string> { { "z", "urn:z" }, { "a", "urn:a" } }, [new(0, null, null), new(1, "empty", null)], events, findings);
}
var simple = Result("found");
var longText = Result(new string('界', 8192) + "😀<&\"\r]]>");
var wide = Result("<😀>&\r", 128);
var escaped = Result(string.Concat(Enumerable.Repeat("<&\"'\r\n\t]]>😀", 512)));
var invalid = Result(new string((char)0xd800, 1));
var rows = new List<object>();
long coldBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
string cold = SchematronReport.SerializeSvrl(simple);
long coldBytes = measure ? GC.GetAllocatedBytesForCurrentThread() - coldBefore : 0;
if (!cold.Contains("<svrl:successful-report", StringComparison.Ordinal)) throw new Exception("cold");
rows.Add(new { name = "cold-owned-string", bytes = measure ? (long?)coldBytes : null, characters = cold.Length });
var sink = new DigestStream();
long streamBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
SchematronReport.SaveSvrl(simple, sink);
long streamBytes = measure ? GC.GetAllocatedBytesForCurrentThread() - streamBefore : 0;
var simpleBytes = Encoding.UTF8.GetBytes(cold);
if (sink.Count != simpleBytes.Length || sink.Hash != DigestStream.HashOf(simpleBytes)) throw new Exception("cold stream");
rows.Add(new { name = "cold-utf8-output", bytes = measure ? (long?)streamBytes : null, outputBytes = sink.Count });
byte[]? positive = null;
Probe("positive-owned-control", () => { positive = new byte[128]; positive[0] = 1; return positive[0]; });
foreach (var pair in new[] { ("short", simple), ("multilingual-long", longText), ("dense-escaping", escaped), ("128-events", wide) })
{
    string expected = SchematronReport.SerializeSvrl(pair.Item2);
    byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
    ulong hash = DigestStream.HashOf(expectedBytes);
    Probe(pair.Item1 + "-owned-string", () => string.Equals(expected, SchematronReport.SerializeSvrl(pair.Item2), StringComparison.Ordinal) ? 1 : 0);
    Probe(pair.Item1 + "-utf8-output", () => { sink.Reset(); SchematronReport.SaveSvrl(pair.Item2, sink); return sink.Count == expectedBytes.Length && sink.Hash == hash ? 1 : 0; });
}
Probe("invalid-scalar-string", () => { try { _ = SchematronReport.SerializeSvrl(invalid); return 0; } catch (ArgumentException) { return 1; } });
Probe("invalid-scalar-stream", () => { sink.Reset(); try { SchematronReport.SaveSvrl(invalid, sink); return 0; } catch (ArgumentException) { return 1; } });
long retainedBefore = measure ? GC.GetTotalMemory(true) : 0;
var retained = new string[16];
for (int i = 0; i < retained.Length; i++) retained[i] = SchematronReport.SerializeSvrl(longText);
long retainedAfter = measure ? GC.GetTotalMemory(true) : 0;
GC.KeepAlive(retained);
GC.KeepAlive(positive);
GC.KeepAlive(cold);
GC.KeepAlive(sink);
File.WriteAllText(args[1], JsonSerializer.Serialize(new
{
    mode = args[0],
    sourceRevision = revision,
    runtime = Environment.Version.ToString(),
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    tiered = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
    serverGc = System.Runtime.GCSettings.IsServerGC,
    rows,
    retainedResultCount = 16,
    retainedApproximateBytes = measure ? (long?)(retainedAfter - retainedBefore) : null,
    processWorkingSet = measure ? (long?)Environment.WorkingSet : null,
    note = "Detached fixture setup and expected XML/UTF8 bytes/digest are outside counters. String routes own character builder chunks/final string; stream routes own encoder and bounded pooled bytes (pool growth cold). Both own key grouping dictionaries/lists and sorted namespace nodes. Digest stream is precreated/reused with no retained output; destination storage allocation is excluded. Invalid XML routes own exceptions. All results consumed. No zero allocation, timing, speedup, BDN or cross-architecture claim. Retained estimate and RSS are approximate process-local counters."
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS: " + rows.Count + " consumed routes; " + (measure ? "allocation measurement" : "counters disabled"));
return 0;
void Probe(string name, Func<int> action)
{
    for (int i = 0; i < 16; i++)
        if (action() != 1)
            throw new Exception(name + " warm");
    long before = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
    int consumed = 0;
    for (int i = 0; i < 32; i++)
        consumed += action();
    long total = measure ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
    if (consumed != 32 || measure && total <= 0)
        throw new Exception(name + " control");
    rows.Add(new
    {
        name,
        iterations = 32,
        consumed,
        bytes = measure ? (long?)total : null,
        bytesPerOperation = measure ? (double?)total / 32 : null
    });
}
sealed class DigestStream : Stream
{
    internal long Count; internal ulong Hash = 14695981039346656037;
    internal void Reset()
    {
        Count = 0;
        Hash = 14695981039346656037;
    }
    internal static ulong HashOf(ReadOnlySpan<byte> bytes)
    {
        ulong hash = 14695981039346656037;
        foreach (byte value in bytes)
            hash = unchecked((hash ^ value) * 1099511628211);
        return hash;
    }
    public override void Write(ReadOnlySpan<byte> bytes)
    {
        Count = checked(Count + bytes.Length);
        foreach (byte value in bytes)
            Hash = unchecked((Hash ^ value) * 1099511628211);
    }
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override bool CanRead => false; public override bool CanWrite => true; public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException(); public override long Position
    {
        get => throw new NotSupportedException(); set => throw new NotSupportedException();
    }
    public override void Flush()
    {
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
}

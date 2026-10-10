using System.Text.Json;

// Compiler control of actual acceptance/reporting code. No parser or GC counters.
int checks = 0;
void Require(bool value) { if (!value) throw new Exception("Acceptance compiler control failed."); checks++; }
for (int i = 0; i < 24; i++)
foreach (string corpus in new[] { "long", "ordinary" })
{
    var warm = new ProbeEvidence(corpus, false, true, false, i) { CoreBytes = 999, DigitBytes = 998, OwnedBytes = 997 };
    warm.Check(true, "functional-warmup"); warm.AllocationChecks(); Require(!warm.Reported);
}
foreach (string corpus in new[] { "long", "ordinary" })
foreach (bool positive in new[] { false, true })
{
    long core = positive ? 64 : 0, owned = corpus == "long" ? 120064 : positive ? 96 : 32;
    var exact = new ProbeEvidence(corpus, positive, true, true, null) { CoreBytes = core, DigitBytes = core, OwnedBytes = owned };
    exact.AllocationChecks(); Require(!exact.Reported);
    foreach (string boundary in corpus == "long" ? new[] { "core", "digit", "owned" } : new[] { "core", "owned" })
    {
        var bad = new ProbeEvidence(corpus, positive, true, true, null) { CoreBytes = core, DigitBytes = core, OwnedBytes = owned, Consumed = 10003, Callbacks = 6, Outcome = 2 };
        if (boundary == "core") bad.CoreBytes++;
        if (boundary == "digit") bad.DigitBytes++;
        if (boundary == "owned") bad.OwnedBytes++;
        using var output = new StringWriter(); var console = Console.Out; Console.SetOut(output);
        try { bad.AllocationChecks(); throw new Exception("Tampered allocation accepted."); }
        catch (InvalidOperationException error) { bad.Report(error); }
        finally { Console.SetOut(console); }
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries); Require(lines.Length == 1);
        using var record = JsonDocument.Parse(lines[0]); var value = record.RootElement;
        Require(value.GetProperty("Boundary").GetString() == boundary + "-allocation");
        Require(value.GetProperty("Consumed").GetInt32() == 10003 && value.GetProperty("Callbacks").GetInt32() == 6 && value.GetProperty("Outcome").GetInt32() == 2);
        Require(value.GetProperty("CoreBytes").GetInt64() == bad.CoreBytes && value.GetProperty("OwnedBytes").GetInt64() == bad.OwnedBytes);
    }
}
// Allocation enforcement is disabled here, but a functional failure still reports and terminates.
var functional = new ProbeEvidence("long", false, true, false, 7) { CoreBytes = 999, Consumed = 17, Callbacks = 2, Outcome = 3, IntervalActive = true };
using (var output = new StringWriter())
{
    var console = Console.Out; Console.SetOut(output);
    try { functional.Check(false, "functional-warmup"); throw new Exception("Functional failure accepted."); }
    catch (InvalidOperationException) { }
    finally { Console.SetOut(console); }
    using var record = JsonDocument.Parse(output.ToString());
    Require(record.RootElement.GetProperty("IntervalActive").GetBoolean());
    Require(record.RootElement.GetProperty("WarmIndex").GetInt32() == 7 && record.RootElement.GetProperty("Phase").GetString() == "warmup" && record.RootElement.GetProperty("CoreBytes").GetInt64() == 999);
}
Console.WriteLine($"Passed {checks} compiler controls; 24 warm pairs, strict real/positive totals, functional failures and structured failure capture; no counter reads.");

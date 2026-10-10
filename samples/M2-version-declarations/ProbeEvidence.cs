using System.Text.Json;

// Acceptance and failure reporting run outside every counter interval.
internal sealed class ProbeEvidence(string corpus, bool positive, bool count, bool enforceAllocations, int? warmIndex)
{
    internal string Boundary = "setup";
    internal long? CoreBytes, DigitBytes, OwnedBytes;
    internal int Consumed, Callbacks, Outcome;
    internal bool Reported, IntervalActive;
    internal void Check(bool value, string boundary)
    {
        Boundary = boundary;
        if (value) return;
        var error = new InvalidOperationException("Version probe oracle failed: " + boundary);
        Report(error);
        throw error;
    }
    internal void AllocationChecks()
    {
        if (!count || !enforceAllocations) return;
        Check(CoreBytes == (positive ? 64 : 0), "core-allocation");
        if (corpus == "long") Check(DigitBytes == (positive ? 64 : 0), "digit-allocation");
        Check(OwnedBytes == (corpus == "long" ? 120064 : positive ? 96 : 32), "owned-allocation");
    }
    internal void Report(Exception error)
    {
        if (Reported) return;
        Reported = true;
        Console.WriteLine(JsonSerializer.Serialize(new { Kind = "probe-failure", Corpus = corpus, Positive = positive, Count = count, EnforceAllocations = enforceAllocations, WarmIndex = warmIndex, Phase = warmIndex is null ? "observation" : "warmup", Boundary, IntervalActive, CounterStatus = IntervalActive ? "workload threw before closing counter read; unavailable counter is null" : "closed intervals or not yet started", CoreBytes, DigitBytes, OwnedBytes, Consumed, Callbacks, Outcome, Failure = new { Type = error.GetType().Name, error.Message }, ProcessId = Environment.ProcessId }));
    }
}

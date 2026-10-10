using System.Runtime.CompilerServices;
using Xemele.Parsing.Syntax;

namespace Xemele.Tests.Syntax;

internal readonly record struct StreamingAllocationSample(int Iteration, long AppendBytes,
    long ValidationBytes, long ObservationBytes, long GapBytes, int Consumed,
    ScanStep Step, long Units, long Calls, int ThreadId);

internal static class StreamingAllocationProbe
{
    // A synchronous borrowed primitive scope. Setup, test context, assertions and
    // caller dispatch stay outside both counters; both phases use this call site.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long AppendChecked<TSink>(XmlParserCore core, string chunk,
        TSink sink, CancellationToken cancellation, int iterations,
        Span<StreamingAllocationSample> samples = default,
        Func<(long Units, long Calls)>? progress = null)
        where TSink : class, IXmlEventSink
    {
        if (!samples.IsEmpty && (samples.Length != iterations || progress is null))
            throw new ArgumentException("Preallocated samples and progress observer must match iterations.");
        long before = GC.GetAllocatedBytesForCurrentThread();
        long last = before;
        for (int i = 0; i < iterations; i++)
        {
            long entry = samples.IsEmpty ? 0 : GC.GetAllocatedBytesForCurrentThread();
            var r = core.Append(chunk, [], false, sink, cancellation);
            long appended = samples.IsEmpty ? 0 : GC.GetAllocatedBytesForCurrentThread();
            if (r.Step != ScanStep.NeedInput || r.Consumed != chunk.Length || r.Diagnostic is not null)
                throw new Exception("Warm grammar rejected.");
            if (!samples.IsEmpty)
            {
                long validated = GC.GetAllocatedBytesForCurrentThread();
                var current = progress!();
                long observed = GC.GetAllocatedBytesForCurrentThread();
                samples[i] = new(i, appended - entry, validated - appended,
                    observed - validated, entry - last, r.Consumed, r.Step,
                    current.Units, current.Calls, Environment.CurrentManagedThreadId);
                last = observed;
            }
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

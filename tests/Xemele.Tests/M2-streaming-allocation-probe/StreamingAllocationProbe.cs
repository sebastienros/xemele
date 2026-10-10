using System.Runtime.CompilerServices;
using Xemele.Parsing.Syntax;

namespace Xemele.Tests.Syntax;

internal static class StreamingAllocationProbe
{
    // A synchronous borrowed primitive scope. Setup, test context, assertions and
    // caller dispatch stay outside both counters; both phases use this call site.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long AppendChecked<TSink>(XmlParserCore core, string chunk,
        TSink sink, CancellationToken cancellation, int iterations)
        where TSink : class, IXmlEventSink
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            var r = core.Append(chunk, [], false, sink, cancellation);
            if (r.Step != ScanStep.NeedInput || r.Consumed != chunk.Length || r.Diagnostic is not null)
                throw new Exception("Warm grammar rejected.");
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

using System.Runtime.CompilerServices;
using Xemele.Parsing.Syntax;

namespace Xemele.Tests.PiTargets;

internal static class PiAllocationProbe
{
    // The entire synchronous checked primitive loop lies between these reads.
    // Caller/setup/assertions/output/completion remain outside; no subtraction.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long AppendChecked<TSink>(XmlParserCore core, string chunk,
        TSink sink, CancellationToken cancellation, int iterations)
        where TSink : class, IXmlEventSink
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            var result = core.Append(chunk, [], false, sink, cancellation);
            if (result.Step != ScanStep.NeedInput || result.Consumed != chunk.Length || result.Diagnostic is not null)
                throw new InvalidOperationException("PI append did not consume the complete chunk.");
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

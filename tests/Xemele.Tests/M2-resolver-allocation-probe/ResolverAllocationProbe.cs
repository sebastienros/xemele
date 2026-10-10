using System.Runtime.CompilerServices;
using Xemele.Resolution.Core;

namespace Xemele.Tests.Resolution;

internal static class ResolverAllocationProbe
{
    private static byte[]? escaped;
    internal static int EscapedLength => escaped?.Length ?? 0;

    // Both counters enclose the entire synchronous loop. Setup, disposal, caller
    // dispatch and reporting are outside; no subtraction or changed warm profile.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long ReadChecked(XmlResolutionSession session, XmlResourceHandle handle,
        byte[] buffer, XmlLocation location, CancellationToken cancellation, int iterations,
        bool positive = false)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            long previous = session.BytesRead;
            buffer.AsSpan().Fill(0xA5);
            var result = handle.Read(buffer, location);
            if (result.Count != 4096 || result.Diagnostic is not null || session.BytesRead != previous + 4096)
                throw new InvalidOperationException("Resolver read must make full nonterminal byte progress.");
            for (int j = 0; j < buffer.Length; j++)
                if (buffer[j] != 0) throw new InvalidOperationException("Borrowed buffer was not cleared by ZeroStream.");
            if (positive) escaped = new byte[37];
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

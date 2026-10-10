using System.Runtime.CompilerServices;
using Xemele.Parsing.Dtd;

namespace Xemele.Tests;

internal static class FramingAllocationProbe
{
    internal const string Spaces = "                                ";
    internal const string Completion = "encoding='X'?>";

    // Both counters and the entire checked synchronous Append loop share one
    // non-inlined frame. Setup/context lookup/completion/output remain outside.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (long Bytes, long Consumed, XmlLocation Cursor) AppendChecked(
        XmlTextDeclarationFramer framer, CancellationToken cancellation, bool positive, ref object? escaped)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        long consumed = 0;
        XmlLocation cursor = default;
        for (int i = 0; i < 1000; i++)
        {
            var result = framer.Append(Spaces, [], false, cancellation);
            if (result.Consumed != 32 || result.Step != TextDeclarationStep.NeedInput
                || result.Diagnostic is not null || result.Declaration is not null)
                throw new InvalidOperationException("Framing Append did not consume all 32 whitespace units.");
            consumed += result.Consumed;
            cursor = result.NextLocation;
            if (positive) escaped = new byte[37];
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return (bytes, consumed, cursor);
    }
}

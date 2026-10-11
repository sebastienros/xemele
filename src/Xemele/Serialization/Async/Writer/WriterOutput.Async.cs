namespace Xemele;

internal sealed partial class WriterOutput
{
    // Strings are immutable owners. Caller memory is retained only until completion.
    internal ValueTask WriteAsync(string text, CancellationToken call) => WriteAsync(text.AsMemory(), call);
    internal async ValueTask WriteAsync(ReadOnlyMemory<char> text, CancellationToken call)
    {
        int offset = 0;
        while (offset < text.Length)
        {
            call.ThrowIfCancellationRequested();
            if (Capacity - Buffered < 6148) await DrainAsync(call).ConfigureAwait(false);
            int used = Encode(text.Span.Slice(offset, Math.Min(text.Length - offset, 2048)));
            offset += used;
            if (used == 0) await DrainAsync(call).ConfigureAwait(false);
        }
    }
    internal async ValueTask PreambleAsync(CancellationToken call)
    {
        call.ThrowIfCancellationRequested();
        // Stage static preamble bytes in our rented owner before the await.
        ReadOnlySpan<byte> preamble = CodePage switch
        { 65001 => [0xef, 0xbb, 0xbf], 1200 => [0xff, 0xfe], _ => [0xfe, 0xff] };
        preamble.CopyTo(Bytes); Buffered = preamble.Length;
        await DrainAsync(call).ConfigureAwait(false);
    }
    internal async ValueTask DrainAsync(CancellationToken call)
    {
        if (Buffered == 0) return;
        call.ThrowIfCancellationRequested();
        await Stream.WriteAsync(BufferedMemory, call).ConfigureAwait(false);
        ConsumeBuffer();
    }
    internal async ValueTask CompleteAsync(CancellationToken call)
    {
        call.ThrowIfCancellationRequested();
        if (Capacity - Buffered < 4) await DrainAsync(call).ConfigureAwait(false);
        Encode([], final: true); await DrainAsync(call).ConfigureAwait(false);
    }
}

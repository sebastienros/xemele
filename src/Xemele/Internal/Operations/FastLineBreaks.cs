using System.Buffers;

namespace Xemele.Internal.Operations;

// Line-break normalization for the optimistic parsers: CRLF and lone CR become LF, which is exactly how the
// streaming parser reads them (and counts them, as one line break). Line and column numbers therefore carry
// over unchanged; only byte offsets need mapping back, which Raw does from the recorded collapsed pairs.
internal static class FastLineBreaks
{
    // Writes the normalized text into destination (at least source.Length bytes) and returns its length.
    // With trackOffsets, dropped receives (ascending, pooled) the output index of every LF whose CR was folded in.
    internal static int Normalize(ReadOnlySpan<byte> source, Span<byte> destination, bool trackOffsets, ref int[]? dropped, out int droppedCount)
    {
        int read = 0, written = 0, count = 0;
        while (read < source.Length)
        {
            int at = source[read..].IndexOf((byte)'\r');
            if (at < 0) { source[read..].CopyTo(destination[written..]); written += source.Length - read; break; }
            source.Slice(read, at).CopyTo(destination[written..]);
            written += at; read += at;
            destination[written++] = (byte)'\n';
            read++;
            if (read < source.Length && source[read] == '\n')
            {
                read++;
                if (trackOffsets)
                {
                    if (dropped is null) dropped = ArrayPool<int>.Shared.Rent(256);
                    else if (count == dropped.Length)
                    {
                        var grown = ArrayPool<int>.Shared.Rent(count * 2);
                        dropped.AsSpan(0, count).CopyTo(grown);
                        ArrayPool<int>.Shared.Return(dropped);
                        dropped = grown;
                    }
                    dropped[count++] = written - 1;
                }
            }
        }
        droppedCount = count;
        return written;
    }

    // Original offset of normalized index position; cursor makes sequential lookups constant time in either direction.
    internal static int Raw(int[] dropped, int count, ref int cursor, int position)
    {
        while (cursor > 0 && dropped[cursor - 1] >= position) cursor--;
        while (cursor < count && dropped[cursor] < position) cursor++;
        return position + cursor;
    }

    internal static byte[] Rent(int length, out bool pooled)
    {
        // Beyond the shared pool's 1 MiB bucket limit a fresh, unzeroed array avoids a pool miss plus a memset.
        pooled = length <= 1 << 20;
        return pooled ? ArrayPool<byte>.Shared.Rent(length) : GC.AllocateUninitializedArray<byte>(length);
    }

    internal static void Release(byte[] buffer, bool pooled, int[]? dropped)
    {
        if (pooled) ArrayPool<byte>.Shared.Return(buffer);
        if (dropped is not null) ArrayPool<int>.Shared.Return(dropped);
    }
}

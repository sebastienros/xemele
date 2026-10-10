using System.Buffers;
using System.Runtime.CompilerServices;

namespace InputSpike;

internal static class Primitives
{
    internal static readonly SearchValues<byte> ByteStops = SearchValues.Create("\"&<\r\n"u8);
    internal static readonly SearchValues<char> CharStops = SearchValues.Create("\"&<\r\n");
    internal static readonly byte[] ArrayBytes = [(byte)'<', (byte)'?', (byte)'x', (byte)'m', (byte)'l'];
    internal static readonly char[] ArrayChars = ['<', '?', 'x', 'm', 'l'];
    private static readonly byte RuntimeByte = 0xEF;
    internal static ReadOnlySpan<byte> Utf8Literal => "<?xml"u8;
    internal static ReadOnlySpan<char> CharLiteral => "<?xml";
    internal static ReadOnlySpan<byte> ConstantBytes => [0xEF, 0xBB, 0xBF];
    internal static ReadOnlySpan<int> ConstantInts => [1, 2, 3];
    // Deliberate controls: a runtime field and a mutable span change lowering.
    internal static ReadOnlySpan<byte> RuntimeBytes => new byte[] { RuntimeByte, 0xBB, 0xBF };
    internal static Span<byte> MutableBytes => new byte[] { 0xEF, 0xBB, 0xBF };

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int RuntimeLocalSum()
    {
        ReadOnlySpan<byte> bytes = [RuntimeByte, 0xBB, 0xBF];
        return bytes[0] + bytes[1] + bytes[2];
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int MutableLocalSum()
    {
        Span<byte> bytes = [0xEF, 0xBB, 0xBF];
        return bytes[0] + bytes[1] + bytes[2];
    }
    public static int Simple(ReadOnlySpan<byte> input)
    {
        for (int i = 0; i < input.Length; i++) if (IsStop(input[i])) return i;
        return -1;
    }
    public static int Simple(ReadOnlySpan<char> input)
    {
        for (int i = 0; i < input.Length; i++) if (IsStop(input[i])) return i;
        return -1;
    }
    private static bool IsStop(int c) => c is '"' or '&' or '<' or '\r' or '\n';

    internal static readonly string[] Vocabulary = ["xml", "UTF-8", "PUBLIC", "SYSTEM", "CDATA", "DOCTYPE", "ELEMENT", "ATTLIST", "ENTITY", "NOTATION", "standalone", "encoding"];
    // Five-bit fixed ASCII hash intentionally creates collisions; full equality is mandatory.
    internal static uint Hash(ReadOnlySpan<char> token, bool insensitive)
    {
        uint hash = 0;
        foreach (char c in token)
        {
            if (c > 127) return uint.MaxValue;
            hash = (hash * 31 + Fold(c, insensitive)) & 31;
        }
        return hash;
    }
    internal static uint Hash(ReadOnlySpan<byte> token, bool insensitive)
    {
        uint hash = 0;
        foreach (byte c in token)
        {
            if (c > 127) return uint.MaxValue;
            hash = (hash * 31 + Fold(c, insensitive)) & 31;
        }
        return hash;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Fold(int c, bool insensitive) => (uint)(insensitive && c is >= 'A' and <= 'Z' ? c + 32 : c);
    internal static bool Equal(ReadOnlySpan<char> left, ReadOnlySpan<char> right, bool insensitive)
    {
        if (left.Length != right.Length) return false;
        if (!insensitive) return left.SequenceEqual(right);
        for (int i = 0; i < left.Length; i++)
            if (left[i] > 127 || right[i] > 127 || Fold(left[i], true) != Fold(right[i], true)) return false;
        return true;
    }
    internal static bool Equal(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, bool insensitive)
    {
        if (left.Length != right.Length) return false;
        if (!insensitive) return left.SequenceEqual(right);
        for (int i = 0; i < left.Length; i++)
            if (left[i] > 127 || right[i] > 127 || Fold(left[i], true) != Fold(right[i], true)) return false;
        return true;
    }
    internal static int LengthMatch(ReadOnlySpan<char> token, bool fold) => token.Length switch
    {
        3 => Equal(token, "xml", fold) ? 0 : -1,
        5 => Equal(token, "UTF-8", fold) ? 1 : Equal(token, "CDATA", fold) ? 4 : -1,
        6 => Equal(token, "PUBLIC", fold) ? 2 : Equal(token, "SYSTEM", fold) ? 3 : Equal(token, "ENTITY", fold) ? 8 : -1,
        7 => Equal(token, "DOCTYPE", fold) ? 5 : Equal(token, "ELEMENT", fold) ? 6 : Equal(token, "ATTLIST", fold) ? 7 : -1,
        8 => Equal(token, "NOTATION", fold) ? 9 : Equal(token, "encoding", fold) ? 11 : -1,
        10 => Equal(token, "standalone", fold) ? 10 : -1,
        _ => -1
    };
    internal static int LengthMatch(ReadOnlySpan<byte> token, bool fold) => token.Length switch
    {
        3 => Equal(token, "xml"u8, fold) ? 0 : -1,
        5 => Equal(token, "UTF-8"u8, fold) ? 1 : Equal(token, "CDATA"u8, fold) ? 4 : -1,
        6 => Equal(token, "PUBLIC"u8, fold) ? 2 : Equal(token, "SYSTEM"u8, fold) ? 3 : Equal(token, "ENTITY"u8, fold) ? 8 : -1,
        7 => Equal(token, "DOCTYPE"u8, fold) ? 5 : Equal(token, "ELEMENT"u8, fold) ? 6 : Equal(token, "ATTLIST"u8, fold) ? 7 : -1,
        8 => Equal(token, "NOTATION"u8, fold) ? 9 : Equal(token, "encoding"u8, fold) ? 11 : -1,
        10 => Equal(token, "standalone"u8, fold) ? 10 : -1,
        _ => -1
    };
    private static readonly uint[] ExactHashes = Vocabulary.Select(s => Hash(s, false)).ToArray();
    private static readonly uint[] FoldedHashes = Vocabulary.Select(s => Hash(s, true)).ToArray();
    // Owned once at initialization. Hot comparisons borrow arrays; never encode a candidate.
    private static readonly byte[][] Encoded = Vocabulary.Select(System.Text.Encoding.ASCII.GetBytes).ToArray();
    internal static int HashMatch(ReadOnlySpan<char> token, bool fold)
    {
        uint hash = Hash(token, fold);
        if (hash == uint.MaxValue) return -1;
        uint[] hashes = fold ? FoldedHashes : ExactHashes;
        for (int i = 0; i < hashes.Length; i++)
            if (hash == hashes[i] && Equal(token, Vocabulary[i], fold)) return i;
        return -1;
    }
    internal static int HashMatch(ReadOnlySpan<byte> token, bool fold)
    {
        uint hash = Hash(token, fold);
        if (hash == uint.MaxValue) return -1;
        uint[] hashes = fold ? FoldedHashes : ExactHashes;
        for (int i = 0; i < hashes.Length; i++)
            if (hash == hashes[i] && Equal(token, Encoded[i], fold)) return i;
        return -1;
    }
}

internal sealed class ChunkCandidate
{
    private readonly byte[] _bytes = new byte[10];
    private readonly char[] _chars = new char[10];
    private int _count;
    private bool _overflow;
    public void Reset() { _count = 0; _overflow = false; }
    public int Append(ReadOnlySpan<byte> chunk, bool end, bool fold, bool hash)
    {
        int take = Math.Min(chunk.Length, _bytes.Length - _count);
        chunk[..take].CopyTo(_bytes.AsSpan(_count)); _count += take;
        _overflow |= take != chunk.Length;
        return !end || _overflow ? -1 : hash ? Primitives.HashMatch(_bytes.AsSpan(0, _count), fold) : Primitives.LengthMatch(_bytes.AsSpan(0, _count), fold);
    }
    public int Append(ReadOnlySpan<char> chunk, bool end, bool fold, bool hash)
    {
        int take = Math.Min(chunk.Length, _chars.Length - _count);
        chunk[..take].CopyTo(_chars.AsSpan(_count)); _count += take;
        _overflow |= take != chunk.Length;
        return !end || _overflow ? -1 : hash ? Primitives.HashMatch(_chars.AsSpan(0, _count), fold) : Primitives.LengthMatch(_chars.AsSpan(0, _count), fold);
    }
}

using System.Buffers;
using System.Text;
namespace Xemele.Serialization;

internal static class CanonicalEscapes
{
    // Straightforward scalar baseline. No delimiter bulk scan, SearchValues or
    // hashing is used: every scalar must be encoded or replaced. Both input and
    // output are borrowed; the operation owns buffers and budget callbacks.
    internal static int Encode(ReadOnlySpan<char> input, Span<byte> output, int mode,
        out int consumed, Action<long>? charge = null)
    {
        int written = 0; consumed = 0;
        while (!input.IsEmpty)
        {
            charge?.Invoke(1);
            if (Rune.DecodeFromUtf16(input, out Rune rune, out int units) != OperationStatus.Done)
                throw new ArgumentException("Invalid UTF-16 input.", nameof(input));
            ReadOnlySpan<byte> replacement = Replacement(rune.Value, mode);
            int length = replacement.IsEmpty ? rune.Utf8SequenceLength : replacement.Length;
            if (length > output.Length - written) break;
            if (replacement.IsEmpty) rune.EncodeToUtf8(output[written..]); else replacement.CopyTo(output[written..]);
            written += length; consumed += units; input = input[units..];
        }
        return written;
    }
    private static ReadOnlySpan<byte> Replacement(int c, int mode)
    {
        if (mode == 3) return [];
        if (c == 13) return "&#xD;"u8;
        if (mode == 2) return [];
        if (c == '&') return "&amp;"u8;
        if (c == '<') return "&lt;"u8;
        if (mode == 0 && c == '>') return "&gt;"u8;
        if (mode == 1 && c == '"') return "&quot;"u8;
        if (mode == 1 && c == 9) return "&#x9;"u8;
        if (mode == 1 && c == 10) return "&#xA;"u8;
        return [];
    }
}

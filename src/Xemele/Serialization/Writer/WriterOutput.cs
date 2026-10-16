using System.Buffers;
using System.Text;
namespace Xemele;

// Owns only bounded encoder scratch. Continuation fields/Encode/BufferedMemory/
// ConsumeBuffer are the boundary for the later asynchronously draining transport.
internal sealed partial class WriterOutput : IDisposable
{
    internal const int Capacity = 8192;
    internal readonly Stream Stream;
    internal readonly Encoder Encoder;
    internal readonly CancellationToken Token;
    internal readonly int CodePage;
    internal byte[]? Bytes;
    internal int Buffered;
    internal WriterOutput(Stream stream, int page, CancellationToken token)
    {
        Stream = stream;
        CodePage = page;
        Token = token;
        Encoder = StrictEncoding(page).GetEncoder();
        Bytes = ArrayPool<byte>.Shared.Rent(Capacity);
    }
    internal static Encoding StrictEncoding(int page) => page switch
    {
        65001 => new UTF8Encoding(false, true),
        1200 => new UnicodeEncoding(false, false, true),
        _ => new UnicodeEncoding(true, false, true)
    };
    internal string Label => CodePage switch { 65001 => "utf-8", 1200 => "utf-16le", _ => "utf-16be" };
    internal ReadOnlyMemory<byte> BufferedMemory => Bytes!.AsMemory(0, Buffered);
    internal void ConsumeBuffer() => Buffered = 0;
    // Call only with at least 6148 free bytes for a <=2048-char chunk.
    internal int Encode(ReadOnlySpan<char> text, bool final = false)
    {
        Encoder.Convert(text, Bytes.AsSpan(Buffered, Capacity - Buffered), final,
            out var used, out var count, out _);
        Buffered += count;
        return used;
    }
    internal void Write(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            Token.ThrowIfCancellationRequested();
            if (Capacity - Buffered < 6148)
            {
                Drain();
            }

            var used = Encode(text[..Math.Min(text.Length, 2048)]);
            text = text[used..];
            if (used == 0)
            {
                Drain();
            }
        }
    }
    internal void Preamble()
    {
        ReadOnlySpan<byte> preamble = CodePage switch
        {
            65001 => [0xef, 0xbb, 0xbf],
            1200 => [0xff, 0xfe],
            _ => [0xfe, 0xff]
        };
        Token.ThrowIfCancellationRequested();
        Stream.Write(preamble);
    }
    internal void Drain()
    {
        if (Buffered == 0)
        {
            return;
        }

        Token.ThrowIfCancellationRequested();
        Stream.Write(BufferedMemory.Span);
        ConsumeBuffer();
    }
    internal void Complete()
    {
        Token.ThrowIfCancellationRequested();
        if (Capacity - Buffered < 4)
        {
            Drain();
        }

        Encode([], final: true);
        Drain();
    }
    public void Dispose()
    {
        var bytes = Bytes;
        Bytes = null;
        Buffered = 0;
        if (bytes is not null)
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }
}

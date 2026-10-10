using System.Text;

namespace InputSpike;

internal enum Step { NeedInput, Paused, Completed }
internal readonly record struct ScanResult(int Consumed, Step Step);
internal interface IFragmentSink<T> where T : unmanaged
{
    // The borrowed fragment expires on return. false requests suspension.
    bool Fragment(ReadOnlySpan<T> fragment, bool tokenEnded);
}
internal interface IUnits<T> where T : unmanaged
{
    static abstract int FindDelimiter(ReadOnlySpan<T> input);
}
internal readonly struct ByteUnits : IUnits<byte>
{
    public static int FindDelimiter(ReadOnlySpan<byte> input) => input.IndexOf((byte)'|');
}
internal readonly struct CharUnits : IUnits<char>
{
    public static int FindDelimiter(ReadOnlySpan<char> input) => input.IndexOf('|');
}

// Synthetic UTF payload tokens separated by '|'; NOT an XML grammar or public API.
// No source reference, token array, or whole-document buffer is stored.
internal sealed class Scanner<T, TUnits> where T : unmanaged where TUnits : IUnits<T>
{
    private bool _insideToken;
    private bool _completed;
    public int RetainedInputUnits => 0;
    public void Reset() { _insideToken = false; _completed = false; }

    public ScanResult Append<TSink>(ReadOnlySpan<T> input, bool final, TSink sink, int quantum = 64)
        where TSink : IFragmentSink<T>
    {
        if (_completed) throw new InvalidOperationException("Already completed.");
        if (quantum <= 0) throw new ArgumentOutOfRangeException(nameof(quantum));
        int consumed = 0;
        while (consumed < input.Length)
        {
            ReadOnlySpan<T> remaining = input[consumed..];
            // Search each payload unit once, plus one bounded boundary lookahead.
            // Searching the full tail per fragment rescans long tokens quadratically.
            int window = (int)Math.Min(remaining.Length, (long)quantum + 1);
            int delimiter = TUnits.FindDelimiter(remaining[..window]);
            int available = delimiter < 0 ? Math.Min(remaining.Length, quantum) : delimiter;
            int take = Math.Min(available, quantum);
            bool ended = delimiter >= 0 && take == available;
            ReadOnlySpan<T> fragment = remaining[..take];
            consumed += take + (ended ? 1 : 0);
            _insideToken = !ended;
            if (!sink.Fragment(fragment, ended)) return new(consumed, Step.Paused);
        }
        if (!final) return new(consumed, Step.NeedInput);
        if (_insideToken)
        {
            _insideToken = false;
            if (!sink.Fragment([], true)) return new(consumed, Step.Paused);
        }
        _completed = true;
        return new(consumed, Step.Completed);
    }
}

internal sealed class CharSink : IFragmentSink<char>
{
    private char _high;
    public long Checksum { get; private set; }
    public int Tokens { get; private set; }
    public bool Pause { get; set; }
    public void Reset() { _high = '\0'; Checksum = 0; Tokens = 0; Pause = false; }
    public bool Fragment(ReadOnlySpan<char> input, bool tokenEnded)
    {
        foreach (char c in input)
        {
            if (_high != '\0')
            {
                if (!char.IsLowSurrogate(c)) throw new InvalidDataException("Unpaired high surrogate.");
                _high = '\0';
            }
            else if (char.IsHighSurrogate(c)) _high = c;
            else if (char.IsLowSurrogate(c)) throw new InvalidDataException("Unpaired low surrogate.");
            Checksum = unchecked(Checksum * 31 + c);
        }
        if (tokenEnded)
        {
            if (_high != '\0') throw new InvalidDataException("Truncated surrogate.");
            Tokens++;
            Checksum = unchecked(Checksum * 31 + '|');
        }
        return !Pause;
    }
}

internal sealed class Utf8Sink : IFragmentSink<byte>
{
    private static readonly UTF8Encoding Strict = new(false, true);
    private readonly Decoder _decoder = Strict.GetDecoder();
    public CharSink Characters { get; } = new();
    public int ScratchUnits => 128;
    public int MaxPendingUtf8Bytes => 3;
    public void Reset() { _decoder.Reset(); Characters.Reset(); }
    public bool Fragment(ReadOnlySpan<byte> input, bool tokenEnded)
    {
        Span<char> scratch = stackalloc char[128];
        do
        {
            _decoder.Convert(input, scratch, tokenEnded, out int used, out int written, out bool done);
            Characters.Fragment(scratch[..written], tokenEnded && done);
            input = input[used..];
            if (done) break;
        } while (true);
        return !Characters.Pause;
    }
}

internal static class Pump
{
    public static long Characters(Scanner<char, CharUnits> scanner, CharSink sink, ReadOnlySpan<char> input, int chunk, bool pause = false)
    {
        scanner.Reset(); sink.Reset(); sink.Pause = pause;
        int offset = 0;
        do
        {
            int count = Math.Min(chunk, input.Length - offset);
            ScanResult result = scanner.Append(input.Slice(offset, count), offset + count == input.Length, sink);
            offset += result.Consumed;
            if (result.Step == Step.Completed) break;
        } while (true);
        return sink.Checksum;
    }
    public static long Bytes(Scanner<byte, ByteUnits> scanner, Utf8Sink sink, ReadOnlySpan<byte> input, int chunk, bool pause = false)
    {
        scanner.Reset(); sink.Reset(); sink.Characters.Pause = pause;
        int offset = 0;
        do
        {
            int count = Math.Min(chunk, input.Length - offset);
            ScanResult result = scanner.Append(input.Slice(offset, count), offset + count == input.Length, sink);
            offset += result.Consumed;
            if (result.Step == Step.Completed) break;
        } while (true);
        return sink.Characters.Checksum;
    }
    public static long DecodeToCharacters(Scanner<char, CharUnits> scanner, CharSink sink, Decoder decoder, ReadOnlySpan<byte> input, int chunk)
    {
        scanner.Reset(); sink.Reset(); decoder.Reset();
        Span<char> scratch = stackalloc char[128];
        int offset = 0;
        bool completed;
        do
        {
            int count = Math.Min(Math.Min(chunk, 128), input.Length - offset);
            bool final = offset + count == input.Length;
            decoder.Convert(input.Slice(offset, count), scratch, final, out int used, out int written, out completed);
            offset += used;
            scanner.Append(scratch[..written], final && completed, sink);
        } while (offset < input.Length || !completed);
        return sink.Checksum;
    }
}

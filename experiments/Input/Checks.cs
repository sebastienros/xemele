using System.Globalization;
using System.Text;

namespace InputSpike;

internal static class Checks
{
    private static int _checks;
    internal static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (Exception e) when (e is InvalidDataException or DecoderFallbackException or InvalidOperationException) { rejected = true; }
        Require(rejected, message);
    }
    internal static void Run()
    {
        _checks = 0;
        string[] texts = ["", "|", "a|b|", "unfinished", "élément|日本語|😀|Καλημέρα|", "😀😀|", "a||z", new string('x', 4096) + "|"];
        foreach (string text in texts)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            long expected = Oracle(text, out int tokens);
            for (int chunk = 1; chunk <= Math.Min(32, Math.Max(text.Length, bytes.Length)); chunk++)
            {
                var chars = new Scanner<char, CharUnits>(); var charSink = new CharSink();
                var utf8 = new Scanner<byte, ByteUnits>(); var byteSink = new Utf8Sink();
                Require(Pump.Characters(chars, charSink, text, chunk) == expected && charSink.Tokens == tokens, "Character chunk oracle.");
                Require(Pump.Bytes(utf8, byteSink, bytes, chunk) == expected && byteSink.Characters.Tokens == tokens, "Byte chunk oracle.");
                Require(Pump.DecodeToCharacters(chars, charSink, new UTF8Encoding(false, true).GetDecoder(), bytes, chunk) == expected, "Incremental decode oracle.");
            }
            // Every two-part split of short inputs, including empty first/final chunks.
            if (bytes.Length < 100)
            for (int split = 0; split <= bytes.Length; split++)
            {
                var scanner = new Scanner<byte, ByteUnits>(); var sink = new Utf8Sink();
                scanner.Append(bytes.AsSpan(0, split), false, sink);
                ScanResult result = scanner.Append(bytes.AsSpan(split), true, sink);
                Require(result.Step == Step.Completed && sink.Characters.Checksum == expected && sink.Characters.Tokens == tokens, "Every byte split.");
            }
            if (text.Length < 100)
            for (int split = 0; split <= text.Length; split++)
            {
                var scanner = new Scanner<char, CharUnits>(); var sink = new CharSink();
                scanner.Append(text.AsSpan(0, split), false, sink);
                scanner.Append(text.AsSpan(split), true, sink);
                Require(sink.Checksum == expected && sink.Tokens == tokens, "Every character split.");
            }
            PauseBytes(bytes, expected, tokens);
            PauseChars(text, expected, tokens);
        }
        string[] invalidChars = ["\ud800", "\udc00", "\ud800a|", "\ud800|", "a\udc00|"];
        foreach (string text in invalidChars)
            Reject(() => Pump.Characters(new(), new(), text, 1), "Invalid UTF-16 rejection.");
        byte[][] invalidBytes = [[0x80], [0xC0, 0xAF], [0xED, 0xA0, 0x80], [0xF4, 0x90, 0x80, 0x80], [0xF0, 0x9F], [0xE2, (byte)'|']];
        foreach (byte[] bytes in invalidBytes)
        {
            Reject(() => Pump.Bytes(new(), new(), bytes, 1), "Invalid UTF-8 byte core.");
            Reject(() => Pump.DecodeToCharacters(new(), new(), new UTF8Encoding(false, true).GetDecoder(), bytes, 1), "Invalid UTF-8 character core.");
        }
        var completedScanner = new Scanner<char, CharUnits>(); completedScanner.Append([], true, new CharSink());
        Reject(() => completedScanner.Append([], true, new CharSink()), "Completed state rejects append.");
        LongChunkProgress();
        TokenChecks();
        foreach (string name in new[] { "ShortEarly", "ShortLate", "ShortMiss", "LongEarly", "LongMiddle", "LongLate", "LongMiss", "UnicodeLate", "UnicodeMiss", "Dense" })
            new ScanBenchmarks { Case = name }.Setup();
        foreach (string name in new[] { "AsciiShort", "UnicodeShort", "AsciiLong", "UnicodeLong" })
            foreach (int chunk in new[] { 7, 256 }) new InputBenchmarks { Case = name, Chunk = chunk }.Setup();
        Console.WriteLine($"PASS {_checks} assertions; every short two-part split; chunks 1..32; per-fragment suspension; long single-chunk bounded progress; malformed Unicode; matching oracle/collisions/cultures; scan and pipeline setup parity.");
    }
    private static long Oracle(string text, out int tokens)
    {
        tokens = text.Count(c => c == '|') + (text.Length > 0 && text[^1] != '|' ? 1 : 0);
        long checksum = 0;
        foreach (char c in text) checksum = unchecked(checksum * 31 + c);
        if (text.Length > 0 && text[^1] != '|') checksum = unchecked(checksum * 31 + '|');
        return checksum;
    }
    private sealed class ProgressSink<T> : IFragmentSink<T> where T : unmanaged
    {
        public int Units;
        public int Ends;
        public int Calls;
        public int Largest;
        public bool Pause;
        public bool Fragment(ReadOnlySpan<T> fragment, bool ended)
        {
            Units += fragment.Length; Ends += ended ? 1 : 0; Calls++;
            Largest = Math.Max(Largest, fragment.Length);
            return !Pause;
        }
    }
    private static void LongChunkProgress()
    {
        // All data is supplied in ONE large chunk; transport chunking must not hide tail rescans.
        foreach (int length in new[] { 1, 63, 64, 65, 127, 128, 129, 4096, 1024 * 1024 })
        foreach (bool delimiter in new[] { false, true })
        foreach (bool pause in new[] { false, true })
        {
            string text = new string('x', length) + (delimiter ? "|" : "");
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            var chars = new Scanner<char, CharUnits>(); var charSink = new ProgressSink<char> { Pause = pause };
            var utf8 = new Scanner<byte, ByteUnits>(); var byteSink = new ProgressSink<byte> { Pause = pause };
            int charOffset = 0, byteOffset = 0;
            ScanResult charResult, byteResult;
            do
            {
                charResult = chars.Append(text.AsSpan(charOffset), true, charSink, quantum: 64);
                Require(charResult.Consumed <= (pause ? 65 : text.Length), "Character bounded progress.");
                charOffset += charResult.Consumed;
            } while (charResult.Step != Step.Completed);
            do
            {
                byteResult = utf8.Append(bytes.AsSpan(byteOffset), true, byteSink, quantum: 64);
                Require(byteResult.Consumed <= (pause ? 65 : bytes.Length), "Byte bounded progress.");
                byteOffset += byteResult.Consumed;
            } while (byteResult.Step != Step.Completed);
            int calls = (length + 63) / 64 + (delimiter ? 0 : 1);
            Require(charOffset == text.Length && byteOffset == bytes.Length, "Entire large chunk consumed.");
            Require(charSink.Units == length && byteSink.Units == length && charSink.Ends == 1 && byteSink.Ends == 1, "Long-token fragment/end accounting.");
            Require(charSink.Calls == calls && byteSink.Calls == calls && charSink.Largest <= 64 && byteSink.Largest <= 64, "Linear fragment count and bounded payload.");
        }
        // A maximal quantum must not overflow the lookahead length.
        var scanner = new Scanner<char, CharUnits>(); var sink = new ProgressSink<char>();
        Require(scanner.Append("x|", true, sink, int.MaxValue).Step == Step.Completed && sink.Ends == 1, "Checked lookahead for maximal quantum.");
    }
    private static void PauseBytes(byte[] input, long expected, int tokens)
    {
        var scanner = new Scanner<byte, ByteUnits>(); var sink = new Utf8Sink(); sink.Characters.Pause = true;
        int offset = 0; Step step;
        do
        {
            ScanResult result = scanner.Append(input.AsSpan(offset), true, sink, quantum: 1);
            offset += result.Consumed; step = result.Step;
            Require(scanner.RetainedInputUnits == 0, "No retained byte input.");
        } while (step != Step.Completed);
        Require(offset == input.Length && sink.Characters.Checksum == expected && sink.Characters.Tokens == tokens, "Paused byte parity.");
    }
    private static void PauseChars(string input, long expected, int tokens)
    {
        var scanner = new Scanner<char, CharUnits>(); var sink = new CharSink { Pause = true };
        int offset = 0; Step step;
        do
        {
            ScanResult result = scanner.Append(input.AsSpan(offset), true, sink, quantum: 1);
            offset += result.Consumed; step = result.Step;
            Require(scanner.RetainedInputUnits == 0, "No retained character input.");
        } while (step != Step.Completed);
        Require(offset == input.Length && sink.Checksum == expected && sink.Tokens == tokens, "Paused character parity.");
    }
    private static void TokenChecks()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var candidates = Primitives.Vocabulary.Concat(Primitives.Vocabulary.Select(s => s.ToLowerInvariant()))
                    .Concat(new[] { "", "xnm", "standalonf", "standaloneX", "stand", "[ML", "éml", "ſystem", "DOCTYPEX", "ENTITY!", "XML" });
                foreach (string token in candidates)
                foreach (bool fold in new[] { false, true })
                {
                    int expected = Array.FindIndex(Primitives.Vocabulary, s => AsciiOracle(token, s, fold));
                    byte[] bytes = Encoding.UTF8.GetBytes(token);
                    Require(Primitives.LengthMatch(token, fold) == expected && Primitives.HashMatch(token, fold) == expected, "Character match oracle.");
                    Require(Primitives.LengthMatch(bytes, fold) == expected && Primitives.HashMatch(bytes, fold) == expected, "Byte match oracle.");
                    for (int split = 0; split <= token.Length; split++)
                    foreach (bool hash in new[] { false, true })
                    {
                        var candidate = new ChunkCandidate(); candidate.Append(token.AsSpan(0, split), false, fold, hash);
                        Require(candidate.Append(token.AsSpan(split), true, fold, hash) == expected, "Character candidate split/end/overflow.");
                    }
                    for (int split = 0; split <= bytes.Length; split++)
                    foreach (bool hash in new[] { false, true })
                    {
                        var candidate = new ChunkCandidate(); candidate.Append(bytes.AsSpan(0, split), false, fold, hash);
                        Require(candidate.Append(bytes.AsSpan(split), true, fold, hash) == expected, "Byte candidate split/end/overflow.");
                    }
                }
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
        Require(Primitives.Hash("xml", false) == Primitives.Hash("xnm", false), "Deliberate hash collision fixture.");
        Require(Primitives.HashMatch("xnm", false) == -1, "Collision fails equality.");
        Require(Primitives.LengthMatch("[ML", true) == -1, "Punctuation is not folded into letters.");
        Require(Primitives.Utf8Literal.SequenceEqual(Primitives.ArrayBytes) && Primitives.CharLiteral.SequenceEqual(Primitives.ArrayChars), "Literal parity.");
        // Deterministic adversarial ASCII/non-ASCII unknowns checked against a simple reference.
        var random = new Random(1776);
        for (int i = 0; i < 1000; i++)
        {
            string token = new(Enumerable.Range(0, random.Next(0, 13)).Select(_ => (char)random.Next(32, 256)).ToArray());
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            foreach (bool fold in new[] { false, true })
            {
                int expected = Array.FindIndex(Primitives.Vocabulary, s => AsciiOracle(token, s, fold));
                Require(Primitives.LengthMatch(token, fold) == expected && Primitives.HashMatch(token, fold) == expected, "Random character match.");
                Require(Primitives.LengthMatch(bytes, fold) == expected && Primitives.HashMatch(bytes, fold) == expected, "Random byte match.");
            }
        }
    }
    private static bool AsciiOracle(string a, string b, bool fold)
    {
        if (a.Length != b.Length || a.Any(c => c > 127)) return false;
        return string.Equals(a, b, fold ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}

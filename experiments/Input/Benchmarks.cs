using System.Text;
using BenchmarkDotNet.Attributes;

namespace InputSpike;

[MemoryDiagnoser, ShortRunJob]
public class InputBenchmarks
{
    [Params("AsciiShort", "UnicodeShort", "AsciiLong", "UnicodeLong")]
    public string Case { get; set; } = "AsciiShort";
    [Params(7, 256)] public int Chunk { get; set; }
    private string _text = "";
    private byte[] _bytes = [];
    private readonly Scanner<char, CharUnits> _chars = new();
    private readonly Scanner<byte, ByteUnits> _utf8 = new();
    private readonly CharSink _charSink = new();
    private readonly Utf8Sink _byteSink = new();
    private readonly Decoder _decoder = new UTF8Encoding(false, true).GetDecoder();
    private static readonly UTF8Encoding Strict = new(false, true);
    [GlobalSetup]
    public void Setup()
    {
        string token = Case.StartsWith("Unicode", StringComparison.Ordinal) ? "élément_日本語_😀|texte_Καλημέρα|" : "element_attribute|text_value|";
        _text = Case.EndsWith("Long", StringComparison.Ordinal) ? string.Concat(Enumerable.Repeat(token, 128)) : token;
        _bytes = Strict.GetBytes(_text);
        long expected = StringToCharacterCore();
        if (ByteToByteCore() != expected || ByteToCharacterScratch() != expected || ByteToOwnedString() != expected || StringViaOwnedUtf8() != expected || StringToCharacterCorePaused() != expected || ByteToByteCorePaused() != expected)
            throw new InvalidOperationException("Pipeline checksums differ.");
    }
    [Benchmark(Baseline = true)] public long StringToCharacterCore() => Pump.Characters(_chars, _charSink, _text, Chunk);
    [Benchmark] public long ByteToByteCore() => Pump.Bytes(_utf8, _byteSink, _bytes, Chunk);
    [Benchmark] public long ByteToCharacterScratch() => Pump.DecodeToCharacters(_chars, _charSink, _decoder, _bytes, Chunk);
    [Benchmark] public long StringToCharacterCorePaused() => Pump.Characters(_chars, _charSink, _text, Chunk, pause: true);
    [Benchmark] public long ByteToByteCorePaused() => Pump.Bytes(_utf8, _byteSink, _bytes, Chunk, pause: true);
    [Benchmark] public long ByteToOwnedString() => Pump.Characters(_chars, _charSink, Strict.GetString(_bytes), Chunk);
    [Benchmark] public long StringViaOwnedUtf8() => Pump.Bytes(_utf8, _byteSink, Strict.GetBytes(_text), Chunk);
}

[MemoryDiagnoser, ShortRunJob]
public class ScanBenchmarks
{
    [Params("ShortEarly", "ShortLate", "ShortMiss", "LongEarly", "LongMiddle", "LongLate", "LongMiss", "UnicodeLate", "UnicodeMiss", "Dense")]
    public string Case { get; set; } = "ShortEarly";
    private string _text = "";
    private byte[] _bytes = [];
    [GlobalSetup]
    public void Setup()
    {
        int length = Case.StartsWith("Short", StringComparison.Ordinal) ? 12 : 1024;
        _text = Case.StartsWith("Unicode", StringComparison.Ordinal) ? string.Concat(Enumerable.Repeat("é日本😀", 128)) : new string('x', length);
        if (Case == "Dense") _text = string.Concat(Enumerable.Repeat("abc&def\n", 128));
        else if (Case.EndsWith("Early", StringComparison.Ordinal)) _text = "<" + _text[1..];
        else if (Case.EndsWith("Middle", StringComparison.Ordinal)) _text = _text[..(length / 2)] + "&" + _text[(length / 2 + 1)..];
        else if (Case.EndsWith("Late", StringComparison.Ordinal)) _text = _text[..^1] + "\n";
        _bytes = Encoding.UTF8.GetBytes(_text);
        if (ByteCached() != ByteSimple() || CharCached() != CharSimple() || ByteSmallSet() != ByteSimple() || CharSmallSet() != CharSimple())
            throw new InvalidOperationException("Scans differ.");
    }
    [Benchmark] public int ByteCached() => _bytes.AsSpan().IndexOfAny(Primitives.ByteStops);
    [Benchmark] public int ByteSimple() => Primitives.Simple(_bytes);
    [Benchmark] public int CharCached() => _text.AsSpan().IndexOfAny(Primitives.CharStops);
    [Benchmark] public int CharSimple() => Primitives.Simple(_text);
    [Benchmark] public int ByteSmallSet() => _bytes.AsSpan().IndexOfAny("\"&<\r\n"u8);
    [Benchmark] public int CharSmallSet() => _text.AsSpan().IndexOfAny("\"&<\r\n");
    // Single-delimiter control has distinct semantics; never used to select a five-stop scanner.
    [Benchmark] public int ByteIndexOfControl() => _bytes.AsSpan().IndexOf((byte)'<');
    [Benchmark] public int CharIndexOfControl() => _text.AsSpan().IndexOf('<');
}

[MemoryDiagnoser, ShortRunJob]
public class MatchBenchmarks
{
    [Params("xml", "standalone", "xnm", "standalonf", "DOCTYPE", "doctype", "UTF-8", "utf-8", "[ML", "éml", "standaloneX", "stand")]
    public string Token { get; set; } = "xml";
    [Params(false, true)] public bool IgnoreAsciiCase { get; set; }
    private byte[] _bytes = [];
    [GlobalSetup] public void Setup()
    {
        _bytes = Encoding.UTF8.GetBytes(Token);
        if (ByteLength() != ByteHash() || CharLength() != CharHash()) throw new InvalidOperationException("Matchers differ.");
    }
    [Benchmark] public int ByteLength() => Primitives.LengthMatch(_bytes, IgnoreAsciiCase);
    [Benchmark] public int ByteHash() => Primitives.HashMatch(_bytes, IgnoreAsciiCase);
    [Benchmark] public int CharLength() => Primitives.LengthMatch(Token, IgnoreAsciiCase);
    [Benchmark] public int CharHash() => Primitives.HashMatch(Token, IgnoreAsciiCase);
}

[MemoryDiagnoser, ShortRunJob]
public class ChunkMatchBenchmarks
{
    [Params("xml", "standalone", "standaloneX", "DOCTYPE", "doctype", "éml")]
    public string Token { get; set; } = "xml";
    private byte[] _bytes = [];
    private readonly ChunkCandidate _candidate = new();
    [GlobalSetup] public void Setup() => _bytes = Encoding.UTF8.GetBytes(Token);
    [Benchmark] public int ByteLength() => ByteMatch(false);
    [Benchmark] public int ByteHash() => ByteMatch(true);
    [Benchmark] public int CharLength() => CharMatch(false);
    [Benchmark] public int CharHash() => CharMatch(true);
    private int ByteMatch(bool hash)
    {
        _candidate.Reset(); int split = _bytes.Length / 2;
        _candidate.Append(_bytes.AsSpan(0, split), false, true, hash);
        return _candidate.Append(_bytes.AsSpan(split), true, true, hash);
    }
    private int CharMatch(bool hash)
    {
        _candidate.Reset(); int split = Token.Length / 2;
        _candidate.Append(Token.AsSpan(0, split), false, true, hash);
        return _candidate.Append(Token.AsSpan(split), true, true, hash);
    }
}

[MemoryDiagnoser, ShortRunJob]
public class LiteralBenchmarks
{
    [Params("<?xml", "<?xmX")] public string Token { get; set; } = "<?xml";
    private byte[] _bytes = [];
    [GlobalSetup] public void Setup() => _bytes = Encoding.UTF8.GetBytes(Token);
    [Benchmark] public bool ByteLiteral() => _bytes.AsSpan().SequenceEqual(Primitives.Utf8Literal);
    [Benchmark] public bool ByteArray() => _bytes.AsSpan().SequenceEqual(Primitives.ArrayBytes);
    [Benchmark] public bool CharLiteral() => Token.AsSpan().SequenceEqual(Primitives.CharLiteral);
    [Benchmark] public bool CharArray() => Token.AsSpan().SequenceEqual(Primitives.ArrayChars);
    [Benchmark] public byte ConstantCollection() => Primitives.ConstantBytes[0];
    [Benchmark] public int ConstantIntCollection() => Primitives.ConstantInts[0];
    [Benchmark] public byte RuntimeCollectionControl() => Primitives.RuntimeBytes[0];
    [Benchmark] public byte MutableCollectionControl() => Primitives.MutableBytes[0];
    [Benchmark] public int RuntimeLocalCollection() => Primitives.RuntimeLocalSum();
    [Benchmark] public int MutableLocalCollection() => Primitives.MutableLocalSum();
}

[MemoryDiagnoser, ShortRunJob]
public class RejectionBenchmarks
{
    [Params("Overlong", "Surrogate", "Truncated")]
    public string Case { get; set; } = "Overlong";
    private byte[] _input = [];
    private readonly Scanner<byte, ByteUnits> _bytes = new();
    private readonly Utf8Sink _byteSink = new();
    private readonly Scanner<char, CharUnits> _chars = new();
    private readonly CharSink _charSink = new();
    private readonly Decoder _decoder = new UTF8Encoding(false, true).GetDecoder();
    [GlobalSetup] public void Setup() => _input = Case switch { "Overlong" => [0xC0, 0xAF], "Surrogate" => [0xED, 0xA0, 0x80], _ => [0xF0, 0x9F] };
    [Benchmark] public int ByteCore()
    {
        try { Pump.Bytes(_bytes, _byteSink, _input, 1); return 0; }
        catch (DecoderFallbackException) { return 1; }
    }
    [Benchmark] public int CharacterScratch()
    {
        try { Pump.DecodeToCharacters(_chars, _charSink, _decoder, _input, 1); return 0; }
        catch (DecoderFallbackException) { return 1; }
    }
}

using System.Buffers;
using BenchmarkDotNet.Attributes;

namespace Xemele.XmlPerformance;

// Candidate scans only: grammar, Unicode scalar and quota checks still follow in the parser.
[MemoryDiagnoser, ShortRunJob]
public class ParserScans
{
    private const string TextSet = "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u000a\u000b\u000c\u000d\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f<&]\ufffe\uffff";
    private const string NameSet = "-.:0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";
    private const string TagSet = ">'\"";
    private static readonly SearchValues<char> TextStops = SearchValues.Create(TextSet);
    private static readonly SearchValues<char> NameUnits = SearchValues.Create(NameSet);
    private static readonly SearchValues<char> TagStops = SearchValues.Create(TagSet);
    private string value = "";

    [Params("text", "name", "tag")] public string Kind { get; set; } = "text";
    [Params(3, 32, 4096)]
    public int Length
    {
        get; set;
    }
    [Params("miss", "early", "last", "unicode")] public string Shape { get; set; } = "miss";

    [GlobalSetup]
    public void Setup()
    {
        // Exhaustively guard the mirrored scalar predicate, including NUL and surrogates.
        for (var c = 0; c <= char.MaxValue; c++)
        {
            value = ((char)c).ToString();
            Check();
        }
        value = new string('a', Length);
        var stop = Kind == "name" ? ' ' : Kind == "tag" ? '"' : '&';
        if (Shape == "early")
        {
            value = value[..2] + stop + value[3..];
        }

        if (Shape == "last")
        {
            value = value[..^1] + stop;
        }

        if (Shape == "unicode")
        {
            value = string.Concat(Enumerable.Repeat("α日本語😀", Length / 6 + 1))[..Length];
        }

        Check();
    }

    private void Check()
    {
        if (Scalar() != Cached() || Scalar() != RuntimeSet())
        {
            throw new InvalidOperationException("Candidate scan oracle differs: " + Kind);
        }
    }

    [Benchmark(Baseline = true)]
    public int Scalar()
    {
        var input = value.AsSpan();
        // Dispatch once, outside the character loop, as in state-specific parser routines.
        switch (Kind)
        {
            case "text":
                for (var i = 0; i < input.Length; i++)
                {
                    var c = input[i];
                    if ((c < ' ' && c != '\t') || c is '<' or '&' or ']' or '\ufffe' or '\uffff')
                    {
                        return i;
                    }
                }
                break;
            case "name":
                for (var i = 0; i < input.Length; i++)
                {
                    var c = input[i];
                    if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.' or ':' or '_'))
                    {
                        return i;
                    }
                }
                break;
            case "tag":
                for (var i = 0; i < input.Length; i++)
                {
                    if (input[i] is '>' or '\'' or '"')
                    {
                        return i;
                    }
                }

                break;
        }
        return -1;
    }

    [Benchmark]
    public int Cached() => Kind switch
    {
        "text" => value.AsSpan().IndexOfAny(TextStops),
        "name" => value.AsSpan().IndexOfAnyExcept(NameUnits),
        "tag" => value.AsSpan().IndexOfAny(TagStops),
        _ => throw new InvalidOperationException()
    };

    [Benchmark]
    public int RuntimeSet() => Kind switch
    {
        "text" => value.AsSpan().IndexOfAny(TextSet),
        "name" => value.AsSpan().IndexOfAnyExcept(NameSet),
        "tag" => value.AsSpan().IndexOfAny('>', '\'', '"'),
        _ => throw new InvalidOperationException()
    };
}

using BenchmarkDotNet.Attributes;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8DatatypesCore;

[MemoryDiagnoser, ShortRunJob]
public class NormalizationBenchmarks
{
    [Params("short", "long", "multilingual", "whitespace")] public string Case { get; set; } = "short";
    private string input = null!;
    private char[] output = null!;
    private ISchemaOperation operation = null!;
    [GlobalSetup]
    public void Setup()
    {
        input = Case switch
        {
            "short" => " value ",
            "long" => new string('x', 1024),
            "multilingual" => "\t根😀 العربية Ελληνικά\r",
            "whitespace" => new string(' ', 512) + "token\t\r\nvalue" + new string(' ', 512),
            _ => throw new ArgumentException()
        };
        output = new char[input.Length];
        operation = new SchemaCompileOperation(new()
        {
            MaxOperations = null,
            MaxComponents = null,
            Limits = new()
            {
                EvaluationWork = null
            }
        });
        var baseline = ScalarBaseline.Normalize(input, output, operation);
        string expected = new(output, 0, baseline);
        if (!DatatypeNormalization.TryNormalize(input, XmlWhitespaceRule.Collapse, output, operation, out var core, out _) || expected != new string(output, 0, core))
        {
            throw new InvalidOperationException("Baseline differs.");
        }
    }
    [Benchmark(Baseline = true)] public int ScalarLoop() => ScalarBaseline.Normalize(input, output, operation);
    [Benchmark]
    public int RuneCore()
    {
        if (!DatatypeNormalization.TryNormalize(input, XmlWhitespaceRule.Collapse, output, operation, out var written, out _))
        {
            throw new InvalidOperationException();
        }

        return written;
    }
}

// Independent straightforward two-pass scalar baseline. The same 2N work and
// XML whitespace semantics apply; no owned output or per-operation allocation.
internal static class ScalarBaseline
{
    internal static int Normalize(ReadOnlySpan<char> input, Span<char> output, ISchemaOperation operation)
    {
        if (!operation.Charge(0, default))
        {
            throw new InvalidOperationException();
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (!operation.Charge(1, default))
            {
                throw new InvalidOperationException();
            }

            var c = input[i];
            if (!char.IsSurrogate(c))
            {
                continue;
            }

            if (!char.IsHighSurrogate(c) || i + 1 == input.Length || !char.IsLowSurrogate(input[++i]))
            {
                throw new FormatException();
            }
        }
        if (!operation.Charge(0, default))
        {
            throw new InvalidOperationException();
        }

        var written = 0;
        var pending = false;
        for (var i = 0; i < input.Length; i++)
        {
            if (!operation.Charge(1, default))
            {
                throw new InvalidOperationException();
            }

            var c = input[i];
            if (c is '\t' or '\r' or '\n' or ' ')
            {
                if (written > 0)
                {
                    pending = true;
                }

                continue;
            }
            if (pending)
            {
                output[written++] = ' ';
                pending = false;
            }
            output[written++] = c;
            if (char.IsHighSurrogate(c))
            {
                output[written++] = input[++i];
            }
        }
        if (!operation.Charge(0, default))
        {
            throw new InvalidOperationException();
        }

        return written;
    }
}

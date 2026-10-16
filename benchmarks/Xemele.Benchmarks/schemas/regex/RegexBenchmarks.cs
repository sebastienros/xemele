using BenchmarkDotNet.Attributes;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8Regex;

[MemoryDiagnoser, ShortRunJob]
public class RegexBenchmarks
{
    private readonly XmlSchemaRegexEngine engine = new();
    private XmlSchemaRegexProgram program = null!;
    private SchemaValidationOperation operation = null!;
    private string input = null!;
    [Params("ShortHit", "LongHit", "EarlyMiss", "LateMiss", "Unicode", "Nullable", "Pooled", "Malformed")]
    public string Case { get; set; } = "ShortHit";
    [GlobalSetup]
    public void Setup()
    {
        var pattern = Case switch
        {
            "Unicode" => "[\\p{L}\\p{So}]+",
            "Nullable" => "(a?)*b",
            "Pooled" => "[a-z]{1,200}",
            _ => "[a-z-[aeiou]]+"
        };
        input = Case switch
        {
            "ShortHit" => "bc",
            "LongHit" => new string('b', 1024),
            "EarlyMiss" => "a" + new string('b', 1023),
            "LateMiss" => new string('b', 1023) + "a",
            "Unicode" => "名😀Ω𐐀",
            "Nullable" => new string('a', 128) + "b",
            "Pooled" => new string('b', 100),
            _ => new string((char)0xD800, 1)
        };
        program = engine.Compile(pattern, default, new SchemaCompileOperation());
        operation = new(new()
        {
            MaxOperations = null,
            Limits = new()
            {
                EvaluationWork = null
            }
        });
        for (var i = 0; i < 32; i++)
        {
            MatchBorrowed();
        }
    }
    [Benchmark] public bool MatchBorrowed() => engine.IsMatch(program, input.AsSpan(), operation);
    public bool MatchOwnedRun() => engine.IsMatch(program, input.AsSpan(), new SchemaValidationOperation(new() { MaxOperations = null, Limits = new() { EvaluationWork = null } }));
}

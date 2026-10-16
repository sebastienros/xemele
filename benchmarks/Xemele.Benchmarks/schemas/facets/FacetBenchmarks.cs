using BenchmarkDotNet.Attributes;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8Facets;

[MemoryDiagnoser, ShortRunJob]
public class FacetBenchmarks
{
    [Params("short", "long", "invalid")] public string Case { get; set; } = "short";
    private string input = null!;
    private XmlFacetEvaluator evaluator = null!;
    private XmlDatatypeRegistry registry = null!;
    private XmlDatatypeDefinition type = null!;
    private SchemaNamespaceContext context = null!;
    private ISchemaOperation operation = null!;
    private XmlDatatypeValue value = null!;
    [GlobalSetup]
    public void Setup()
    {
        input = Case switch
        {
            "short" => "alpha",
            "long" => new string('a', 512),
            _ => "Alpha"
        };
        context = new(new Dictionary<string, string>(), "", null);
        operation = new SchemaCompileOperation(new()
        {
            MaxOperations = null,
            MaxComponents = null,
            Limits = new()
            {
                EvaluationWork = null
            }
        });
        type = new(new("urn:bench", "pattern"), XmlDatatypeVariety.Atomic, DatatypeTypes.Name("string"), null, [], [new(XmlFacetKind.Pattern, "[a-z]+", false, context, default)], [], false, default);
        registry = new([type], operation);
        evaluator = new();
        if (!evaluator.ValidateDefinition(type, registry, operation))
        {
            throw new InvalidOperationException();
        }

        if (!registry.Find(DatatypeTypes.XsdNamespace)!.TryParse(DatatypeTypes.Name("string"), input, context, operation, out var parsed, out _))
        {
            throw new InvalidOperationException();
        }

        value = parsed!;
    }
    // Straight ASCII oracle for this precisely pinned whole-language pattern.
    // No generator/hash/SearchValues or different regex semantics are introduced.
    [Benchmark(Baseline = true)]
    public bool BaselineLoop()
    {
        if (input.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] is < 'a' or > 'z')
            {
                return false;
            }
        }

        return true;
    }
    [Benchmark] public bool BorrowedPreparedPattern() => evaluator.CheckPreparedValue(type, input, value, registry, operation);
}

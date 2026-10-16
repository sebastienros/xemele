using BenchmarkDotNet.Attributes;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8DatatypesNumeric;

[MemoryDiagnoser, ShortRunJob]
public class NumericBenchmarks
{
    [Params("short", "long")] public string Case { get; set; } = "short";
    private string input = null!, larger = null!, invalid = null!;
    private NumericDatatypeProvider provider = null!;
    private SchemaNamespaceContext context = null!;
    private ISchemaOperation operation = null!;
    private DatatypeAtomicValue left = null!, right = null!;
    [GlobalSetup]
    public void Setup()
    {
        input = Case == "short" ? "12345678" : new string('1', 4096);
        larger = input[..^1] + '9';
        invalid = input + "x";
        provider = new();
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
        left = Parse(input);
        right = Parse(larger);
    }
    private DatatypeAtomicValue Parse(string text)
    {
        if (!provider.TryParse(DatatypeTypes.All["decimal"], text, context, operation, out var value, out _) || value is null)
        {
            throw new InvalidOperationException();
        }

        return value;
    }
    // Straight scalar comparison baseline for these canonical positive integral
    // fixtures. It visits the same characters and charges the same work as core
    // dispatch + provider, without payload/primitive/retype joins.
    [Benchmark(Baseline = true)]
    public int BaselineCompare()
    {
        DatatypeValues.Charge(operation, 2);
        for (var i = 0; i < input.Length; i++)
        {
            DatatypeValues.Charge(operation);
            var comparison = input[i].CompareTo(larger[i]);
            if (comparison != 0)
            {
                DatatypeValues.Charge(operation, 0);
                return Math.Sign(comparison);
            }
        }
        DatatypeValues.Charge(operation, 0);
        return 0;
    }
    [Benchmark] public int ProviderCompare() => (int)DatatypeValues.Compare(left, right, operation);
    [Benchmark] public int ProviderHash() => DatatypeValues.ValueHashCode(left, operation);
    [Benchmark] public object OwnedParse() => Parse(input);
    [Benchmark] public bool InvalidSuffix() => provider.TryParse(DatatypeTypes.All["decimal"], invalid, context, operation, out _, out _);
}

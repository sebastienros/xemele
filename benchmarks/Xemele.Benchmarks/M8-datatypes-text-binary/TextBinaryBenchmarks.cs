using BenchmarkDotNet.Attributes;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8DatatypesTextBinary;

[MemoryDiagnoser, ShortRunJob]
public class TextBinaryBenchmarks
{
    [Params("short", "long")] public string Case { get; set; } = "short";
    private string input = null!, duplicate = null!, invalid = null!;
    private TextBinaryDatatypeProvider provider = null!;
    private SchemaNamespaceContext context = null!;
    private ISchemaOperation operation = null!;
    private DatatypeMetadata metadata;
    private DatatypeAtomicValue left = null!, right = null!;
    [GlobalSetup]
    public void Setup()
    {
        input = Case == "short" ? "根😀 value" : "根😀" + new string('a', 4096);
        duplicate = new string(input.AsSpan()); invalid = input + '\0';
        provider = new(); context = new(new Dictionary<string, string>(), "", null);
        operation = new SchemaCompileOperation(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } });
        metadata = DatatypeTypes.All["string"]; left = Parse(input); right = Parse(duplicate);
    }
    private DatatypeAtomicValue Parse(string text)
    {
        if (!provider.TryParse(metadata, text, context, operation, out var value, out _) || value is null) throw new InvalidOperationException();
        return value;
    }
    // Straight scalar equality oracle for the same immutable UTF-16 fixtures,
    // with the same length/core/provider shape and scalar-pair work charges.
    [Benchmark(Baseline = true)] public bool BaselineEquals()
    {
        DatatypeValues.Charge(operation, 3);
        bool equal = input.Length == duplicate.Length;
        for (int i = 0, j = 0; equal && i < input.Length;)
        {
            DatatypeValues.Charge(operation);
            char a = input[i++], b = duplicate[j++];
            int x = char.IsHighSurrogate(a) ? char.ConvertToUtf32(a, input[i++]) : a;
            int y = char.IsHighSurrogate(b) ? char.ConvertToUtf32(b, duplicate[j++]) : b;
            equal = x == y;
        }
        DatatypeValues.Charge(operation, 0); return equal;
    }
    [Benchmark] public bool ProviderEquals() => DatatypeValues.ValueEquals(left, right, operation);
    [Benchmark] public int ProviderHash() => DatatypeValues.ValueHashCode(left, operation);
    [Benchmark] public object OwnedParse() => Parse(input);
    [Benchmark] public bool InvalidSuffix() => provider.TryParse(metadata, invalid, context, operation, out _, out _);
}

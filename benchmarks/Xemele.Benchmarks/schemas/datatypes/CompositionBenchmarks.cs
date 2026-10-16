using BenchmarkDotNet.Attributes;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8Datatypes;

[MemoryDiagnoser, ShortRunJob]
public class CompositionBenchmarks
{
    [Params("short", "long", "invalid")] public string Case { get; set; } = "short";
    private string input = null!;
    private IXmlDatatypeLibrary library = null!;
    private SchemaNamespaceContext context = null!;
    private ISchemaOperation operation = null!;
    internal XmlDatatypeValue? LastValue;
    internal bool Equivalent(XmlDatatypeValue? baseline) => baseline is null ? LastValue is null :
        LastValue is not null && library.ValueEquals(baseline, LastValue, operation) &&
        library.ValueHashCode(baseline, operation) == library.ValueHashCode(LastValue, operation);
    [GlobalSetup]
    public void Setup()
    {
        input = Case switch
        {
            "short" => " a\tb\r\n根 ",
            "long" => " \t根" + new string('a', 512) + " b\n c ",
            _ => " a b ! "
        };
        library = new XmlDatatypeRegistry().Find(DatatypeTypes.XsdNamespace)!;
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
    }
    // Straight materializing oracle, same whitespace/item/value semantics with
    // unlimited budgets. Setup/fixture generation are outside timed methods.
    [Benchmark(Baseline = true)]
    public bool BaselineSplit()
    {
        LastValue = null;
        var normalized = library.Normalize(input, XmlWhitespaceRule.Collapse, operation);
        if (normalized.Length == 0)
        {
            return false;
        }

        var tokens = normalized.Split(' ');
        var items = new XmlDatatypeValue[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!library.TryParse(DatatypeTypes.Name("NMTOKEN"), tokens[i], context, operation, out var value, out _))
            {
                return false;
            }

            items[i] = value!;
        }
        LastValue = library.CreateList(DatatypeTypes.Name("NMTOKENS"), items, operation);
        GC.KeepAlive(LastValue);
        return true;
    }
    [Benchmark]
    public bool BorrowedList()
    {
        var valid = library.TryParse(DatatypeTypes.Name("NMTOKENS"), input, context, operation, out var value, out _);
        LastValue = value;
        GC.KeepAlive(value);
        return valid;
    }
}

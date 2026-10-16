using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;
using Xemele.Validation;

namespace Xemele.Benchmarks.M5DtdModel;

[MemoryDiagnoser, ShortRunJob]
public class DtdModelBenchmarks
{
    private DtdModel source = null!;
    private CompiledDtdModel compiled = null!;
    private ValidationOperation operation = null!;
    private string name = null!, miss = null!;
    [Params(8, 128, 4096)]
    public int NameLength
    {
        get; set;
    }
    [GlobalSetup]
    public void Setup()
    {
        name = "a" + new string('b', NameLength - 1);
        miss = name[..^1] + "c";
        using var parser = new DtdParser(DtdSubsetKind.External, new());
        var result = parser.Append("<!ELEMENT r (" + name + "*)><!ELEMENT " + name + " EMPTY><!ATTLIST r kind (one|two) 'one'>", [], true, default);
        if (result.Step != DtdParseStep.Completed)
        {
            throw new InvalidOperationException();
        }

        source = parser.Model;
        compiled = DtdCompiler.Compile(source, new());
        operation = new(new()
        {
            MaxOperations = null,
            Limits = new()
            {
                EvaluationWork = null
            }
        });
    }
    [Benchmark]
    public bool TransitionHit()
    {
        var state = 0;
        return compiled.Elements[0].Content.TryAdvance(ref state, name, operation);
    }
    [Benchmark]
    public bool TransitionLastCharacterMiss()
    {
        var state = 0;
        return compiled.Elements[0].Content.TryAdvance(ref state, miss, operation);
    }
    [Benchmark] public object? BorrowedLookupHit() => compiled.FindElement(name.AsSpan(), operation);
    [Benchmark] public object? BorrowedLookupMiss() => compiled.FindElement(miss.AsSpan(), operation);
    [Benchmark] public bool AttributeTokenHit() => compiled.Attributes[0].IsValueValid("two", compiled, operation);
    [Benchmark] public bool AttributeTokenMiss() => compiled.Attributes[0].IsValueValid("Two", compiled, operation);
    [Benchmark] public object ColdCompilation() => DtdCompiler.Compile(source, new());
    [Benchmark]
    public bool ComposedModelUse()
    {
        var element = compiled.FindElement("r", operation)!;
        var state = 0;
        return element.Content.TryAdvance(ref state, name, operation) && element.Content.TryAdvance(ref state, name, operation) &&
            element.Content.IsAccepting(state) && compiled.Attributes[0].IsValueValid("one", compiled, operation);
    }
}

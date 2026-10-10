using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class DtdValueBenchmarks
{
    private DtdValueDecoder decoder = null!;
    private readonly char[] output = new char[128];
    private readonly Dictionary<string, DtdEntityDeclaration> table = new(StringComparer.Ordinal)
    { ["outer"] = new("outer", false, " &inner; &#9; ", null, null, null, false, default),
      ["inner"] = new("inner", false, "中😀", null, null, null, false, default) };
    [GlobalSetup] public void Setup() => decoder = new(new() { TokenCharacters = null, ExpandedCharacters = null });
    [GlobalCleanup] public void Cleanup() => decoder.Dispose();
    [Benchmark] public int BorrowedDeclaration() => decoder.Decode("text &retained; &#x1F600; &#000009;", output, false).Written;
    public bool BorrowedOutputEquals(ReadOnlySpan<char> expected) => output.AsSpan(0, expected.Length).SequenceEqual(expected);
    [Benchmark] public string OwnedAttribute() => DtdValueProcessor.NormalizeAttributeDefault(" &outer; &#13; ", DtdAttributeType.NmTokens, table, new()).Value!;
}

using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Parsing.Dtd;
using Xemele.Internal.Operations;
using System.Text;

// Before a complete DTD consumer exists these are provisional framing workloads.
// Both loops charge raw scalars, normalize CRLF, check cancellation, advance the
// cursor and count normalized token scalars. No optimized matcher is introduced.
[MemoryDiagnoser, ShortRunJob]
[InvocationCount(8192)]
public class DtdFramingScanBenchmarks
{
    [Params(8, 4096)] public int Length { get; set; }
    [Params(false, true)] public bool CrLf { get; set; }
    private string input = "";
    private XmlTextDeclarationFramer framer = null!;
    private ScalarLocation baseline = null!;
    private long raw, normalized;
    private bool afterCr;
    [GlobalSetup]
    public void Setup()
    {
        input = CrLf ? string.Concat(Enumerable.Repeat(" \t\r\n", Length / 4)) : new string(' ', Length);
        Reset();
        var expected = ScalarBaseline(); var actual = IncrementalFramer();
        if (expected != actual) throw new Exception("Whitespace semantics differ.");
        ValidateCheckpoint();
    }
    public void ValidateCheckpoint()
    {
        if (baseline.Current() != framer.Append([], [], false, default).NextLocation)
            throw new Exception("Whitespace cursors differ.");
    }
    // IterationSetup forces unroll=1. The fixed 8192 calls accept at most
    // 33,554,432 raw units per iteration, well within Int32 cursor/token limits.
    // Both states restart together outside timing, including pilot/warmup stages.
    [IterationSetup]
    public void Reset()
    {
        framer?.Dispose();
        framer = new(new() { TokenCharacters = null, ExpandedCharacters = null });
        framer.Append("<?xml ", [], false, default);
        baseline = new(null);
        foreach (char c in "<?xml ") baseline.TryAdvance(new Rune(c));
        raw = normalized = 6; afterCr = false;
    }
    [Benchmark(Baseline = true)]
    public int ScalarBaseline()
    {
        foreach (char c in input)
        {
            CancellationToken.None.ThrowIfCancellationRequested();
            if (raw == long.MaxValue) throw new Exception("Budget overflow.");
            raw++;
            if (!baseline.TryAdvance(new Rune(c))) throw new Exception("Cursor overflow.");
            bool skip = afterCr && c == '\n'; afterCr = c == '\r';
            if (!skip && ++normalized > int.MaxValue) throw new Exception("Token overflow.");
            if (c is not (' ' or '\t' or '\r' or '\n')) throw new Exception("Not whitespace.");
        }
        return input.Length;
    }
    [Benchmark]
    public int IncrementalFramer()
    {
        var r = framer.Append(input, [], false, default);
        if (r.Step != TextDeclarationStep.NeedInput) throw new Exception("Unexpected decision.");
        return r.Consumed;
    }
    [GlobalCleanup] public void Cleanup() => framer.Dispose();
}

[MemoryDiagnoser, ShortRunJob]
public class DtdFramingOwnershipBenchmarks
{
    [Params("short", "long-label", "chunked", "absent", "multilingual", "malformed")]
    public string Corpus { get; set; } = "short";
    private string input = "";
    [GlobalSetup]
    public void Setup() => input = Corpus switch
    {
        "long-label" => "<?xml encoding='" + new string('a', 4096) + "'?>",
        "absent" => "<?xml-stylesheet?>",
        "multilingual" => "<?xml😀body",
        "malformed" => "<?xml encoding='9'?>",
        _ => "<?xml version='1.0' encoding='UTF-8'?>"
    };
    [Benchmark]
    public int FrameOwnedResult()
    {
        using var f = new XmlTextDeclarationFramer(new());
        if (Corpus != "chunked") return (int)f.Append(input, [], true, default).Step;
        TextDeclarationResult result = default;
        for (int i = 0; i < input.Length; i++) result = f.Append(input.AsSpan(i, 1), [], i == input.Length - 1, default);
        return (int)result.Step;
    }
}

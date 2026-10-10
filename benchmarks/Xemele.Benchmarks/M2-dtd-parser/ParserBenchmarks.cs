using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;
namespace Xemele.Benchmarks;
[MemoryDiagnoser, ShortRunJob]
public class DtdParserBenchmarks
{
    private DtdParser scanner = null!;
    private string corpus = "";
    [Params(1, 64)] public int Declarations { get; set; }
    [GlobalSetup] public void Setup()
    {
        scanner = new(DtdSubsetKind.Internal, new() { DocumentCharacters = null, TokenCharacters = null }); scanner.Append("<!--", [], false, default);
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < Declarations; i++) { builder.Append("<!ELEMENT e").Append(i).Append(" (a|b)*><!ATTLIST e").Append(i).Append(" a CDATA 'raw &later;' b (yes|no) 'yes'>"); }
        builder.Append("<!ENTITY later '中😀'><!ENTITY % p '<!NOTATION n SYSTEM &quot;x&quot;>'>"); corpus = builder.ToString();
    }
    [GlobalCleanup] public void Cleanup() => scanner.Dispose();
    [Benchmark] public int BorrowedScan() => scanner.Append("ascii中😀\r\n", [], false, default).Consumed;
    [Benchmark] public object OwnedGrammar()
    { using var p = new DtdParser(DtdSubsetKind.Internal, new()); if (p.Append(corpus, [], true, default).Step != DtdParseStep.Completed) throw new Exception("Grammar corpus."); return p.Model; }
    [Benchmark] public object ChunkedGrammar()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new());
        for (int offset = 0; offset < corpus.Length; offset += 7)
        { int count = Math.Min(7, corpus.Length - offset); var result = p.Append(corpus.AsSpan(offset, count), [], offset + count == corpus.Length, default); if (result.Step == DtdParseStep.Rejected) throw new Exception("Chunked corpus."); }
        return p.Model;
    }
}

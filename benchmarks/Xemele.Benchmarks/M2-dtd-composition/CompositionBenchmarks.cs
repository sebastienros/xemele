using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Parsing.Dtd;
namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class DtdCompositionBenchmarks
{
    private DtdModel prior = null!;
    private OperationLedger ledger = null!;
    private string external = null!;
    private static readonly Uri Uri = new("https://fixture.invalid/composed.dtd");
    [Params(32, 4096)] public int CommentScalars { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        ledger = new OperationLedger(); prior = MakePrior(ledger);
        external = "<?xml\r\nencoding='UTF-8'?><!--" + new string('x', CommentScalars) + "😀中-->%x;<!ATTLIST child a CDATA '&g;'>";
        Check(Run());
    }
    internal static DtdModel MakePrior(OperationLedger ledger)
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(null, 1, 1, null));
        if (p.Append("<!ENTITY % x '<!ELEMENT child EMPTY>'><!ENTITY g 'inside'>", [], true, default).Step != DtdParseStep.Completed) throw new Exception("prior");
        return p.Model;
    }
    [Benchmark]
    public object SeedAndFramedComposition() => Run();
    private DtdModel Run()
    {
        using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, new(Uri, 1, 1, null));
        p.SeedPriorModel(prior, default);
        using var f = new XmlTextDeclarationFramer(new(), ledger, Uri);
        var r = f.Append(external, [], true, default);
        if (r.Step != TextDeclarationStep.Declared) throw new Exception("frame");
        p.SetFramedInitialSource(r.NextLocation);
        if (p.Append(external.AsSpan(r.Consumed), [], true, default).Step != DtdParseStep.Completed) throw new Exception("parse");
        return p.Model;
    }
    [Benchmark]
    public object SeedAndModelSnapshot()
    {
        using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, new(Uri, 1, 1, null));
        p.SeedPriorModel(prior, default); p.SetFramedInitialSource(new(Uri, 1, 1, null));
        if (p.Append([], [], true, default).Step != DtdParseStep.Completed) throw new Exception("seed");
        return p.Model;
    }
    internal static void Check(object result)
    {
        var model = (DtdModel)result;
        if (model.Elements.Count != 1 || model.Elements[0].Name != "child" || model.ParameterEntities.Count != 1 || model.GeneralEntities.Count != 1 || model.Attributes.Count != 1 || model.GeneralEntities[0].ReplacementText != "inside" || !model.Elements[0].IsExternal) throw new Exception("composition oracle");
    }
}

[MemoryDiagnoser, ShortRunJob]
public class DtdFramedScanBenchmarks
{
    private DtdParser parser = null!;
    private string fragment = null!;
    [Params(32, 4096)] public int CommentScalars { get; set; }
    [GlobalSetup]
    public void Setup()
    {
        parser = new(DtdSubsetKind.External, new XmlResourceLimits { DocumentCharacters = null, ExpandedCharacters = null });
        parser.SetFramedInitialSource(new(null, 1, 1, null));
        parser.Append("<!--", [], false, default);
        fragment = new string('x', CommentScalars) + "中😀\r\n";
    }
    [Benchmark] public DtdParseStep BorrowedCommentScan() => parser.Append(fragment, [], false, default).Step;
    [GlobalCleanup] public void Cleanup() => parser.Dispose();
}

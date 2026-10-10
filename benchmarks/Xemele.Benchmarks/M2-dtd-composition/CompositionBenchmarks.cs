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
        CheckResult(Run());
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
    internal void CheckResult(object result)
    {
        var model = (DtdModel)result;
        Check(model.Elements.Count == 1 && model.Attributes.Count == 1 && model.GeneralEntities.Count == 1 && model.ParameterEntities.Count == 1 && model.Notations.Count == 0);
        var e = model.Elements[0];
        Check(e.Name == "child" && e.IsExternal && e.Location == new XmlLocation(null, 1, 1, null));
        Check(e.Content.Kind == DtdParticleKind.Empty && e.Content.Name is null && e.Content.Occurrence == DtdOccurrence.Once && e.Content.Children.Count == 0);
        var a = model.Attributes[0];
        Check(a.ElementName == "child" && a.Name == "a" && a.Type == DtdAttributeType.CData && a.Tokens.Count == 0 && a.DefaultKind == DtdDefaultKind.Value && a.DefaultValue == "&g;" && a.IsExternal && a.Location == new XmlLocation(Uri, 2, 47 + CommentScalars, null));
        var g = model.GeneralEntities[0];
        Check(g.Name == "g" && !g.IsParameter && g.ReplacementText == "inside" && g.SystemId is null && g.PublicId is null && g.NotationName is null && !g.IsExternal && g.Location == new XmlLocation(null, 1, 39, null));
        var p = model.ParameterEntities[0];
        Check(p.Name == "x" && p.IsParameter && p.ReplacementText == "<!ELEMENT child EMPTY>" && p.SystemId is null && p.PublicId is null && p.NotationName is null && !p.IsExternal && p.Location == new XmlLocation(null, 1, 1, null));
        Check(ReferenceEquals(prior.GeneralEntities[0], g) && ReferenceEquals(prior.ParameterEntities[0], p));
        var applied = DtdValueProcessor.NormalizeAttributeDefault(a.DefaultValue!, a.Type, model.GeneralEntities.ToDictionary(d => d.Name), new());
        Check(applied.Diagnostic is null && applied.Value == "inside");
        Check(ledger.Depth == 0);
        static void Check(bool condition) { if (!condition) throw new Exception("authored complete model oracle"); }
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
        fragment = "<!--" + new string('x', CommentScalars) + "中😀\r\n-->";
    }
    [Benchmark] public int BorrowedCommentScan() => (int)parser.Append(fragment, [], false, default).Step;
    [GlobalCleanup] public void Cleanup() => parser.Dispose();
}

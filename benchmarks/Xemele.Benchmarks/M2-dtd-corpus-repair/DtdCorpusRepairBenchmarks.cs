#if DTD_REPAIR_DRIVER
global using EntityBaseBenchmarks = Xemele.Benchmarks.DtdCorpusRepairBenchmarks;
#endif
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Dtd;
namespace Xemele.Benchmarks;
[MemoryDiagnoser, ShortRunJob]
public class DtdCorpusRepairBenchmarks
{
    private const string Grammar = "<!ENTITY 中 'v1'><!ATTLIST doc a1 CDATA '&中;'>";
    private const string Document = "<!DOCTYPE doc SYSTEM 'https://fixture.invalid/020.ent'><doc></doc>";
    private static readonly Uri External = new("https://fixture.invalid/020.ent");
    private readonly FixtureResolver resolver = new();
    [GlobalSetup] public void Setup() { EntityBaseProvenance.Gate("before"); Check(); }
    [GlobalCleanup] public void Cleanup() => EntityBaseProvenance.Gate("after");
    [Benchmark] public object InternalNestedModel()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new()); p.ConfigureDeclarationReading(false);
        if (p.Append(Grammar, [], true, default).Step != DtdParseStep.Completed) throw new Exception("live default oracle");
        return p.Model;
    }
    [Benchmark] public object RedirectedComposition() => XmlDocument.Parse(Document, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
    public void Check()
    {
        var m = (DtdModel)InternalNestedModel();
        if (m.Elements.Count != 0 || m.GeneralEntities.Count != 1 || m.Attributes.Count != 1 || m.ParameterEntities.Count != 0 || m.Notations.Count != 0) throw new Exception("complete internal tables");
        var e = m.GeneralEntities[0];
        if (e.Name != "中" || e.ReplacementText != "v1" || e.IsParameter || e.IsExternal || e.SystemId is not null || e.PublicId is not null || e.NotationName is not null || e.Location != new XmlLocation(null, 1, 1, null)) throw new Exception("entity oracle");
        var a = m.Attributes[0];
        if (a.ElementName != "doc" || a.Name != "a1" || a.Type != DtdAttributeType.CData || a.DefaultKind != DtdDefaultKind.Value || a.DefaultValue != "&中;" || a.Tokens.Count != 0 || a.IsExternal || a.Location != new XmlLocation(null, 1, 31, null)) throw new Exception("default oracle");
        var doc = (XmlDocument)RedirectedComposition();
        if (doc.RootElement.LocalName != "doc" || doc.RootElement.GetAttribute("a1") != "v1" || doc.RootElement.Attributes.Count != 1 || !doc.RootElement.FirstChild.IsDefault || doc.RootElement.GetTextContent() != "" || doc.DocumentType!.Name != "doc" || doc.DocumentType.SystemId != External.AbsoluteUri || doc.DocumentType.PublicId is not null || resolver.Requests != 1 || resolver.Disposals != 1) throw new Exception("complete document/resource oracle");
        m = doc.Dtd!;
        if (m.Elements.Count != 1 || m.Attributes.Count != 1 || m.GeneralEntities.Count != 0 || m.ParameterEntities.Count != 1 || m.Notations.Count != 0) throw new Exception("complete external tables");
        var element = m.Elements[0];
        if (element.Name != "doc" || !element.IsExternal || element.Content.Kind != DtdParticleKind.Mixed || element.Content.Occurrence != DtdOccurrence.Once || element.Content.Name is not null || element.Content.Children.Count != 0 || element.Location != new XmlLocation(External, 2, 1, 21)) throw new Exception("element oracle");
        a = m.Attributes[0];
        if (a.ElementName != "doc" || a.Name != "a1" || a.Type != DtdAttributeType.CData || a.Tokens.Count != 0 || a.DefaultKind != DtdDefaultKind.Value || a.DefaultValue != "v1" || !a.IsExternal || a.Location != new XmlLocation(External, 3, 13, 59)) throw new Exception("external default oracle");
        var pe = m.ParameterEntities[0];
        if (pe.Name != "e" || !pe.IsParameter || !pe.IsExternal || pe.ReplacementText != "doc" || pe.SystemId is not null || pe.PublicId is not null || pe.NotationName is not null || pe.Location != new XmlLocation(External, 1, 1, 0)) throw new Exception("parameter oracle");
        resolver.Requests = resolver.Disposals = 0;
        if (Environment.GetCommandLineArgs().Contains("--allocation")) BorrowedAllocation();
    }
    private static void BorrowedAllocation()
    {
        using var p = new DtdParser(DtdSubsetKind.External, new());
        if (p.Append("<!ENTITY % p SYSTEM 'child'><!ELEMENT%p;", [], true, default).Step != DtdParseStep.NeedEntity) throw new Exception("pending keyword-adjacent fixture");
        p.PushEntity(new("https://fixture.invalid/child"));
        for (int i = 0; i < 128; i++) if (p.Append(" \t", [], false, default).Consumed != 2) throw new Exception("warm count");
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++)
        { var r = p.Append(" \t", [], false, default); if (r.Consumed != 2 || r.Step != DtdParseStep.NeedInput || r.Diagnostic is not null || r.Entity is not null) throw new Exception("borrowed count/outcome oracle"); }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        if (bytes != 0) throw new Exception("0B borrowed primitive");
        if (p.Append("doc EMPTY>", [], true, default).Step != DtdParseStep.NeedInput || p.Append([], [], true, default).Step != DtdParseStep.Completed) throw new Exception("balanced completion");
        Console.WriteLine("DTD_REPAIR_BORROWED " + JsonSerializer.Serialize(new { iterations = 1024, utf16PerOperation = 2, bytes, coldAndOwnedExcluded = true }));
    }
    private sealed class FixtureResolver : XmlResourceResolver
    {
        // Exact original 020.ent CRLF bytes, generated once outside intervals.
        private static readonly byte[] Body = "<!ENTITY % e \"doc\">\r\n<!ELEMENT doc (#PCDATA)>\r\n<!ATTLIST%e;a1 CDATA \"v1\">\r\n"u8.ToArray();
        internal int Requests, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Requests++; if (request.Uri != External || request.Referrer is not null || request.Kind != XmlResourceKind.ExternalSubset || request.SystemId != External.AbsoluteUri || request.PublicId is not null) throw new Exception("request oracle"); return new(new Owned(this), External); }
        private sealed class Owned(FixtureResolver owner) : MemoryStream(Body, false)
        { protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); } }
    }
}

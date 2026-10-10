#if NONVALIDATING_DRIVER
global using EntityBaseBenchmarks = Xemele.Benchmarks.NonvalidatingBenchmarks;
#endif
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Parsing.Dtd;
namespace Xemele.Benchmarks;

// Reuses the verified entity-base driver/binding/provenance. The existing
// InternalNestedModel case name refers to internal PE text in an external DTD.
[MemoryDiagnoser, ShortRunJob]
public class NonvalidatingBenchmarks
{
    private static readonly Uri Parent = new("https://fixture.invalid/final/ext.dtd");
    private const string Grammar = "<!ENTITY % group '(a'><!ENTITY % close 'b)>'><!ENTITY % mode 'INCLUDE['><!ELEMENT r %group;,%close;<![%mode;<!ATTLIST r v CDATA 'ok'>]]>";
    private const string Document = "<!DOCTYPE r SYSTEM 'https://fixture.invalid/request/ext.dtd'><r>&e;</r>";
    private readonly FixtureResolver resolver = new();
    [GlobalSetup] public void Setup() { EntityBaseProvenance.Gate("before"); Check(); }
    [GlobalCleanup] public void Cleanup() => EntityBaseProvenance.Gate("after");
    [Benchmark] public object InternalNestedModel()
    {
        using var p = new DtdParser(DtdSubsetKind.External, new(), Parent);
        if (p.Append(Grammar, [], true, default).Step != DtdParseStep.Completed) throw new Exception("complete expanded grammar oracle");
        return p.Model;
    }
    [Benchmark] public object RedirectedComposition() => XmlDocument.Parse(Document, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
    public void Check()
    {
        var model = (DtdModel)InternalNestedModel();
        if (model.Elements.Count != 1 || model.Attributes.Count != 1 || model.ParameterEntities.Count != 3 || model.GeneralEntities.Count != 0 || model.Notations.Count != 0) throw new Exception("complete table oracle");
        var e = model.Elements[0];
        if (e.Name != "r" || !e.IsExternal || e.Location != new XmlLocation(Parent, 1, Grammar.IndexOf("<!ELEMENT", StringComparison.Ordinal) + 1, null) || e.Content.Kind != DtdParticleKind.Sequence || e.Content.Name is not null || e.Content.Occurrence != DtdOccurrence.Once || e.Content.Children.Count != 2) throw new Exception("complete element oracle");
        for (int i = 0; i < 2; i++) { var c = e.Content.Children[i]; if (c.Kind != DtdParticleKind.Name || c.Name != (i == 0 ? "a" : "b") || c.Occurrence != DtdOccurrence.Once || c.Children.Count != 0) throw new Exception("child oracle"); }
        var a = model.Attributes[0];
        if (a.ElementName != "r" || a.Name != "v" || a.Type != DtdAttributeType.CData || a.Tokens.Count != 0 || a.DefaultKind != DtdDefaultKind.Value || a.DefaultValue != "ok" || !a.IsExternal || a.Location != new XmlLocation(Parent, 1, Grammar.IndexOf("v CDATA", StringComparison.Ordinal) + 1, null)) throw new Exception("complete default oracle");
        string[] names = ["group", "close", "mode"], replacements = ["(a", "b)>", "INCLUDE["];
        for (int i = 0; i < names.Length; i++) { var p = model.ParameterEntities[i]; if (p.Name != names[i] || !p.IsParameter || !p.IsExternal || p.ReplacementText != replacements[i] || p.SystemId is not null || p.PublicId is not null || p.NotationName is not null || p.Location != new XmlLocation(Parent, 1, Grammar.IndexOf("<!ENTITY % " + names[i], StringComparison.Ordinal) + 1, null)) throw new Exception("complete parameter oracle"); }
        var document = (XmlDocument)RedirectedComposition();
        if (document.RootElement.LocalName != "r" || document.RootElement.GetTextContent() != "right!" || document.RootElement.GetAttribute("v") != "ok" || document.DocumentType!.Name != "r" || resolver.Requests != 3 || resolver.Disposals != 3) throw new Exception("complete document/resource oracle");
        resolver.Requests = resolver.Disposals = 0;
        if (Environment.GetCommandLineArgs().Contains("--allocation")) BorrowedAllocation();
    }
    private static void BorrowedAllocation()
    {
        using var parser = new DtdParser(DtdSubsetKind.External, new(), Parent);
        if (parser.Append("<!ENTITY % p SYSTEM 'child'><!ELEMENT r %p;", [], true, default).Step != DtdParseStep.NeedEntity) throw new Exception("pending borrowed fixture");
        parser.PushEntity(new("https://fixture.invalid/child.dtd"));
        for (int i = 0; i < 128; i++) if (parser.Append(" \t", [], false, default).Consumed != 2) throw new Exception("warm borrowed count");
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++)
        {
            var r = parser.Append(" \t", [], false, default);
            if (r.Consumed != 2 || r.Step != DtdParseStep.NeedInput || r.Diagnostic is not null || r.Entity is not null) throw new Exception("borrowed interval oracle");
        }
        long borrowed = GC.GetAllocatedBytesForCurrentThread() - start;
        if (borrowed != 0) throw new Exception("0B borrowed append target");
        if (parser.Append("EMPTY>", [], true, default).Step != DtdParseStep.NeedInput || parser.Append([], [], true, default).Step != DtdParseStep.Completed) throw new Exception("borrowed finalization");
        Console.WriteLine("NONVALIDATING_BORROWED " + JsonSerializer.Serialize(new { iterations = 1024, utf16PerOperation = 2, bytes = borrowed, coldAndOwnedExcluded = true }));
    }
    private sealed class FixtureResolver : XmlResourceResolver
    {
        private static readonly byte[] Subset = Encoding.UTF8.GetBytes("<!ENTITY % p SYSTEM '../payload/close.dtd'><!ELEMENT r (#PCDATA)><!ATTLIST r v CDATA 'ok'><!ENTITY e %p;");
        private static readonly byte[] Closing = "SYSTEM 'body.ent'>"u8.ToArray(), Body = "right!"u8.ToArray();
        internal int Requests, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Requests++; byte[] body; Uri final;
            if (request.Uri.AbsoluteUri == "https://fixture.invalid/request/ext.dtd")
            { if (request.Kind != XmlResourceKind.ExternalSubset || request.Referrer is not null || request.SystemId != request.Uri.AbsoluteUri || request.PublicId is not null) throw new Exception("subset request"); body = Subset; final = Parent; }
            else if (request.Uri.AbsoluteUri == "https://fixture.invalid/payload/close.dtd")
            { if (request.Kind != XmlResourceKind.ExternalEntity || request.Referrer != Parent || request.SystemId != "../payload/close.dtd" || request.PublicId is not null) throw new Exception("PE request"); body = Closing; final = new("https://fixture.invalid/actual/close.dtd"); }
            else
            { if (request.Uri.AbsoluteUri != "https://fixture.invalid/final/body.ent" || request.Kind != XmlResourceKind.ExternalEntity || request.Referrer != Parent || request.SystemId != "body.ent" || request.PublicId is not null) throw new Exception("declaration opener base"); body = Body; final = new("https://fixture.invalid/content/body.ent"); }
            return new(new OwnedStream(this, body), final);
        }
        private sealed class OwnedStream(FixtureResolver owner, byte[] body) : MemoryStream(body, false)
        { protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); } }
    }
}

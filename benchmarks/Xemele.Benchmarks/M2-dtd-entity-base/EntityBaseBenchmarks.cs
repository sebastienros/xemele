using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Parsing.Dtd;

namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class EntityBaseBenchmarks
{
    private static readonly Uri Primary = new("https://fixture.invalid/primary/document.xml");
    private static readonly Uri Final = new("https://fixture.invalid/final/external.dtd");
    private const string Declarations = "<!ENTITY % x \"<!ENTITY e SYSTEM 'body.ent'>\"><!ENTITY % y '&#37;x;'>%y;";
    private const string Document = "<!DOCTYPE r SYSTEM 'https://fixture.invalid/request/external.dtd' [<!ENTITY % x \"<!ENTITY e SYSTEM 'body.ent'>\"><!ENTITY % y '&#37;x;'>]><r>&e;</r>";
    private readonly FixtureResolver resolver = new();

    [GlobalSetup]
    public void Setup() => Check();

    [Benchmark]
    public object InternalNestedModel()
    {
        using var parser = new DtdParser(DtdSubsetKind.Internal, new(), Primary);
        if (parser.Append(Declarations, [], true, default).Step != DtdParseStep.Completed) throw new Exception("internal fixture");
        return parser.Model;
    }

    [Benchmark]
    public object RedirectedComposition() => XmlDocument.Parse(Document, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });

    public void Check()
    {
        var model = (DtdModel)InternalNestedModel();
        if (model.Elements.Count != 0 || model.Attributes.Count != 0 || model.Notations.Count != 0 || model.GeneralEntities.Count != 1 || model.ParameterEntities.Count != 2)
            throw new Exception("table counts");
        var entity = model.GeneralEntities[0];
        if (entity.Name != "e" || entity.IsParameter || entity.IsExternal || entity.ReplacementText is not null || entity.SystemId != "body.ent" || entity.PublicId is not null || entity.NotationName is not null || entity.Location != new XmlLocation(Primary, 1, 1, null))
            throw new Exception("complete declaration oracle");
        var document = (XmlDocument)RedirectedComposition();
        if (document.RootElement.LocalName != "r" || document.RootElement.GetTextContent() != "right!" || resolver.LastReferrer != Final)
            throw new Exception("redirected composition oracle");
    }
    private sealed class FixtureResolver : XmlResourceResolver
    {
        private static readonly byte[] Subset = Encoding.UTF8.GetBytes("<?xml encoding='UTF-8'?>%y;");
        private static readonly byte[] Body = "right!"u8.ToArray();
        internal Uri? LastReferrer;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Uri.AbsoluteUri == "https://fixture.invalid/request/external.dtd") return new(new MemoryStream(Subset, false), Final);
            if (request.Uri.AbsoluteUri != "https://fixture.invalid/final/body.ent" || request.Referrer != Final || request.SystemId != "body.ent" || request.PublicId is not null || request.Kind != XmlResourceKind.ExternalEntity)
                throw new Exception("exact resource oracle");
            LastReferrer = request.Referrer;
            return new(new MemoryStream(Body, false), new("https://fixture.invalid/content/body.ent"));
        }
    }
}

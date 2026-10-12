using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdPublicationCheckpoints;

internal sealed class PublicationFixture
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly Uri Main = new("https://example.test/main.xsd"), Child = new("https://example.test/child.xsd");
    internal static readonly string[] Routes = ["legacy-owned-graph", "shared-principal-owned-graph", "unused-model-checkpoint", "unused-attribute-checkpoint", "zero-self-redefine-checkpoints", "self-redefine-checkpoints", "aggregate-scalar-refusal", "stream-scalar-expansion-exact-cap"];
    private readonly string[] sources;
    private readonly string original, included;
    private readonly byte[] streamPrincipal;
    private readonly XmlDocument legacy;
    private readonly long aggregateScalars;
    private int consumed;
    internal PublicationFixture()
    {
        original = Wrap("<xs:group name='g'><xs:sequence><xs:element name='x' type='xs:int'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute name='q' type='xs:QName' fixed='p:v' xmlns:p='urn:old'/></xs:attributeGroup>");
        included = Wrap("<xs:annotation><xs:documentation>é😀&amp;</xs:documentation></xs:annotation><xs:element name='r'/>");
        sources = [
            Wrap("<xs:element name='r' type='xs:int'/>"),
            Wrap("<xs:element name='r' type='xs:int'/>"),
            Wrap("<xs:group name='g'><xs:sequence><xs:element name='x' type='xs:int'/><xs:element name='x' type='xs:int'/></xs:sequence></xs:group>"),
            Wrap("<xs:attribute name='a' type='xs:int' fixed='1'/><xs:attributeGroup name='ag'><xs:attribute ref='a' fixed='01'/><xs:attribute name='id' type='xs:ID'/></xs:attributeGroup>"),
            Wrap("<xs:redefine schemaLocation='child.xsd'><xs:group name='g'><xs:sequence><xs:element name='x' type='xs:int'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute name='q' type='xs:QName' fixed='p:v' xmlns:p='urn:old'/></xs:attributeGroup></xs:redefine>"),
            Wrap("<xs:redefine schemaLocation='child.xsd'><xs:group name='g'><xs:sequence><xs:group ref='g'/><xs:element name='new'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attributeGroup ref='ag'/><xs:attribute name='new'/></xs:attributeGroup></xs:redefine>"),
            Wrap("<xs:include schemaLocation='child.xsd'/><xs:annotation><xs:documentation>😀&#x1F600;</xs:documentation></xs:annotation>"),
            Wrap("<xs:include schemaLocation='child.xsd'/><xs:annotation><xs:documentation>😀&#x1F600;</xs:documentation></xs:annotation>")
        ];
        legacy = XmlDocument.Parse(sources[0]); streamPrincipal = Encoding.UTF8.GetBytes(sources[7]);
        aggregateScalars = sources[7].EnumerateRunes().Count() + included.EnumerateRunes().Count();
    }
    private static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    internal object Run(int route)
    {
        if (route == 0)
        {
            var options = new SchemaCompileOptions();
            return Consume(new XmlSchemaCompiler().Compile(legacy, options, new SchemaCompileOperation(options)), route);
        }
        var limits = route >= 6 ? new XmlResourceLimits { DocumentCharacters = aggregateScalars - (route == 6 ? 1 : 0), ExpandedCharacters = 2 } : new XmlResourceLimits();
        var context = new SchemaCompileContext(new() { BaseUri = Main, Limits = limits, Resolver = new Resolver(route is 4 or 5 ? original : included) });
        XmlDocument document;
        if (route == 7) { using var input = new MemoryStream(streamPrincipal, writable: false); document = context.Parse(input).Document; }
        else document = context.Parse(sources[route]).Document;
        try
        {
            var graph = new XmlSchemaCompiler().Compile(document, context);
            if (route == 6) throw new InvalidOperationException("Expected aggregate quota refusal.");
            return Consume(graph, route);
        }
        catch (SchemaCompileException error) when (route == 6)
        {
            if (error.Diagnostics[^1].Code != "SCHEMA.Limit" || context.Operation.IsComplete) throw new InvalidOperationException("Wrong refusal.");
            consumed += error.Diagnostics.Count; return error;
        }
    }
    private XsdCompiledSchema Consume(XsdCompiledSchema graph, int route)
    {
        int checkpoints = graph.Types.Count(t => t.Name.LocalName.StartsWith("#model-group:", StringComparison.Ordinal) || t.Name.LocalName.StartsWith("#attribute-group:", StringComparison.Ordinal));
        int expected = route is 4 or 5 ? 4 : route is 2 or 3 ? 1 : 0;
        if (checkpoints != expected) throw new InvalidOperationException("Checkpoint count mismatch.");
        if (route == 7 && graph.Elements.Single().Location.SourceUri != Child) throw new InvalidOperationException("Include identity mismatch.");
        consumed += graph.Types.Count + graph.Elements.Count + graph.Attributes.Count; return graph;
    }
    internal int Consume() => consumed;
    private sealed class Resolver(string text) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return new(new MemoryStream(Encoding.UTF8.GetBytes(text)), Child); }
    }
}

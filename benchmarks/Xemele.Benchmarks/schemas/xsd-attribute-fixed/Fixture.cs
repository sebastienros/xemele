using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdAttributeFixed;

internal sealed class AttributeFixedFixture
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static readonly string[] Routes = ["typed-list-short-equivalent", "typed-list-short-unequal", "typed-list-long-equivalent", "typed-list-long-unequal", "typed-token-multilingual-equivalent", "typed-token-multilingual-unequal", "typed-qname-equivalent-contexts", "typed-qname-unequal-contexts", "public-short-equivalent", "public-short-unequal", "public-long-multilingual-equivalent", "public-long-multilingual-unequal"];
    private readonly XsdCompiledSchema[] graphs = new XsdCompiledSchema[8];
    private readonly XsdTypeDefinition[] targets = new XsdTypeDefinition[8];
    private readonly XmlDocument[] documents = new XmlDocument[4];
    private readonly XmlFacetEvaluator facets = new();
    private readonly SchemaCompileOptions options = new() { MaxOperations = null };
    private int consumed;
    internal AttributeFixedFixture()
    {
        var list = string.Join(" ", Enumerable.Range(1, 32));
        var padded = string.Join("   ", Enumerable.Range(1, 32).Select(n => "0" + n + ".00"));
        var multilingual = string.Join(" ", Enumerable.Repeat("café 東京", 128));
        var paddedMultilingual = "  " + multilingual.Replace(" ", "   ", StringComparison.Ordinal) + "  ";
        string[] sources = [Source("L", "1 2 3", "01.0 2.00 03"), Source("L", "1 2 3", "1 2 4"),
            Source("L", list, padded), Source("L", list, padded + " 33"),
            Source("xs:token", "  café   東京  ", "café 東京"), Source("xs:token", "café 東京", "café 京都"),
            Source("xs:QName", "p:v", "q:v", "xmlns:p='urn:one'", "xmlns:q='urn:one'"),
            Source("xs:QName", "p:v", "p:v", "xmlns:p='urn:one'", "xmlns:p='urn:two'"),
            Source("xs:decimal", "1", "01.00"), Source("xs:decimal", "1", "2"),
            Source("xs:token", multilingual, paddedMultilingual), Source("xs:token", multilingual, paddedMultilingual + " 不同")];
        for (var i = 0; i < sources.Length; i++)
        {
            var document = XmlDocument.Parse(sources[i]);
            if (i >= 8)
            {
                documents[i - 8] = document;
                continue;
            }
            var op = new SchemaCompileOperation(options);
            var graph = new XmlSchemaCompiler().Compile(document, options, op);
            graphs[i] = graph with
            {
                Datatypes = new XmlDatatypeRegistry(graph.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), op)
            };
            targets[i] = graph.Types.Single(t => t.Name == new SchemaName("", "D"));
        }
    }
    private static string Source(string type, string basis, string derived, string bc = "", string dc = "") =>
        $"<xs:schema xmlns:xs='{Xs}'><xs:simpleType name='L'><xs:list itemType='xs:decimal'/></xs:simpleType><xs:complexType name='B'><xs:attribute name='a' type='{type}' fixed='{basis}' {bc}/></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:attribute name='a' type='{type}' fixed='{derived}' {dc}/></xs:restriction></xs:complexContent></xs:complexType><xs:element name='root' type='D'/></xs:schema>";
    internal object RunOwned(int route)
    {
        var valid = route % 2 == 0;
        try
        {
            object result = route < 8 ? new XsdContentCompiler().Compile(graphs[route], targets[route], new SchemaCompileOperation(options), facets)
                : XmlSchema.Compile(documents[route - 8], options);
            if (!valid)
            {
                throw new InvalidOperationException("Unequal fixed attributes were accepted: " + Routes[route]);
            }

            consumed++;
            return result;
        }
        catch (SchemaCompileException error) when (!valid)
        {
            if (error.Diagnostics.Count == 0 || error.Diagnostics.Any(d => d.Code != "XSD.SchemaRestriction"))
            {
                throw new InvalidOperationException("Unexpected diagnostic: " + Routes[route]);
            }

            consumed += error.Diagnostics.Count + 1;
            return error;
        }
    }
    internal int Consume() => consumed;
}

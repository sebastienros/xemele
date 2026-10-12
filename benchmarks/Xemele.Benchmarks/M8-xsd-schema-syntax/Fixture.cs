using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdSchemaSyntax;
internal sealed class SyntaxFixture
{
    internal static readonly string[] Routes = ["compiler-lang-short", "compiler-lang-long-whitespace", "compiler-lang-multilingual-payload", "compiler-lang-empty-invalid", "compiler-group-legal", "compiler-global-occurs-invalid", "path-dot", "path-long-whitespace", "path-multilingual-name", "path-self-invalid", "path-descendant-axis-invalid", "facade-owned-composition"];
    private readonly XmlDocument[] documents;
    private readonly string[] paths = [".", "." + new string(' ',8192) + "//child::row", ".//child::根😀/attribute::é", "self::node()", "descendant-or-self::node()/child::row"];
    private readonly SchemaNamespaceContext context = new(new Dictionary<string,string>(), "", null);
    private readonly string facade;
    private int consumed;
    internal SyntaxFixture()
    {
        documents = new[] { Wrap("<xs:annotation><xs:documentation xml:lang='en-US'/></xs:annotation>"), Wrap("<xs:annotation><xs:documentation xml:lang='" + new string(' ',8192) + "en-US" + new string(' ',8192) + "'/></xs:annotation>"), Wrap("<xs:annotation><xs:documentation xml:lang='zh-Hant'>根😀é<opaque xmlns='urn:payload'/></xs:documentation></xs:annotation>"), Wrap("<xs:annotation><xs:documentation xml:lang=''/></xs:annotation>"), Wrap("<xs:group name='G'><xs:sequence><xs:element name='row'/></xs:sequence></xs:group>"), Wrap("<xs:group name='G' minOccurs='1'><xs:sequence><xs:element name='row'/></xs:sequence></xs:group>") }.Select(s => XmlDocument.Parse(s)).ToArray();
        facade = Wrap("<xs:annotation><xs:documentation xml:lang='en-US'>根😀</xs:documentation></xs:annotation><xs:element name='root'><xs:unique name='K'><xs:selector xpath='.//child::row'/><xs:field xpath='attribute::id'/></xs:unique></xs:element>");
    }
    private static string Wrap(string body) => "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>" + body + "</xs:schema>";
    internal object Run(int route)
    {
        bool invalid = route is 3 or 5 or 9 or 10;
        try
        {
            object result;
            if (route < 6) { var graph = new XmlSchemaCompiler().Compile(documents[route],new(),new SchemaCompileOperation()); consumed += graph.Types.Count; result = graph; }
            else if (route < 11) { var path = IdentityXPath.Compile(paths[route-6],true,context,new SchemaCompileOperation(),default); consumed += path.SelectsNonElements ? 2 : 1; result = path; }
            else { var schema = XmlSchema.Compile(facade); consumed += schema.Validate("<root/>").IsValid ? 1 : throw new InvalidOperationException("Facade validation failed."); result = schema; }
            if (invalid) throw new InvalidOperationException("Expected syntax rejection.");
            return result;
        }
        catch (SchemaCompileException error) when (invalid)
        {
            string expected = route < 6 ? "XSD.SchemaRestriction" : "XSD.Identity";
            if (error.Diagnostics.Count == 0 || error.Diagnostics[0].Code != expected) throw new InvalidOperationException("Wrong syntax diagnostic.");
            consumed += error.Diagnostics.Count; return error;
        }
    }
    internal int Consume() => consumed;
}

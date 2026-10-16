using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdValidate;

internal sealed class EvaluationFixture
{
    internal const string Source = """
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
<xs:element name="root"><xs:complexType><xs:sequence><xs:element name="row" maxOccurs="unbounded">
<xs:complexType><xs:sequence><xs:element name="value" type="xs:int" default="01" minOccurs="0"/><xs:element name="fixed" type="xs:int" fixed="01" minOccurs="0"/></xs:sequence><xs:attribute name="n" type="xs:int" default="1"/><xs:attribute name="id" type="xs:ID"/><xs:attribute name="ref" type="xs:IDREF"/></xs:complexType>
</xs:element></xs:sequence></xs:complexType><xs:unique name="U"><xs:selector xpath="row"/><xs:field xpath="@n"/></xs:unique></xs:element>
<xs:element name="open"/><xs:element name="number" type="xs:int" nillable="true"/>
</xs:schema>
""";
    internal const string Instance = "<root><row n='1'/><row n='2'/></root>";
    internal const string Invalid = "<root><row n='bad'/></root>";
    internal static readonly string LongInstance = "<root>" + string.Concat(Enumerable.Range(1, 64).Select(i => $"<row n='{i}'/>")) + "</root>";
    internal static readonly string[] Routes = ["valid-owned", "invalid-diagnostic", "typed-default", "typed-duplicate", "document-ids", "long-owned", "wildcard-scopes", "nil-owned", "work-refusal"];
    private readonly XsdCompiledSchema schema;
    private readonly Dictionary<SchemaName, XsdContentProgram> programs = [];
    private readonly XsdIdentityProgram identity;
    private readonly XmlSchemaValidator evaluator = new();
    private readonly XmlFacetEvaluator facets = new();
    private readonly XsdContentMatcher matcher = new();
    private readonly XsdIdentityFactory factory = new();
    private readonly XmlDocument[] documents;
    private int consumed;
    internal EvaluationFixture()
    {
        var compile = new SchemaCompileContext();
        var original = new XmlSchemaCompiler().Compile(compile.Parse(Source).Document, compile.Options, compile.Operation);
        schema = original with
        {
            Datatypes = new XmlDatatypeRegistry(original.SimpleTypes.Where(t => t.Name.NamespaceUri != "http://www.w3.org/2001/XMLSchema"), compile.Operation)
        };
        foreach (var type in schema.Types)
        {
            programs.Add(type.Name, new XsdContentCompiler().Compile(schema, type, compile.Operation));
        }

        identity = new XsdIdentityCompiler().Compile(schema, compile.Operation);
        documents = [XmlDocument.Parse(Instance), XmlDocument.Parse(Invalid), XmlDocument.Parse("<root><row><value/><fixed>1</fixed></row></root>"), XmlDocument.Parse("<root><row/><row n='01'/></root>"), XmlDocument.Parse("<root><row n='1' id='a'/><row n='2' ref='a'/></root>"), XmlDocument.Parse(LongInstance), XmlDocument.Parse("<open><unknown><nested/></unknown></open>"), XmlDocument.Parse("<number xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/>"), XmlDocument.Parse(Instance)];
    }
    internal ValidationResult Run(int route)
    {
        var result = evaluator.Validate(schema, documents[route], new()
        {
            MaxOperations = route == 8 ? 1 : null
        }, programs, matcher, facets, factory, identity);
        var expectedValid = route is not (1 or 3 or 8);
        if (result.IsValid != expectedValid || result.IsComplete != (route != 8))
        {
            throw new InvalidOperationException("Independent route expectation failed: " + Routes[route]);
        }

        consumed += result.Diagnostics.Count + 1;
        return result;
    }
    internal int Consume() => consumed;
}

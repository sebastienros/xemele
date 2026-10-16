using Xemele;
using Xemele.Validation;
const string xs = "http://www.w3.org/2001/XMLSchema";
foreach (string lexical in new[] { "2", "01.0" })
{
    var context = new SchemaCompileContext();
    string source = $"<xs:schema xmlns:xs='{xs}'><xs:complexType name='B'><xs:sequence><xs:element name='v' type='xs:decimal' fixed='1'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element name='v' type='xs:decimal' fixed='{lexical}'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType></xs:schema>";
    var declarations = new XmlSchemaCompiler().Compile(context.Parse(source).Document, context.Options, context.Operation);
    var schema = declarations with
    {
        Datatypes = new XmlDatatypeRegistry(declarations.SimpleTypes.Where(t => t.Name.NamespaceUri != xs), context.Operation)
    };
    var type = schema.Types.Single(t => t.Name == new SchemaName("", "D"));
    var facets = new XmlFacetEvaluator();
    try
    {
        var program = new XsdContentCompiler().Compile(schema, type, context.Operation, facets);
        if (lexical == "2")
            throw new InvalidOperationException("Unequal fixed values accepted.");
        var cursor = new XsdContentMatcher().CreateCursor(program);
        if (!cursor.Accept(new("", "v"), context.Operation, out var id, out _) || id is null || !cursor.Complete(context.Operation))
            throw new InvalidOperationException("Accepted content program changed.");
    }
    catch (SchemaCompileException failure) when (lexical == "2" && failure.Diagnostics.Any(d => d.Code == "XSD.SchemaRestriction")) { }
}
Console.WriteLine("PASS: explicit decimal rejection/equivalence and real content cursor");

using Xemele;
using Xemele.Validation;
const string xs = "http://www.w3.org/2001/XMLSchema";
var compile = new SchemaCompileContext();
var original = new XmlSchemaCompiler().Compile(compile.Parse($"""
<xs:schema xmlns:xs="{xs}"><xs:element name="root"><xs:complexType><xs:sequence>
<xs:element name="row" maxOccurs="unbounded"><xs:complexType><xs:attribute name="n" type="xs:int" default="1"/></xs:complexType></xs:element>
</xs:sequence></xs:complexType><xs:unique name="U"><xs:selector xpath="row"/><xs:field xpath="@n"/></xs:unique></xs:element></xs:schema>
""").Document, compile.Options, compile.Operation);
var schema = original with { Datatypes = new XmlDatatypeRegistry(original.SimpleTypes.Where(t=>t.Name.NamespaceUri!=xs),compile.Operation) };
var programs = new Dictionary<SchemaName,XsdContentProgram>();
foreach (var type in schema.Types) programs.Add(type.Name,new XsdContentCompiler().Compile(schema,type,compile.Operation));
var identity = new XsdIdentityCompiler().Compile(schema,compile.Operation);
var evaluator = new XmlSchemaValidator(); var facets = new XmlFacetEvaluator();
ValidationResult Run(string xml) => evaluator.Validate(schema,XmlDocument.Parse(xml),new(),programs,new XsdContentMatcher(),facets,new XsdIdentityFactory(),identity);
if (!Run("<root><row/><row n='2'/></root>").IsValid) return 1;
if (Run("<root><row/><row n='01'/></root>").IsValid) return 2;
if (Run("<root><row n='bad'/></root>").IsValid) return 3;
Console.WriteLine("Internal pre-facade composition: typed defaults, unique values and invalid lexical values validated. No public schema publication claim.");
return 0;

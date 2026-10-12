using Xemele;
using Xemele.Validation;

const string xs="http://www.w3.org/2001/XMLSchema";
var compile=new SchemaCompileContext();
var schema=new XmlSchemaCompiler().Compile(compile.Parse($"""
<xs:schema xmlns:xs="{xs}"><xs:element name="root">
<xs:key name="K"><xs:selector xpath="row"/><xs:field xpath="@id"/></xs:key>
<xs:keyref name="R" refer="K"><xs:selector xpath="use"/><xs:field xpath="@id"/></xs:keyref>
</xs:element></xs:schema>
""").Document,compile.Options,compile.Operation);
IXsdIdentityCompiler compiler=new XsdIdentityCompiler();
var program=compiler.Compile(schema,compile.Operation);
var registry=new XmlDatatypeRegistry(schema.SimpleTypes.Where(d => d.Name.NamespaceUri != "http://www.w3.org/2001/XMLSchema"),compile.Operation);
var facets=new XmlFacetEvaluator();
var integer=registry.FindType(new(xs,"integer"))!;
var context=new SchemaNamespaceContext(new Dictionary<string,string>(),"",null);
bool Validate(string xml)
{
    var doc=XmlDocument.Parse(xml);var op=new SchemaValidationOperation();
    IXsdIdentitySession session=new XsdIdentityFactory().Create(schema,program,doc);
    session.EnterElement(doc.RootElement,schema.Elements[schema.GlobalElements[new("","root")]],op);
    foreach(var node in doc.RootElement.Children)
    {
        session.EnterElement(node,null,op); // Wildcard nodes are still visited.
        foreach(var a in node.Attributes)
        {
            if(!facets.TryValidate(integer,a.Value,context,registry,op,out var value,out var error))
                throw new InvalidOperationException(error?.Message);
            session.Attribute(node,new(a.NamespaceUri,a.LocalName),new(true,integer.Name,value),op);
        }
        session.LeaveElement(node,new(true,new(xs,"anyType"),null),op);
    }
    session.LeaveElement(doc.RootElement,new(true,new(xs,"anyType"),null),op);
    session.Complete(op);var result=op.Complete();
    if(!result.IsComplete)throw new InvalidOperationException("Stopped consumer.");
    return result.IsValid;
}
if(!Validate("<root><use id='1'/><row id='01'/></root>"))return 1;
if(Validate("<root><row id='01'/><row id='1'/></root>"))return 2;
if(Validate("<root><use id='2'/><row id='1'/></root>"))return 3;
Console.WriteLine("Real compiler + facets + prepared identity hooks: forward reference valid, typed duplicate and missing reference invalid.");
return 0;

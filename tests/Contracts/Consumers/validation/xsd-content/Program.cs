using Xemele;
using Xemele.Validation;
const string Source = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='urn:orders' xmlns:o='urn:orders'><xs:complexType name='Order'><xs:sequence><xs:element name='item' type='xs:string' maxOccurs='unbounded'/><xs:any namespace='##other' processContents='lax' minOccurs='0' maxOccurs='unbounded'/></xs:sequence></xs:complexType><xs:element name='order' type='o:Order'/></xs:schema>";
var context = new SchemaCompileContext();
var source = context.Parse(Source);
IXsdCompiler compiler = new XmlSchemaCompiler();
var schema = compiler.Compile(source.Document, context.Options, context.Operation);
var root = schema.Elements[schema.GlobalElements[new("urn:orders", "order")]];
var type = schema.Types.Single(t => t.Name == root.Type);
IXsdContentCompiler contentCompiler = new XsdContentCompiler();
var program = contentCompiler.Compile(schema, type, context.Operation);
IXsdContentMatcher matcher = new XsdContentMatcher();
var cursor = matcher.CreateCursor(program);
var other = matcher.CreateCursor(program);
var instance = new SchemaCompileContext().Parse("<o:order xmlns:o='urn:orders' xmlns:e='urn:external'><item>one</item><item>two</item><e:extra/></o:order>").Document;
int items = 0, wildcards = 0;
foreach (var child in instance.RootElement.Children)
{
    if (child.Kind != XmlNodeKind.Element)
        continue;
    if (!cursor.Accept(new(child.NamespaceUri, child.LocalName), context.Operation, out var id, out var policy))
        throw new Exception("Valid instance sequence rejected.");
    if (id is int declaration)
    {
        if (schema.Elements[declaration].Name != new SchemaName("", "item"))
            throw new Exception("Local declaration identity lost.");
        items++;
    }
    else
    {
        if (policy != XsdProcessContents.Lax)
            throw new Exception("Wildcard policy lost.");
        wildcards++;
    }
}
if (!cursor.Complete(context.Operation) || items != 2 || wildcards != 1 || other.Complete(context.Operation)) throw new Exception("Independent cursor/end condition mismatch.");
source.Document.RootElement.SetAttribute("targetNamespace", "changed");
if (!other.Accept(new("", "item"), context.Operation, out _, out _) || !other.Complete(context.Operation)) throw new Exception("Program retained mutable schema document.");
if (typeof(XmlDocument).Assembly.GetTypes().Any(t => t.Namespace is string ns && (ns.Contains("Html", StringComparison.OrdinalIgnoreCase) || ns.StartsWith("Xemele.Css", StringComparison.Ordinal)))) throw new Exception("XML-only consumer loaded unrelated/later dependencies.");
Console.WriteLine("PASS: real XML compiler/content pipeline, owned immutable program, two isolated cursors, exact local declaration/wildcard policy, instance termination; runtime " + Environment.Version);

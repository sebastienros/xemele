using Xemele;
using Xemele.Validation;
const string source = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:group name='g'><xs:sequence><xs:element name='node'><xs:complexType><xs:group ref='g' minOccurs='0'/></xs:complexType></xs:element></xs:sequence></xs:group><xs:element name='root'><xs:complexType><xs:group ref='g'/></xs:complexType></xs:element></xs:schema>";
const string valid = "<root><node><node><node/></node></node></root>";
foreach (var schema in new[] { XmlSchema.Compile(source), XmlSchema.Compile(XmlDocument.Parse(source)) })
{
    var result = schema.Validate(valid);
    if (!result.IsComplete || !result.IsValid) throw new Exception("Finite recursive validation failed.");
    result = schema.Validate("<root><node><wrong/></node></root>");
    if (!result.IsComplete || result.IsValid) throw new Exception("Recursive invalid neighbor was accepted or refused.");
}
using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(source));
using var instance = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(valid));
if (!XmlSchema.Compile(input).Validate(instance).IsValid || !input.CanRead || !instance.CanRead) throw new Exception("Recursive stream consumer failed.");
if (Environment.GetEnvironmentVariable("RECURSIVE_XML_ONLY") == "1" && typeof(XmlSchema).Assembly.GetType("Xemele.HtmlDocument") is not null) throw new Exception("XML-only consumer includes HTML.");
Console.WriteLine("Recursive normal/XML-only consumer passed.");

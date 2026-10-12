using System.Text;
using Xemele;
using Xemele.Validation;
const string Ns = "http://www.w3.org/2001/XMLSchema";
string principal = $"<xs:schema xmlns:xs='{Ns}'><xs:include schemaLocation='urn:child'/><xs:attributeGroup name='unused'><xs:attribute name='a' type='xs:int'/></xs:attributeGroup></xs:schema>";
string child = $"<xs:schema xmlns:xs='{Ns}'><xs:group name='unused'><xs:sequence><xs:element name='x' type='xs:int'/></xs:sequence></xs:group></xs:schema>";
var context = new SchemaCompileContext(new() { Limits = new() { DocumentCharacters = principal.Length + child.Length }, Resolver = new Resolver(child) });
var input = context.Parse(principal);
var graph = new XmlSchemaCompiler().Compile(input.Document, context);
var carriers = graph.Types.Where(t => t.Name.LocalName is "#attribute-group:unused" or "#model-group:unused").ToArray();
if (carriers.Length != 2 || carriers.Any(t => !t.Anonymous)) throw new Exception("Missing publication checkpoints.");
if (carriers.Single(t => t.Name.LocalName == "#attribute-group:unused").Attributes.Single().DeclarationId != 0) throw new Exception("Local attribute ID changed.");
if (graph.Elements.Single().Location.SourceUri != new Uri("urn:child")) throw new Exception("Include source lost.");
input.Document.RootElement.SetAttribute("targetNamespace", "changed");
if (carriers.Any(t => t.Name.NamespaceUri != "")) throw new Exception("Checkpoint retained mutable source.");
#if XML_ONLY
if (typeof(XmlDocument).Assembly.GetTypes().Any(t => t.Namespace is string ns && (ns.Contains("Html", StringComparison.OrdinalIgnoreCase) || ns.StartsWith("Xemele.Css", StringComparison.Ordinal)))) throw new Exception("XML-only consumer loaded a later dependency.");
#endif
Console.WriteLine("PASS: shared-context compiler join, exact aggregate cap, unused immutable checkpoints and source locations; runtime " + Environment.Version);
sealed class Resolver(string text) : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return new(new MemoryStream(Encoding.UTF8.GetBytes(text)), request.Uri); }
}

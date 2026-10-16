using Xemele;
using Xemele.Validation;
if (Environment.Version.Major != 10 || Environment.Version.Minor != 0) throw new Exception("The .NET 10 runtime is required.");
bool xmlOnly = Environment.GetEnvironmentVariable("OUTCOMES_XML_ONLY") == "1";
if (xmlOnly && typeof(XmlDocument).Assembly.GetType("Xemele.HtmlDocument") is not null) throw new Exception("HTML present in XML-only consumer.");
var request = new XmlResourceRequest(new Uri("urn:example"), null, XmlResourceKind.Schema);
var legacy = new Legacy();
if (legacy.ResolveResult(request, default).Status != XmlResourceResolutionStatus.Denied) throw new Exception("Legacy null changed.");
if (legacy.Resolve(request, default) is not null || await legacy.ResolveAsync(request, default) is not null) throw new Exception("Legacy consumer changed.");
var context = new SchemaCompileContext(new() { Resolver = new OptionalResolver() });
var reference = new SchemaResourceReference("urn:example", null, SchemaResourcePurpose.XsdInclude, default);
if (context.Resources.TryLoadOptional(reference, null, context.Operation, out var missing) || missing is not null || !context.Operation.IsComplete) throw new Exception("Optional absence failed.");
try
{
    context.Resources.Load(reference, context.Operation);
    throw new Exception("Mandatory accepted absence.");
}
catch (SchemaCompileException error) when (error.Diagnostics.Single().Code == "SCHEMA.ResourceDenied") { }
var schema = XmlSchema.Compile("<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='root'/></xs:schema>");
if (!schema.Validate("<root/>").IsValid) throw new Exception("XML consumer failed.");
Console.WriteLine($"PASS: additive/legacy/optional/mandatory/XML consumer, XML-only={xmlOnly}, runtime {Environment.Version}.");
internal sealed class Legacy : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => null;
}
internal sealed class OptionalResolver : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => null;
    public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken cancellationToken) => XmlResourceResolution.Unavailable;
}

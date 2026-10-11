using Xemele;
using Xemele.Validation;
using Xemele.XPath;

var context = new SchemaCompileContext(new() { BaseUri = new("urn:schematron:consumer") });
var source = context.Parse("""
<schema xmlns="http://purl.oclc.org/dsdl/schematron" defaultPhase="first">
 <ns prefix="p" uri="urn:p"/>
 <phase id="first"><let name="threshold" value="1"/><active pattern="p"/></phase>
 <phase id="second"><let name="threshold" value="2"/><active pattern="p"/></phase>
 <pattern id="p"><rule context="p:item"><assert test="$threshold = 1" subject="@code">Code <name path="@code"/>: <value-of select="@code"/></assert></rule></pattern>
</schema>
""");
ISchematronCompiler compiler = new SchematronCompiler();
var graph = compiler.Compile(source.Document, new() { Schema = context.Options }, context.Operation);
source.Document.RootElement.SetTextContent("changed");
var document = XmlDocument.Parse("<root xmlns:p='urn:p'><p:item code='x'/><group><p:item code='y'/></group></root>");
var rule = graph.Patterns[0].Rules[0];
if (rule.ContextSelector.Evaluate(document.DocumentNode).AsNodeSet().Count != 2) throw new Exception("Match semantics changed");
if (graph.Phases["first"].Lets[0].Value.Evaluate(document.DocumentNode).AsNumber() != 1 || graph.Phases["second"].Lets[0].Value.Evaluate(document.DocumentNode).AsNumber() != 2) throw new Exception("Phase scopes changed");
if (rule.Subject is not null || rule.Checks[0].SubjectSource != "@code" || graph.Namespaces["p"] != "urn:p") throw new Exception("Detached subject/context changed");
if (XPathExpression.Compile("string(/root/group/p:item/@code)", new() { Namespaces = graph.Namespaces }).Evaluate(document.DocumentNode).AsString() != "y") throw new Exception("M4 changed");
if (!DtdSchema.Compile("<!ELEMENT root (item)><!ELEMENT item EMPTY>").Validate(XmlDocument.Parse("<root><item/></root>")).IsValid) throw new Exception("M5 changed");
if (typeof(XmlDocument).Assembly.GetTypes().Any(t => t.Namespace is string ns && (ns.Contains("Html", StringComparison.OrdinalIgnoreCase) || ns.StartsWith("Xemele.Css", StringComparison.Ordinal)))) throw new Exception("XML-only consumer loaded HTML/CSS");
Console.WriteLine("PASS: detached compiler model; separate phase bindings; XPath match/subjects; M4/M5 preserved; XML-only; runtime " + Environment.Version);

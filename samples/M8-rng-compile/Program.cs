using Xemele;
using Xemele.Validation;
using Xemele.XPath;

var context = new SchemaCompileContext(new() { BaseUri = new("urn:rng:consumer") });
var source = context.Parse("<grammar xmlns='http://relaxng.org/ns/structure/1.0'><start><ref name='tree'/></start><define name='tree'><element name='tree'><optional><ref name='tree'/></optional></element></define></grammar>");
IRelaxNgCompiler compiler = new RelaxNgCompiler();
var graph = compiler.Compile(source.Document, context.Options, context.Operation);
if (graph.Patterns[graph.Start].Kind != RngPatternKind.Ref || graph.Patterns.Count != 5) throw new Exception("RNG graph changed");
if (graph.Datatypes.Find("") is not null) throw new Exception("Compiler must not publish an executable datatype placeholder");
source.Document.RootElement.SetAttribute("changed", "yes");
if (graph.Patterns[graph.Start].Location.SourceUri != new Uri("urn:rng:consumer")) throw new Exception("Owned graph lost source");
var document = XmlDocument.Parse("<root>preserved</root>");
if (XPathExpression.Compile("string(/root)").Evaluate(document.DocumentNode).AsString() != "preserved") throw new Exception("M4 changed");
if (!DtdSchema.Compile("<!ELEMENT root (#PCDATA)>").Validate(document).IsValid) throw new Exception("M5 changed");
if (typeof(XmlDocument).Assembly.GetTypes().Any(t => t.Namespace is string ns && (ns.Contains("Html", StringComparison.OrdinalIgnoreCase) || ns.StartsWith("Xemele.Css", StringComparison.Ordinal)))) throw new Exception("XML-only consumer loaded HTML/CSS");
Console.WriteLine("PASS: real RELAX NG compiler; owned recursive graph; M4 XPath/M5 DTD preserved; XML-only; runtime " + Environment.Version);

using Xemele;
using Xemele.Validation;
using Xemele.XPath;

var compile = new SchemaCompileContext(new() { BaseUri = new("urn:m8:principal"), MaxOperations = 1000, MaxComponents = 100 });
var source = compile.Parse("<root xmlns:p='urn:prefix'>preserved</root>");
var names = source.Context(source.Document.RootElement);
if (names.Prefixes["p"] != "urn:prefix") throw new Exception("Namespace snapshot failed.");
var expression = XPathExpression.Compile("string(/root)");
var run = new SchemaValidationOperation(new() { MaxOperations = 1000 });
if (SchemaXPath.Evaluate(expression, source.Document.RootElement, null, run).AsString() != "preserved" || !run.Complete().IsValid) throw new Exception("M4/M5 composition failed.");
if (!DtdSchema.Compile("<!ELEMENT root (#PCDATA)>").Validate(XmlDocument.Parse("<root>preserved</root>")).IsValid) throw new Exception("M5 DTD behavior changed.");
var definition = new XmlDatatypeDefinition(new("urn:t", "union"), XmlDatatypeVariety.Union, null, null, [new("", "string")], [], [], false, default);
if (definition.MemberTypes.Count != 1) throw new Exception("Datatype model failed.");
var report = new SchematronResult(new(true, []), "#ALL", null, new Dictionary<string, string>(), [new(0, null, null)], [new(0, 0, 0, "/root", "/root")], []);
if (!report.Validation.IsValid || report.ActivePatterns[0].Id is not null) throw new Exception("Detached report failed.");
source.CheckUnchanged();
Console.WriteLine("PASS: real M8 models/options/context, M4 actual-accounting/M5 adapters, datatype contracts and detached report; runtime " + Environment.Version);

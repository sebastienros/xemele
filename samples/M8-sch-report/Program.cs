using System.Text;
using Xemele;
using Xemele.Validation;
using Xemele.XPath;

var schema=SchematronSchema.Compile("<schema xmlns='http://purl.oclc.org/dsdl/schematron'><pattern><rule context='item' subject='@code'><assert test='@code'>missing</assert><report test='@code'>code:<value-of select='@code'/></report></rule></pattern></schema>");
var doc=XmlDocument.Parse("<root><item code='😀'/><item/></root>");
var report=schema.Validate(doc);
if(report.Validation.IsValid || !report.Validation.IsComplete || report.Findings.Count!=2)throw new Exception("evaluation");
string text=SchematronReport.SerializeSvrl(report);
doc.RootElement.SetTextContent("removed");
using var output=new MemoryStream();SchematronReport.SaveSvrl(report,output);
if(!output.CanWrite || !output.ToArray().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(text)) || text.StartsWith("<?xml",StringComparison.Ordinal))throw new Exception("profile bytes/lifetime");
var xml=XmlDocument.Parse(text);
var xp=new XPathCompileOptions {Namespaces=new Dictionary<string,string>{{"s","http://purl.oclc.org/dsdl/svrl"}}};
if(XPathExpression.Compile("count(/s:schematron-output/s:failed-assert)",xp).Evaluate(xml.DocumentNode).AsNumber()!=1 || XPathExpression.Compile("count(/s:schematron-output/s:successful-report)",xp).Evaluate(xml.DocumentNode).AsNumber()!=1)throw new Exception("XML profile");
foreach(var result in new[]{schema.Validate("<item code='ok'/>"),schema.Validate("<broken>")})_ =XmlDocument.Parse(SchematronReport.SerializeSvrl(result));
if(typeof(XmlDocument).Assembly.GetType("Xemele.Html5.Html5Parser") is null && typeof(XmlDocument).Assembly.GetType("Xemele.Css.CssSelector") is null)Console.WriteLine("PASS genuine XML-only source assembly");
Console.WriteLine("PASS detached invalid/valid/incomplete reports; string/UTF8 parity; M1 parse/M4 XPath; caller stream open");

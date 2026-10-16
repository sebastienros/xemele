using System.Text;
using Xemele;
using Xemele.Streaming;
using Xemele.XPath;
using Xemele.Validation;
using Xemele.Html;
using Xemele.Css;

int checks = 0;
void Require(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
void Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T) { checks++; return; }
    throw new Exception("Expected " + typeof(T).Name);
}
const string input = "<r xmlns='urn:r'><n a='7'>text</n></r>";
var document = XmlDocument.Parse(input);
Require(document.RootElement.NamespaceUri == "urn:r", "Expanded names");
Require(document.RootElement.GetAttribute("absent") is null, "Missing attribute null");
Require(document.RootElement.Parent == document.DocumentNode, "Owned node identity");
Require(!XmlDocument.TryParse("<r>", out var rejected, out var diagnostics) && rejected is null && diagnostics.Count != 0, "TryParse output");
Throws<ArgumentNullException>(() => XmlDocument.Parse((string)null!));
Throws<XmlParseException>(() => XmlDocument.Parse("<r>"));
Throws<ArgumentException>(() => XmlDocument.Parse("<r/>", new() { Recover = true }));
Throws<InvalidOperationException>(() => { _ = default(XmlNode).Kind; });
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
var loaded = await XmlDocument.LoadAsync(stream);
Require(stream.CanRead && loaded.RootElement.NamespaceUri == "urn:r", "Async ownership");
using var output = new MemoryStream();
loaded.Save(output);
Require(output.CanWrite && output.Length > 0, "Save ownership");
Throws<OperationCanceledException>(() => loaded.Save(output, cancellationToken: new(true)));
Require(output.CanWrite, "Cancelled ownership");
var recovered = XmlDocument.ParseWithDiagnostics("<r a='1' a='2'/>", new() { Recover = true });
Require(recovered.IsComplete && !recovered.IsWellFormed && recovered.Value is not null, "Recovery distinguishes repaired output");
Throws<XmlParseException>(() => XmlDocument.Parse("<!DOCTYPE r SYSTEM 'file:///not-authorized'><r/>"));
var copiedText = new List<string>();
int starts = 0;
XmlSaxParser.Parse(input, new XmlSaxCallbacks { OnStartElement = _ => starts++, OnText = text => copiedText.Add(text.ToString()) });
Require(starts == 2 && string.Concat(copiedText) == "text", "Borrowed callback copy");
var sentinel = new InvalidOperationException("caller callback");
try
{
    XmlSaxParser.Parse("<r/>", new XmlSaxCallbacks { OnStartElement = _ => throw sentinel });
}
catch (InvalidOperationException ex) { Require(ReferenceEquals(ex, sentinel), "Callback exception identity"); }
var query = XPathExpression.Compile("sum(/r/n/@a) + $offset");
var numbers = XmlDocument.Parse("<r><n a='7'/></r>");
var context = new XPathContext { Variables = new Dictionary<XPathExpandedName, XPathValue> { [new("", "offset")] = XPathValue.FromNumber(2) } };
Require(query.Evaluate(numbers.RootElement, context).AsNumber() == 9, "Compiled XPath variables");
var html = Html5Parser.Parse("<p class='selected'>one<p>two");
Require(CssSelector.Compile("p.selected").Select(html.DocumentNode).Count == 1, "HTML5 CSS");
var xsd = Xemele.Validation.XmlSchema.Compile("<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='r' type='xs:integer'/></xs:schema>");
var valid = xsd.Validate("<r>7</r>");
var invalid = xsd.Validate("<r>bad</r>");
Require(valid.IsComplete && valid.IsValid && invalid.IsComplete && !invalid.IsValid, "Validation status");
Throws<SchemaCompileException>(() => Xemele.Validation.XmlSchema.Compile("<wrong/>"));
Console.WriteLine($"PASS migration {checks} checks; existing and additive APIs, null/exception/ownership/cancellation behavior");

using Xemele;
using Xemele.Streaming;

var handler = new XmlSaxCallbacks
{
    OnStartElement = element => Console.WriteLine($"start:{element.LocalName}:{element.NamespaceUri}"),
    OnText = text => Console.WriteLine("text:" + text.ToString()),
    OnEndDocument = () => Console.WriteLine("complete")
};
XmlSaxParser.Parse("<r xmlns='urn:sample'>hello</r>", handler);
XmlSaxParser.Parse("<r/>"u8, handler);
using var input = new MemoryStream("<r/>"u8.ToArray());
var result = XmlSaxParser.ParseWithDiagnostics(input, handler);
if (!result.IsWellFormed || result.Value is null || !input.CanRead) return 1;
var repaired = XmlSaxParser.ParseWithDiagnostics("<r a='1' a='2'/>", new XmlSaxCallbacks(), new() { Recover = true });
if (!repaired.IsComplete || repaired.IsWellFormed || repaired.Diagnostics.Count != 1) return 2;
return 0;

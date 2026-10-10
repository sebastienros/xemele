using System.Text.Json;
using Xemele;
using Xemele.Parsing.Syntax;

const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST unused a CDATA '&x;'>]><r a=' A &x; B&#9;' b='complete'/>";
var document = XmlDocument.Parse(xml, new() { DtdProcessing = XmlDtdProcessing.Parse });
var value = document.IncompleteAttributeValues.Single(); var declaration = document.IncompleteAttributeDefaults.Single();
if (value.Attribute.LocalName != "a" || value.Parent != document.RootElement || value.Sequence != 0 || value.Default is not null ||
    value.LexicalValue != " A &x; B&#9;" || value.IsTokenized || value.Parts.Count != 3 ||
    value.Parts[0] is not XmlKnownAttributeText { Text: " A " } || value.Parts[1] is not XmlUnknownAttributeReference { Name: "x", LexicalReference: "&x;" } ||
    value.Parts[2] is not XmlKnownAttributeText { Text: " B\t" } || declaration.ElementName != "unused" || declaration.AttributeName != "a" ||
    declaration.Sequence != 0 || declaration.Parts.Single() is not XmlUnknownAttributeReference { Name: "x" } || document.RootElement.GetAttribute("b") != "complete")
    throw new Exception("Owned actual source/package oracle.");
try { _ = value.Attribute.Value; throw new Exception("Missing value refusal."); }
catch (InvalidOperationException e) when (e.Message == "Attribute value contains unresolved entity references.") { }
try { _ = document.ToXmlString(); throw new Exception("Missing output refusal."); }
catch (InvalidOperationException e) when (e.Message == "Document contains unresolved entity references.") { }
Console.WriteLine(JsonSerializer.Serialize(new { values = document.IncompleteAttributeValues.Count, defaults = document.IncompleteAttributeDefaults.Count,
    parts = value.Parts.Count, lexical = value.LexicalValue, value.Sequence, known = document.RootElement.GetAttribute("b") }));

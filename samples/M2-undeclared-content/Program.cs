using Xemele;
var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
var document = XmlDocument.Parse("<!DOCTYPE foo [<!ENTITY % p ''>%p;]><foo>a&ent2;b</foo>", options);
var reference = document.UnresolvedReferences.Single();
if (reference.Name != "ent2" || reference.LexicalReference != "&ent2;" || reference.Parent != document.RootElement ||
    reference.PreviousSibling != document.RootElement.FirstChild || reference.ChildBoundary != 1 || reference.Sequence != 0 ||
    document.RootElement.FirstChild.Value != "a" || document.RootElement.LastChild.Value != "b") throw new Exception("Actual public provenance oracle.");
try { document.ToXmlString(); throw new Exception("Loss refusal absent."); }
catch (InvalidOperationException error) when (error.Message == "Document contains unresolved entity references.") { }
Console.WriteLine("Actual source/package parse, provenance and output refusal passed.");

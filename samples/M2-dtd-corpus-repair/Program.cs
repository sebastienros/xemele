using Xemele;
var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
var doc = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY 中 'v1'><!ATTLIST r a1 CDATA '&中;'>]><r/>", options);
if (doc.RootElement.LocalName != "r" || doc.RootElement.GetAttribute("a1") != "v1" || doc.RootElement.Attributes.Count != 1 || !doc.RootElement.FirstChild.IsDefault || doc.DocumentType!.Name != "r") throw new Exception("document/default oracle");
foreach (string dtd in new[] { "<!ATTLIST r a CDATA '&e;'><!ENTITY e 'v'>", "<!ENTITY a:b 'unused'>", "<!NOTATION a:b SYSTEM 'unused'>" })
    if (XmlDocument.TryParse("<!DOCTYPE r [" + dtd + "]><r/>", out _, out _, options)) throw new Exception("WFC rejection oracle");
Console.WriteLine("Passed authored document/default/non-ASCII name and three WFC rejection oracles.");

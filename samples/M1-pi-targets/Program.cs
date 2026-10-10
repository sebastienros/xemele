using System.Text;
using Xemele;
const string invalid = "<?a:b bogus?><foo/>";
if (XmlDocument.TryParse(Encoding.UTF8.GetBytes(invalid), out var rejected, out var diagnostics) || rejected is not null || diagnostics.Single().Code != "XML.Syntax") throw new Exception("Colon PI accepted.");
var document = XmlDocument.Parse("<?xml-stylesheet data?><p:foo xmlns:p='u' p:a='v'/>");
if (document.DocumentNode.FirstChild.LocalName != "xml-stylesheet" || document.RootElement.Prefix != "p") throw new Exception("Valid PI/QName rejected.");
try { document.CreateProcessingInstruction("a:b", ""); throw new Exception("Factory accepted colon."); } catch (ArgumentException) { }
var supplementary = document.CreateProcessingInstruction("\U00010000", "");
document.RootElement.AppendChild(supplementary);
var output = document.ToXmlString(new() { OmitXmlDeclaration = true });
if (!output.Contains("<?\U00010000?>", StringComparison.Ordinal)) throw new Exception("Supplementary NCName lost.");
Console.WriteLine("Independent source/package PI target sample passed: synthetic042 rejection, legal xml-like/supplementary targets, QName preserved.");

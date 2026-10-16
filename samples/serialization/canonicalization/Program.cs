using System.Text;
using Xemele;
using Xemele.Serialization;
using Xemele.XPath;

var document = XmlDocument.Parse("<envelope xmlns:p='urn:p' xml:lang='en'><p:payload z='2' a='1'>根😀</p:payload></envelope>");
var selection = XPathExpression.Compile("(//node() | //@* | //namespace::*)[ancestor-or-self::*[local-name()='payload']]").Evaluate(document.DocumentNode).AsNodeSet();
foreach (var mode in Enum.GetValues<XmlCanonicalizationMode>())
{
    using var target = new MemoryStream();
    XmlCanonicalizer.Canonicalize(selection, target, new()
    {
        Mode = mode
    });
    Console.WriteLine(mode + ": " + Encoding.UTF8.GetString(target.ToArray()));
}

using System.Xml.Linq;
using Xemele;
var d = new Xemele.XmlDocument();
var root = d.CreateElement("xmlns", "urn:e");
d.DocumentNode.AppendChild(root);
root.SetNamespaceDeclaration("", "urn:e");
var a = root.SetAttribute("xmlns", "value", "urn:a");
var repair = new XmlSaveOptions { OmitXmlDeclaration = true, NamespaceHandling = XmlNamespaceHandling.Repair };
string xml = d.ToXmlString(repair);
var oracle = XDocument.Parse(xml).Root!;
if (oracle.Name != XName.Get("xmlns", "urn:e") || oracle.Attribute(XName.Get("xmlns", "urn:a"))?.Value != "value" || a.Prefix != "") throw new Exception("Repair mismatch");
root.SetNamespaceDeclaration("p", "urn:a");
root.SetAttribute("xmlns", "value", "urn:a", "p");
var strict = repair with { NamespaceHandling = XmlNamespaceHandling.Strict };
using var stream = new MemoryStream();
d.Save(stream, strict);
stream.Position = 0;
if (XDocument.Load(stream).Root!.Attribute(XName.Get("xmlns", "urn:a"))?.Value != "value") throw new Exception("Strict mismatch");
try { root.SetAttribute("xmlns", "bad"); throw new Exception("Declaration accepted as attribute"); } catch (ArgumentException) { }
var plain = new Xemele.XmlDocument();
plain.DocumentNode.AppendChild(plain.CreateElement("xmlns"));
if (plain.ToXmlString(strict) != "<xmlns/>") throw new Exception("Plain element rejected");
Console.WriteLine("Independent source/package namespace construction, Strict/Repair and BCL round trips passed.");
// Allocation counters only: no timing or throughput measurements. Owned tree/text
// creation and the first serializer/JIT/pool use are outside the warm scope.
if (args.Contains("--allocation"))
{
    static long Measure(int length)
    {
        var doc = new Xemele.XmlDocument();
        var e = doc.CreateElement("xmlns");
        doc.DocumentNode.AppendChild(e);
        e.SetAttribute("xmlns", "v", "urn:a", "p");
        e.SetNamespaceDeclaration("p", "urn:a");
        e.AppendChild(doc.CreateText(new string('x', length)));
        var options = new XmlSaveOptions { OmitXmlDeclaration = true };
        for (int i = 0; i < 20; i++)
            doc.Save(Stream.Null, options);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
            doc.Save(Stream.Null, options);
        return (GC.GetAllocatedBytesForCurrentThread() - start) / 10;
    }
    long small = Measure(1024), large = Measure(1024 * 1024);
    Console.WriteLine($"Warm Strict serializer: 1KiB text {small} B/op; 1MiB text {large} B/op; owned tree/text and cold setup excluded.");
    if (large > small + 256)
        throw new Exception("Streaming allocation grew with text length");
}

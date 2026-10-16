using System.Text;
using Xemele;
using Writer = Xemele.XmlWriter;
if (args.Length == 1 && args[0] == "--probe")
{
    var options = new XmlSaveOptions { OmitXmlDeclaration = true };
    long coldStart = GC.GetAllocatedBytesForCurrentThread();
    Document(options);
    long cold = GC.GetAllocatedBytesForCurrentThread() - coldStart;
    for (int i = 0; i < 100; i++)
        Document(options);
    long start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1000; i++)
        Document(options);
    long warm = GC.GetAllocatedBytesForCurrentThread() - start;
    Console.WriteLine($"Cold document/setup: {cold} B; warmed setup/output/dispose: {warm / 1000.0} B/document (1000 documents).");
    return 0;
}
static void Document(XmlSaveOptions options)
{
    using var w = Writer.Create(Stream.Null, options);
    w.WriteStartElement("r");
    w.WriteAttribute("a", "v");
    w.WriteText("hello&α😀]]");
    w.WriteText(">");
    w.WriteEndElement();
    w.WriteEndDocument();
}
using var stream = new MemoryStream();
using var writer = Writer.Create(stream, new() { OmitXmlDeclaration = true });
writer.WriteStartElement("greeting", "urn:example", "e");
writer.WriteAttribute("language", "en");
writer.WriteNamespace("e", "urn:example");
writer.WriteText("Hello & welcome, 世界 😀");
writer.WriteEndElement();
writer.WriteEndDocument();
const string expected = "<e:greeting xmlns:e=\"urn:example\" language=\"en\">Hello &amp; welcome, 世界 😀</e:greeting>";
string actual = Encoding.UTF8.GetString(stream.ToArray());
if (actual != expected || !stream.CanWrite) return 1;
Console.WriteLine(actual);
return 0;

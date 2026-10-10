using System.Text;
using Xemele;

if (args.Contains("--allocation"))
{
    string fixture = "<r>" + string.Concat(Enumerable.Repeat("<c a='value'>α😀&amp;</c>", 512)) + "</r>";
    byte[] fixtureBytes = Encoding.UTF8.GetBytes(fixture);
    long start = GC.GetAllocatedBytesForCurrentThread();
    var cold = XmlDocument.Parse(fixtureBytes.AsSpan());
    Console.WriteLine($"Cold complete owned byte DOM: {GC.GetAllocatedBytesForCurrentThread() - start} B; children={cold.RootElement.Children.Count}");
    for (int i = 0; i < 16; i++) XmlDocument.Parse(fixtureBytes.AsSpan());
    start = GC.GetAllocatedBytesForCurrentThread();
    var warm = XmlDocument.Parse(fixtureBytes.AsSpan());
    Console.WriteLine($"Warm complete owned byte DOM: {GC.GetAllocatedBytesForCurrentThread() - start} B; children={warm.RootElement.Children.Count}");
    start = GC.GetAllocatedBytesForCurrentThread();
    var chars = XmlDocument.Parse(fixture);
    Console.WriteLine($"Warm complete owned character DOM: {GC.GetAllocatedBytesForCurrentThread() - start} B; children={chars.RootElement.Children.Count}");
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long retainedStart = GC.GetTotalMemory(true);
    var batch = new XmlDocument[32];
    for (int i = 0; i < batch.Length; i++) batch[i] = XmlDocument.Parse(fixtureBytes.AsSpan());
    long retained = GC.GetTotalMemory(true) - retainedStart;
    Console.WriteLine($"Retained owned DOM GC estimate: {retained / batch.Length} B/document; shared fixture excluded, 32 owners.");
    GC.KeepAlive(batch); GC.KeepAlive(cold); GC.KeepAlive(warm); GC.KeepAlive(chars);
}

const string xml = "<?xml version='1.0' encoding='UTF-8'?><catalog xmlns:p='urn:items'><p:item id='1'>α😀&amp;<![CDATA[x<y]]></p:item></catalog>";
byte[] bytes = Encoding.UTF8.GetBytes(xml);
var characters = XmlDocument.Parse(xml);
var fromBytes = XmlDocument.Parse(bytes.AsSpan());
using var stream = new MemoryStream(bytes);
var loaded = XmlDocument.Load(stream);
foreach (var document in new[] { characters, fromBytes, loaded })
{
    var item = document.RootElement.FirstChild;
    if (item.LocalName != "item" || item.NamespaceUri != "urn:items" || item.GetAttribute("id") != "1" || item.GetTextContent() != "α😀&x<y")
        throw new InvalidOperationException("DOM sample structure mismatch.");
}
if (!stream.CanRead || !XmlDocument.TryParse(xml.AsSpan(), out var successful, out var empty) || successful is null || empty.Count != 0)
    throw new InvalidOperationException("Factory ownership/success mismatch.");
if (XmlDocument.TryParse("<!DOCTYPE r><r/>", out var rejected, out var diagnostics) || rejected is not null || diagnostics.Count != 1 || diagnostics[0].Code != "XML.DtdProhibited")
    throw new InvalidOperationException("Strict rejection mismatch.");
Console.WriteLine("Independent DOM factories: characters/bytes/stream; structure, ownership and strict rejection passed.");

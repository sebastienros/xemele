using System.Text;
using System.Xml;
using XDocument = Xemele.XmlDocument;
using Xemele;

const string input = "<!--before--><p:r xmlns:p='urn:p' a='&#x9;&#xA;&#xD;'>&#x1F600;<![CDATA[x<y]]><?work value?></p:r>";
const string expected = "<!--before--><p:r xmlns:p='urn:p' a='&#x9;&#xA;&#xD;' b='&amp;&quot;'>&#x1F600;<![CDATA[x<y]]><?work value?><p:item>changed&#xD;&lt;</p:item></p:r>";
var documents = new List<XDocument> { XDocument.Parse(input), XDocument.Parse(input.AsSpan()) };
foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, false, true), new UnicodeEncoding(true, false, true) })
{
    byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes(input)).ToArray();
    // Explicit UTF-16 signature without a declaration is unsupported: use a BOM.
    if (encoding.CodePage != 65001) bytes = (encoding.CodePage == 1200 ? new byte[] { 255, 254 } : new byte[] { 254, 255 }).Concat(encoding.GetBytes(input)).ToArray();
    documents.Add(XDocument.Parse(bytes.AsSpan()));
    using var stream = new OneByteStream(bytes);
    documents.Add(XDocument.Load(stream));
    Require(stream.CanRead, "Load closed caller stream");
}
foreach (var document in documents)
{
    var root = document.RootElement;
    Require(root.LocalName == "r" && root.NamespaceUri == "urn:p" && root.GetAttribute("a") == "\t\n\r" && root.GetTextContent() == "😀x<y", "input semantic model");
    root.SetAttribute("b", "&\"");
    var child = document.CreateElement("item", "urn:p", "p");
    child.SetTextContent("changed\r<");
    root.AppendChild(child);
    var clone = child.Clone(true);
    root.AppendChild(clone);
    clone.Remove();
    Require(child == root.LastChild && clone.Parent.IsDefault && clone.GetTextContent() == "changed\r<", "mutation identity");
    string output = document.ToXmlString();
    Require(Signature(output) == Signature(expected), "independent reader semantic oracle");
    var reparsed = XDocument.Parse(output);
    Require(reparsed.RootElement.LastChild.GetTextContent() == "changed\r<" && reparsed.RootElement.GetAttribute("b") == "&\"", "strict semantic reparse");
    foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, false, true), new UnicodeEncoding(true, false, true) })
    {
        using var stream = new MemoryStream();
        document.Save(stream, new() { Encoding = encoding, EmitByteOrderMark = true });
        Require(stream.CanWrite, "Save closed caller stream");
        stream.Position = 0;
        using var reader = XmlReader.Create(stream, new() { CloseInput = false, DtdProcessing = DtdProcessing.Prohibit });
        Require(Signature(reader) == Signature(expected), "independent byte output model");
        stream.Position = 0;
        Require(XDocument.Load(stream).RootElement.LastChild.GetTextContent() == "changed\r<", "byte semantic reparse");
    }
}
var constructed = new XDocument();
var constructedRoot = constructed.CreateElement("r");
constructed.DocumentNode.AppendChild(constructedRoot);
constructedRoot.SetAttribute("a", "\t\n\r&\"");
constructedRoot.AppendChild(constructed.CreateText("x\r<&"));
constructedRoot.AppendChild(constructed.CreateCData("y<z"));
constructedRoot.AppendChild(constructed.CreateComment("ok"));
constructedRoot.AppendChild(constructed.CreateProcessingInstruction("work", "data"));
const string constructedXml = "<r a=\"&#x9;&#xA;&#xD;&amp;&quot;\">x&#xD;&lt;&amp;<![CDATA[y<z]]><!--ok--><?work data?></r>";
string constructedOutput = constructed.ToXmlString(new() { OmitXmlDeclaration = true });
Require(constructedOutput == constructedXml && Signature(constructedOutput) == Signature(constructedXml), "constructed known output");
Require(XDocument.Parse(constructedOutput).RootElement.GetTextContent() == "x\r<&y<z", "constructed strict round trip");
Console.WriteLine("Constructed output: " + constructedOutput);
foreach (var (xml, code) in new[] { ("<!DOCTYPE r SYSTEM 'file:///must-not-open'><r/>", "XML.DtdProhibited"), ("<?a:b bogus?><r/>", "XML.Syntax"), ("<?xml version='1.1'?><r/>", "XML.Syntax"), ("<r>&unknown;</r>", "XML.UndeclaredEntity") })
{
    Require(!XDocument.TryParse(xml, out var rejected, out var diagnostics) && rejected is null && diagnostics.Count > 0 && diagnostics[0].Code == code, "safe rejection " + code);
    using var stream = new OneByteStream(Encoding.UTF8.GetBytes(xml));
    try { XDocument.Load(stream); throw new Exception("Rejected stream accepted"); }
    catch (XmlParseException) { Require(stream.CanRead, "rejection closed stream"); }
}
var unsupported = Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='ISO-8859-1'?><r/>");
Require(!XDocument.TryParse(unsupported.AsSpan(), out var unsupportedDocument, out var unsupportedDiagnostics) && unsupportedDocument is null && unsupportedDiagnostics[0].Code == "INPUT.UnsupportedEncoding", "unsupported encoding");
using (var cancelled = new MemoryStream())
{
    try { constructed.Save(cancelled, cancellationToken: new CancellationToken(true)); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { Require(cancelled.CanWrite && cancelled.Length == 0, "cancelled Save lifetime"); }
}
Console.WriteLine("M1 demo passed: 8 factory inputs, mutation/identity, constructed exact output, independent XmlReader semantic oracle, 24 encoded saves/reparses, safe unsupported rejection and caller-stream lifetime.");

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static string Signature(string xml)
{
    using var reader = XmlReader.Create(new StringReader(xml), new() { DtdProcessing = DtdProcessing.Prohibit });
    return Signature(reader);
}
static string Signature(XmlReader reader)
{
    var rows = new List<string>();
    while (reader.Read())
    {
        if (reader.NodeType == XmlNodeType.XmlDeclaration) continue;
        rows.Add(System.Text.Json.JsonSerializer.Serialize(new[] { reader.NodeType.ToString(), reader.LocalName, reader.NamespaceURI, reader.Value }));
        if (reader.NodeType == XmlNodeType.Element)
        {
            var attributes = new List<string>();
            while (reader.MoveToNextAttribute())
                if (reader.NamespaceURI != "http://www.w3.org/2000/xmlns/") attributes.Add(System.Text.Json.JsonSerializer.Serialize(new[] { reader.LocalName, reader.NamespaceURI, reader.Value }));
            reader.MoveToElement(); attributes.Sort(StringComparer.Ordinal); rows.AddRange(attributes);
            if (reader.IsEmptyElement) rows.Add(System.Text.Json.JsonSerializer.Serialize(new[] { "EndElement", reader.LocalName, reader.NamespaceURI, "" }));
        }
    }
    return string.Join("\n", rows);
}
sealed class OneByteStream(byte[] bytes) : MemoryStream(bytes)
{
    public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));
    public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
}

using Xemele;
using Writer = Xemele.XmlWriter;
using var stream = new MemoryStream();
await using (var writer = Writer.Create(stream, new() { OmitXmlDeclaration = true }))
{
    await writer.WriteStartDocumentAsync(); await writer.WriteCommentAsync("prolog".AsMemory());
    await writer.WriteProcessingInstructionAsync("go", "now".AsMemory());
    await writer.WriteStartElementAsync("root", "urn:sample", "p");
    await writer.WriteAttributeAsync("a", "value"); await writer.WriteNamespaceAsync("p", "urn:sample");
    await writer.WriteTextAsync("α😀&]]".AsMemory()); await writer.WriteTextAsync(">".AsMemory());
    await writer.WriteCDataAsync("literal".AsMemory()); await writer.FlushAsync();
    await writer.WriteEndElementAsync(); await writer.WriteEndDocumentAsync();
}
string actual = System.Text.Encoding.UTF8.GetString(stream.ToArray());
const string expected = "<!--prolog--><?go now?><p:root xmlns:p=\"urn:sample\" a=\"value\">α😀&amp;]]&gt;<![CDATA[literal]]></p:root>";
if (actual != expected) throw new InvalidOperationException(actual);
Console.WriteLine(actual);

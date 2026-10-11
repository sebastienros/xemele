using System.Runtime.InteropServices;
using Xemele.Validation;
using Xemele.Serialization;
using Xemele.XPath;
using System.Text;
using Xemele;
using Xemele.Streaming;
using Reader = Xemele.Streaming.XmlReader;
using Writer = Xemele.XmlWriter;

Require(RuntimeInformation.FrameworkDescription == ".NET 10.0.11");
Console.WriteLine(RuntimeInformation.FrameworkDescription);
const string xml = "<!--before--><r xmlns='urn:r' a='&amp;'>α<![CDATA[😀]]><c/>tail</r>";
const string golden = "<!--before--><r xmlns=\"urn:r\" a=\"&amp;\">α<![CDATA[😀]]><c/>tail</r>";
byte[] bytes = Encoding.UTF8.GetBytes(xml);
var options = new XmlSaveOptions { OmitXmlDeclaration = true };
var dom = XmlDocument.Parse(bytes);
Require(dom.RootElement.NamespaceUri == "urn:r" && dom.RootElement.GetAttribute("a") == "&" && dom.RootElement.GetTextContent() == "α😀tail");
Require(dom.ToXmlString(options) == golden);
var handler = new Handler(); XmlSaxParser.Parse(bytes, handler); Check(handler);
int callbackStarts = 0, callbackEnds = 0;
XmlSaxParser.Parse(xml, new XmlSaxCallbacks { OnStartElement = e => callbackStarts++, OnEndDocument = () => callbackEnds++ });
Require(callbackStarts == 2 && callbackEnds == 1);
var pushed = new Handler();
using (var push = new XmlPushParser(pushed))
{
    for (int i = 0; i < bytes.Length; i += 3) push.Append(bytes.AsSpan(i, Math.Min(3, bytes.Length - i)));
    Require(pushed.Starts == 2 && pushed.Completed == 0); push.Complete(); Check(pushed);
}
using (var input = new MemoryStream(bytes, false))
using (var reader = Reader.Create(input)) { var current = new Handler(); while (reader.Read()) Token(reader, current); Check(current); Require(input.CanRead && reader.TokenKind == XmlTokenKind.None); }
using (var input = new AsyncInput(bytes)) { var loaded = await XmlDocument.LoadAsync(input); Require(loaded.ToXmlString(options) == golden && input.CanRead); }
using (var input = new AsyncInput(bytes)) { var current = new Handler(); await XmlSaxParser.ParseAsync(input, current); Check(current); Require(input.CanRead); }
using (var input = new AsyncInput(bytes))
await using (var reader = Reader.Create(input)) { var current = new Handler(); while (await reader.ReadAsync()) Token(reader, current); Check(current); Require(input.CanRead); }
using (var output = new MemoryStream()) { await dom.SaveAsync(output, options); Require(Encoding.UTF8.GetString(output.ToArray()) == golden && output.CanWrite); }
using (var output = new MemoryStream())
{
    using var writer = Writer.Create(output, options);
    writer.WriteComment("before"); writer.WriteStartElement("r", "urn:r"); writer.WriteNamespace("", "urn:r"); writer.WriteAttribute("a", "&");
    writer.WriteText("α"); writer.WriteCData("😀"); writer.WriteStartElement("c", "urn:r"); writer.WriteEndElement(); writer.WriteText("tail"); writer.WriteEndElement(); writer.WriteEndDocument();
    Require(Encoding.UTF8.GetString(output.ToArray()) == golden && output.CanWrite);
}
using (var output = new MemoryStream())
{
    await using var writer = Writer.Create(output, options);
    await writer.WriteCommentAsync("before".AsMemory()); await writer.WriteStartElementAsync("r", "urn:r"); await writer.WriteNamespaceAsync("", "urn:r"); await writer.WriteAttributeAsync("a", "&");
    await writer.WriteTextAsync("α".AsMemory()); await writer.WriteCDataAsync("😀".AsMemory()); await writer.WriteStartElementAsync("c", "urn:r"); await writer.WriteEndElementAsync(); await writer.WriteTextAsync("tail".AsMemory()); await writer.WriteEndElementAsync(); await writer.WriteEndDocumentAsync();
    Require(Encoding.UTF8.GetString(output.ToArray()) == golden && output.CanWrite);
}
var dtd = XmlDocument.Parse("<!DOCTYPE r [<!ATTLIST r a CDATA 'v'><!ENTITY e 'E'>]><r>&e;</r>", new() { DtdProcessing = XmlDtdProcessing.Parse });
Require(dtd.RootElement.GetAttribute("a") == "v" && dtd.RootElement.GetTextContent() == "E");
var recovered = XmlDocument.ParseWithDiagnostics("<r a='1' a='2'/>", new() { Recover = true });
Require(recovered.IsComplete && !recovered.IsWellFormed && recovered.Diagnostics.Count == 1 && recovered.Value!.RootElement.GetAttribute("a") == "1");
// The prior public XML/streaming demonstration also supplies the XPath context.
var query = Xemele.XPath.XPathExpression.Compile("concat(local-name(/*), ':', string(/*))");
Require(query.Evaluate(dom.DocumentNode).AsString() == "r:α😀tail");
var namespaceQuery = Xemele.XPath.XPathExpression.Compile("namespace-uri(/*)");
Require(namespaceQuery.Evaluate(dom.RootElement).AsString() == "urn:r");
var numbers = XmlDocument.Parse("<r><a n='1'>one</a><a n='2'>two</a><a n='3'>three</a></r>");
var sum = Xemele.XPath.XPathExpression.Compile("sum(/r/a/@n)"); Require(sum.Evaluate(numbers.RootElement).AsNumber() == 6);
var reverse = Xemele.XPath.XPathExpression.Compile("/r/a[3]/preceding-sibling::*[1]");
Require(reverse.Evaluate(numbers.RootElement).AsNodeSet()[0].StringValue == "two");
var frozenResult = reverse.Evaluate(numbers.RootElement).AsNodeSet();
var middleText = numbers.RootElement.FirstChild.NextSibling.FirstChild; middleText.Value = "changed";
Require(frozenResult[0].StringValue == "two" && reverse.Evaluate(numbers.RootElement).AsNodeSet()[0].StringValue == "changed");
Console.WriteLine("M4 standalone XPath passed: namespace/scalar/sum/reverse-axis/owned snapshot/reuse.");
Console.WriteLine("M3 standalone core preview passed: DOM, SAX callbacks/subclass, push, sync/async reader, sync/async writer, async DOM, DTD defaults, recovery.");
// Public M5 validation owns each result and permits independent schema reuse.
const string declarations = "<!ELEMENT r (a*)><!ELEMENT a EMPTY><!ATTLIST a id ID #REQUIRED>";
var schema = DtdSchema.Compile(declarations);
var valid = XmlDocument.Parse("<r><a id='one'/></r>");
var good = schema.Validate(valid); Require(good.IsComplete && good.IsValid && good.Diagnostics.Count == 0);
var bad = schema.Validate(XmlDocument.Parse("<r><a/></r>")); Require(bad.IsComplete && !bad.IsValid && bad.Diagnostics.Count > 0);
var quota = schema.Validate(valid, new() { MaxOperations = 1 }); Require(!quota.IsComplete && !quota.IsValid);
Require(schema.Validate(valid).IsValid && good.IsValid);
var withDtd = XmlDocument.Parse("<!DOCTYPE r [" + declarations + "]><r><a id='one'/></r>", new() { DtdProcessing = XmlDtdProcessing.Parse });
Require(DtdSchema.FromDocument(withDtd).Validate(withDtd).IsValid);
// Distinct inclusive XML context demonstrates all three modes; selection has exact membership.
var selectedDocument = XmlDocument.Parse("<r xml:lang='en' xml:id='parent'><skip><x a='A'>text<y/></x></skip></r>");
var x = selectedDocument.RootElement.FirstChild.FirstChild;
var canonicalDocument = XmlDocument.Parse("<!--before--><r z='2' a='&amp;'>α<![CDATA[<>]]><!--inside--><c/></r><!--after-->");
foreach (var mode in new[] { XmlCanonicalizationMode.Inclusive10, XmlCanonicalizationMode.Inclusive11, XmlCanonicalizationMode.Exclusive10 })
{
    using var whole = new MemoryStream();
    XmlCanonicalizer.Canonicalize(canonicalDocument, whole, new() { Mode = mode, IncludeComments = true });
    Require(whole.CanWrite && Encoding.UTF8.GetString(whole.ToArray()) == "<!--before-->\n<r a=\"&amp;\" z=\"2\">α&lt;&gt;<!--inside--><c></c></r>\n<!--after-->");
    using var withoutComments = new MemoryStream();
    XmlCanonicalizer.Canonicalize(canonicalDocument, withoutComments, new() { Mode = mode });
    Require(withoutComments.CanWrite && Encoding.UTF8.GetString(withoutComments.ToArray()) == "<r a=\"&amp;\" z=\"2\">α&lt;&gt;<c></c></r>");
    using var subset = new MemoryStream();
    XmlCanonicalizer.Canonicalize(new[] { XPathItem.FromNode(x) }, subset, new() { Mode = mode });
    string context = mode == XmlCanonicalizationMode.Inclusive10 ? " xml:id=\"parent\" xml:lang=\"en\"" : mode == XmlCanonicalizationMode.Inclusive11 ? " xml:lang=\"en\"" : "";
    Require(subset.CanWrite && Encoding.UTF8.GetString(subset.ToArray()) == "<x" + context + "></x>");
}
Console.WriteLine("M5 standalone validation/canonicalization passed: Compile/FromDocument/valid/invalid/quota/reuse, three exact algorithms, comments/subset/destination ownership.");
static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Independent preview oracle failed."); }
static void Check(Handler handler) => Require(handler.Starts == 2 && handler.Ends == 2 && handler.Completed == 1 && handler.Value.ToString() == "α😀tail");
static void Token(Reader reader, Handler handler)
{
    switch (reader.TokenKind)
    {
        case XmlTokenKind.StartElement: handler.Starts++; break;
        case XmlTokenKind.EndElement: handler.Ends++; break;
        case XmlTokenKind.Text: handler.Value.Append(reader.ValueSpan); break;
        case XmlTokenKind.EndDocument: handler.Completed++; break;
    }
}
sealed class Handler : XmlSaxHandler
{
    public int Starts, Ends, Completed;
    public readonly StringBuilder Value = new();
    public override void StartElement(XmlStartElement element) => Starts++;
    public override void EndElement(XmlEndElement element) => Ends++;
    public override void Text(ReadOnlySpan<char> text) => Value.Append(text);
    public override void EndDocument() => Completed++;
}
sealed class AsyncInput(byte[] bytes) : Stream
{
    private int offset;
    private bool disposed;
    public override int Read(byte[] buffer, int start, int count) => throw new InvalidOperationException("Synchronous input.");
    public override int Read(Span<byte> buffer) => throw new InvalidOperationException("Synchronous input.");
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this); cancellationToken.ThrowIfCancellationRequested(); await Task.Yield();
        int n = Math.Min(Math.Min(buffer.Length, 3), bytes.Length - offset);
        bytes.AsMemory(offset, n).CopyTo(buffer); offset += n; return n;
    }
    public override bool CanRead => !disposed;
    protected override void Dispose(bool disposing) { disposed = true; base.Dispose(disposing); }
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => bytes.Length;
    public override long Position { get => offset; set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long value, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int start, int count) => throw new NotSupportedException();
}

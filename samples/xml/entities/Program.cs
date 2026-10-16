using System.Reflection;
using System.Text;
using Xemele;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;

if (args is ["--runtime"]) { Console.WriteLine(Environment.Version); return; }
if (args is ["--contract"]) { Contract.Print(); return; }
if (args is ["--allocations"]) { Measurements.Run(); return; }

const string input = "<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:sample'><!ATTLIST r xmlns CDATA 'urn:r' a NMTOKENS ' owned  tail ' id ID #IMPLIED>]><r id='one'>&e;</r>";
var resolver = new SampleResolver();
var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
var document = XmlDocument.Parse(input, options);
if (document.DocumentType?.Name != "r" || document.RootElement.NamespaceUri != "urn:r" ||
    document.RootElement.GetAttribute("a") != "owned tail" || document.RootElement.GetAttribute("id") != "one" ||
    document.RootElement.GetTextContent() != "payloadtail" || document.RootElement.LastChild.LocalName != "b")
    throw new InvalidOperationException("DOM full-output oracle failed.");
Console.WriteLine($"DOM:doctype={document.DocumentType.Name};namespace={document.RootElement.NamespaceUri};default={document.RootElement.GetAttribute("a")};id={document.RootElement.GetAttribute("id")};text={document.RootElement.GetTextContent()}");
var sink = new SampleSink();
using (var core = new XmlParserCore(options, new Uri("https://primary.example/document.xml")))
{
    var result = core.Append(input, [], true, sink, default);
    if (result.Step != ScanStep.Completed || sink.Metadata != 1 || sink.Elements != "rb" || sink.Text.ToString() != "payloadtail" || sink.Attributes != "id=one;a=owned tail;")
        throw new InvalidOperationException("SAX full-output oracle failed.");
}
Console.WriteLine($"SAX:metadata={sink.Metadata};elements={sink.Elements};attributes={sink.Attributes};text={sink.Text}");
var injectedSink = new SampleSink();
using (var core = new XmlParserCore(options, new ModelContext(sink.Model!), new Uri("urn:explicit")))
    if (core.Append("<r/>", [], true, injectedSink, default).Step != ScanStep.Completed || injectedSink.Attributes != "a=owned tail;")
        throw new InvalidOperationException("Explicit approved context constructor/default lookup failed.");
using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(input), false))
{
    if (XmlDocument.Load(stream, options).RootElement.GetTextContent() != "payloadtail" || !stream.CanRead)
        throw new InvalidOperationException("Stream ownership/output oracle failed.");
}
if (resolver.Opens != 3 || resolver.Disposals != 3) throw new InvalidOperationException("Exactly-once resource ownership oracle failed.");
Console.WriteLine($"Context:default={injectedSink.Attributes};streams={resolver.Opens};disposed={resolver.Disposals}");

internal sealed class SampleResolver : XmlResourceResolver
{
    internal int Opens, Disposals;
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.SystemId != "urn:sample")
            return null;
        Opens++;
        return new(new OwnedStream(this), new("https://final.example/entity"));
    }
    private sealed class OwnedStream(SampleResolver owner) : MemoryStream(Encoding.UTF8.GetBytes("payload<b>tail</b>"), false)
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                owner.Disposals++;
            base.Dispose(disposing);
        }
    }
}
internal sealed class ModelContext(DtdModel model) : IXmlEntityContext
{
    public DtdModel Model => model;
    public bool TryGetGeneralEntity(ReadOnlySpan<char> name, out DtdEntityDeclaration? declaration)
    {
        foreach (var entity in model.GeneralEntities)
            if (name.SequenceEqual(entity.Name))
            {
                declaration = entity;
                return true;
            }
        declaration = null;
        return false;
    }
    public IReadOnlyList<DtdAttributeDeclaration> GetAttributes(ReadOnlySpan<char> elementName) => model.Attributes;
}
internal sealed class SampleSink : IXmlEventSink, IXmlDtdSink
{
    internal int Metadata;
    internal DtdModel? Model;
    internal readonly StringBuilder Text = new();
    internal string Elements = "", Attributes = "";
    public bool DocumentType(XmlDocumentType declaration, DtdModel model, XmlLocation location)
    {
        Metadata++;
        Model = model;
        return true;
    }
    public bool StartDocument(XmlLocation location) => true;
    public bool Declaration(XmlDeclarationView declaration) => true;
    public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
    {
        Elements += name.LocalName.ToString();
        return true;
    }
    public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
    public bool Attribute(XmlAttributeView attribute)
    {
        Attributes += attribute.Name.LocalName.ToString() + "=" + attribute.Value.ToString() + ";";
        return true;
    }
    public bool StartElementContent() => true;
    public bool EndElement(XmlNameView name, XmlLocation location) => true;
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
    public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location)
    {
        Text.Append(text);
        return true;
    }
    public bool EndDocument(XmlLocation location) => true;
}

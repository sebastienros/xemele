using Xemele;
using Xemele.Parsing.Syntax;
// Internal compile/behavior probe, deliberately independent of input and DOM.
using var core = new XmlParserCore(new());
var sink = new Sink();
string[] chunks = ["<?xml version='1.0'?><r p:a='v' xmlns:p='u'>α\r", "\n\uD83D", "\uDE00&amp;<![CDATA[x]]><p:c/></r>"];
for (int i = 0; i < chunks.Length; i++)
{
    int offset = 0; ScanResult result;
    do
    {
        result = core.Append(chunks[i].AsSpan(offset), [], i == chunks.Length - 1, sink, default);
        offset += result.Consumed;
        if (result.Step == ScanStep.Rejected) throw new Exception(result.Diagnostic!.Code);
    } while (result.Step == ScanStep.Paused);
    if (offset != chunks[i].Length || result.Step != (i == chunks.Length - 1 ? ScanStep.Completed : ScanStep.NeedInput)) throw new Exception("Probe progress mismatch.");
}
if (sink.Elements != 2 || sink.Ends != 2 || sink.Documents != 1 || sink.Units != 6) throw new Exception("Probe event mismatch.");
Console.WriteLine("M1 syntax independent character/sink probe passed; every callback paused once, CRLF/surrogate/reference/namespace/CDATA preserved.");
sealed class Sink : IXmlEventSink
{
    internal int Elements, Ends, Documents, Units;
    public bool StartDocument(XmlLocation l) => false;
    public bool Declaration(XmlDeclarationView d) => false;
    public bool StartElement(XmlNameView n, bool empty, XmlLocation l) { Elements++; return false; }
    public bool NamespaceDeclaration(XmlNamespaceView n) => false;
    public bool Attribute(XmlAttributeView a) => false;
    public bool StartElementContent() => false;
    public bool EndElement(XmlNameView n, XmlLocation l) { Ends++; return false; }
    public bool StartText(XmlTextKind k, ReadOnlySpan<char> target, XmlLocation l) => false;
    public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation l) { Units += text.Length; return false; }
    public bool EndDocument(XmlLocation l) { Documents++; return false; }
}

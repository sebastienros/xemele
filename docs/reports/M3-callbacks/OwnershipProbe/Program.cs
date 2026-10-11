using Xemele;
using Xemele.Streaming;
long before = GC.GetAllocatedBytesForCurrentThread();
var cold = Create();
long coldBytes = GC.GetAllocatedBytesForCurrentThread() - before;
GC.KeepAlive(cold);
// Retention is the incremental live owner graph, after JIT/initialization warmup.
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
long baseline = GC.GetTotalMemory(true);
const int count = 1000;
var retained = new Owner[count];
before = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < count; i++) retained[i] = Create();
long warmBytes = GC.GetAllocatedBytesForCurrentThread() - before;
long retainedBytes = GC.GetTotalMemory(true) - baseline;
Console.WriteLine($"Cold first complete owner graph: {coldBytes} B allocated");
Console.WriteLine($"Warm setup: {warmBytes / count} B/graph (1000 fresh graphs, container allocation excluded)");
Console.WriteLine($"Retained heap delta: {retainedBytes} B for 1000 live graphs (includes reference container; informational GC estimate)");
GC.KeepAlive(retained); GC.KeepAlive(cold);
static Owner Create()
{
    var sink = new Sink();
    var callbacks = new XmlSaxCallbacks
    {
        OnStartDocument = sink.StartDocument,
        OnEndDocument = sink.EndDocument,
        OnStartElement = sink.StartElement,
        OnEndElement = sink.EndElement,
        OnText = sink.Text,
        OnComment = sink.Comment,
        OnProcessingInstruction = sink.ProcessingInstruction,
        OnStartCData = sink.StartCData,
        OnEndCData = sink.EndCData,
        OnDeclaration = sink.Declaration,
        OnStartDtd = sink.StartDtd,
        OnDtdDeclaration = sink.DtdDeclaration,
        OnEndDtd = sink.EndDtd,
        OnSkippedEntity = sink.SkippedEntity,
        OnIncompleteAttributeDefault = sink.IncompleteAttributeDefault,
        OnDiagnostic = sink.Diagnostic,
    };
    XmlAttributeValuePart[] parts = [new XmlKnownAttributeText("text", default), new XmlUnknownAttributeReference("x", "&x;", default)];
    AttributeRow[] rows = [new("a", "", "", "value", false, true, default, default), new("b", "", "", "", true, false, parts, default)];
    NamespaceRow[] namespaces = [new("p", "urn:p", default)];
    return new(callbacks, rows, namespaces, parts);
}
sealed record Owner(XmlSaxCallbacks Callbacks, AttributeRow[] Rows, NamespaceRow[] Namespaces, XmlAttributeValuePart[] Parts);
sealed class Sink : XmlSaxHandler
{
    public override void StartDocument() { }
    public override void EndDocument() { }
    public override void StartElement(XmlStartElement element) { }
    public override void EndElement(XmlEndElement element) { }
    public override void Text(ReadOnlySpan<char> text) { }
    public override void Comment(ReadOnlySpan<char> text) { }
    public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) { }
    public override void StartCData() { }
    public override void EndCData() { }
    public override void Declaration(XmlDeclaration declaration) { }
    public override void StartDtd(XmlDocumentTypeView documentType) { }
    public override void DtdDeclaration(XmlDtdDeclaration declaration) { }
    public override void EndDtd() { }
    public override void SkippedEntity(XmlSkippedEntity entity) { }
    public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration) { }
    public override void Diagnostic(XmlDiagnostic diagnostic) { }
}

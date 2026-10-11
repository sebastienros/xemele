using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Streaming;
namespace Xemele.Benchmarks.Callbacks;
[MemoryDiagnoser]
public class CallbackBenchmarks
{
    private readonly Counter counter = new();
    private IXmlSaxHandler subclass = null!, delegates = null!, optional = null!;
    private static readonly AttributeRow[] Rows = [new("a", "", "", "value", false, true, default, default)];
    [GlobalSetup] public void Setup()
    {
        subclass = counter; optional = new XmlSaxCallbacks();
        delegates = new XmlSaxCallbacks
        {
            OnStartDocument = counter.StartDocument, OnEndDocument = counter.EndDocument,
            OnStartElement = counter.StartElement, OnEndElement = counter.EndElement,
            OnText = counter.Text, OnComment = counter.Comment, OnProcessingInstruction = counter.ProcessingInstruction,
            OnStartCData = counter.StartCData, OnEndCData = counter.EndCData, OnDeclaration = counter.Declaration,
            OnStartDtd = counter.StartDtd, OnDtdDeclaration = counter.DtdDeclaration, OnEndDtd = counter.EndDtd,
            OnSkippedEntity = counter.SkippedEntity, OnIncompleteAttributeDefault = counter.IncompleteAttributeDefault,
            OnDiagnostic = counter.Diagnostic
        };
    }
    private static readonly XmlDiagnostic DiagnosticValue = new("test", XmlDiagnosticSeverity.Warning, "message", default);
    private static readonly XmlIncompleteAttributeDefault Incomplete = new("r", "a", "&x;", false, false, default, default, Array.Empty<XmlAttributeValuePart>(), 0);
    private void Dispatch(IXmlSaxHandler h)
    {
        h.StartDocument(); h.EndDocument(); h.StartElement(new("r", "", "", new(Rows), default, true, default));
        h.EndElement(new("r", "", "", default)); h.Text("text Ω"); h.Comment("comment"); h.ProcessingInstruction("pi", "data");
        h.StartCData(); h.EndCData(); h.Declaration(new("1.0", "", null, default));
        h.StartDtd(new("r", "", "", false, false, default));
        h.DtdDeclaration(new(XmlDtdDeclarationKind.Notation, "n", "", "", "", XmlDtdDefaultKind.None, "", false, "", false, "", false, "", false, null, default));
        h.EndDtd(); h.SkippedEntity(new("x", "&x;", false, default)); h.IncompleteAttributeDefault(Incomplete); h.Diagnostic(DiagnosticValue);
    }
    [Benchmark(Baseline = true)] public int Subclass() { Dispatch(subclass); return counter.Total; }
    [Benchmark] public int Delegates() { Dispatch(delegates); return counter.Total; }
    [Benchmark] public void Optional() => Dispatch(optional);
    private sealed class Counter : XmlSaxHandler
    {
        internal int Total;
        public override void StartDocument() => Total++;
        public override void EndDocument() => Total++;
        public override void StartElement(XmlStartElement e) { foreach (var a in e.Attributes) Total += a.Value.Length; Total++; }
        public override void EndElement(XmlEndElement e) => Total++;
        public override void Text(ReadOnlySpan<char> t) => Total++;
        public override void Comment(ReadOnlySpan<char> t) => Total++;
        public override void ProcessingInstruction(ReadOnlySpan<char> t, ReadOnlySpan<char> d) => Total++;
        public override void StartCData() => Total++;
        public override void EndCData() => Total++;
        public override void Declaration(XmlDeclaration d) => Total++;
        public override void StartDtd(XmlDocumentTypeView d) => Total++;
        public override void DtdDeclaration(XmlDtdDeclaration d) => Total++;
        public override void EndDtd() => Total++;
        public override void SkippedEntity(XmlSkippedEntity e) => Total++;
        public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault d) => Total++;
        public override void Diagnostic(XmlDiagnostic d) => Total++;
    }
}

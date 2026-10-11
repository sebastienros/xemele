using Xemele;
namespace Xemele.Streaming;
public delegate void XmlStartDocumentCallback();
public delegate void XmlEndDocumentCallback();
public delegate void XmlStartElementCallback(XmlStartElement element);
public delegate void XmlEndElementCallback(XmlEndElement element);
public delegate void XmlTextCallback(ReadOnlySpan<char> text);
public delegate void XmlCommentCallback(ReadOnlySpan<char> text);
public delegate void XmlProcessingInstructionCallback(ReadOnlySpan<char> target, ReadOnlySpan<char> data);
public delegate void XmlStartCDataCallback();
public delegate void XmlEndCDataCallback();
public delegate void XmlDeclarationCallback(XmlDeclaration declaration);
public delegate void XmlStartDtdCallback(XmlDocumentTypeView documentType);
public delegate void XmlDtdDeclarationCallback(XmlDtdDeclaration declaration);
public delegate void XmlEndDtdCallback();
public delegate void XmlSkippedEntityCallback(XmlSkippedEntity entity);
public delegate void XmlIncompleteAttributeDefaultCallback(XmlIncompleteAttributeDefault declaration);
public delegate void XmlDiagnosticCallback(XmlDiagnostic diagnostic);
public interface IXmlSaxHandler
{
    void StartDocument();
    void EndDocument();
    void StartElement(XmlStartElement element);
    void EndElement(XmlEndElement element);
    void Text(ReadOnlySpan<char> text);
    void Comment(ReadOnlySpan<char> text);
    void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data);
    void StartCData();
    void EndCData();
    void Declaration(XmlDeclaration declaration);
    void StartDtd(XmlDocumentTypeView documentType);
    void DtdDeclaration(XmlDtdDeclaration declaration);
    void EndDtd();
    void SkippedEntity(XmlSkippedEntity entity);
    void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration);
    void Diagnostic(XmlDiagnostic diagnostic);
}
public abstract class XmlSaxHandler : IXmlSaxHandler
{
    public virtual void StartDocument() { }
    public virtual void EndDocument() { }
    public virtual void StartElement(XmlStartElement element) { }
    public virtual void EndElement(XmlEndElement element) { }
    public virtual void Text(ReadOnlySpan<char> text) { }
    public virtual void Comment(ReadOnlySpan<char> text) { }
    public virtual void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) { }
    public virtual void StartCData() { }
    public virtual void EndCData() { }
    public virtual void Declaration(XmlDeclaration declaration) { }
    public virtual void StartDtd(XmlDocumentTypeView documentType) { }
    public virtual void DtdDeclaration(XmlDtdDeclaration declaration) { }
    public virtual void EndDtd() { }
    public virtual void SkippedEntity(XmlSkippedEntity entity) { }
    public virtual void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration) { }
    public virtual void Diagnostic(XmlDiagnostic diagnostic) { }
}
public sealed class XmlSaxCallbacks : XmlSaxHandler
{
    public XmlStartDocumentCallback? OnStartDocument { get; init; }
    public override void StartDocument() => OnStartDocument?.Invoke();
    public XmlEndDocumentCallback? OnEndDocument { get; init; }
    public override void EndDocument() => OnEndDocument?.Invoke();
    public XmlStartElementCallback? OnStartElement { get; init; }
    public override void StartElement(XmlStartElement element) => OnStartElement?.Invoke(element);
    public XmlEndElementCallback? OnEndElement { get; init; }
    public override void EndElement(XmlEndElement element) => OnEndElement?.Invoke(element);
    public XmlTextCallback? OnText { get; init; }
    public override void Text(ReadOnlySpan<char> text) => OnText?.Invoke(text);
    public XmlCommentCallback? OnComment { get; init; }
    public override void Comment(ReadOnlySpan<char> text) => OnComment?.Invoke(text);
    public XmlProcessingInstructionCallback? OnProcessingInstruction { get; init; }
    public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) => OnProcessingInstruction?.Invoke(target, data);
    public XmlStartCDataCallback? OnStartCData { get; init; }
    public override void StartCData() => OnStartCData?.Invoke();
    public XmlEndCDataCallback? OnEndCData { get; init; }
    public override void EndCData() => OnEndCData?.Invoke();
    public XmlDeclarationCallback? OnDeclaration { get; init; }
    public override void Declaration(XmlDeclaration declaration) => OnDeclaration?.Invoke(declaration);
    public XmlStartDtdCallback? OnStartDtd { get; init; }
    public override void StartDtd(XmlDocumentTypeView documentType) => OnStartDtd?.Invoke(documentType);
    public XmlDtdDeclarationCallback? OnDtdDeclaration { get; init; }
    public override void DtdDeclaration(XmlDtdDeclaration declaration) => OnDtdDeclaration?.Invoke(declaration);
    public XmlEndDtdCallback? OnEndDtd { get; init; }
    public override void EndDtd() => OnEndDtd?.Invoke();
    public XmlSkippedEntityCallback? OnSkippedEntity { get; init; }
    public override void SkippedEntity(XmlSkippedEntity entity) => OnSkippedEntity?.Invoke(entity);
    public XmlIncompleteAttributeDefaultCallback? OnIncompleteAttributeDefault { get; init; }
    public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration) => OnIncompleteAttributeDefault?.Invoke(declaration);
    public XmlDiagnosticCallback? OnDiagnostic { get; init; }
    public override void Diagnostic(XmlDiagnostic diagnostic) => OnDiagnostic?.Invoke(diagnostic);
}

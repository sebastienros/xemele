using Xemele;
namespace Xemele.Streaming;

/// <summary>Receives notification that document parsing has started.</summary>
public delegate void XmlStartDocumentCallback();
/// <summary>Receives notification that document parsing has finished.</summary>
public delegate void XmlEndDocumentCallback();
/// <summary>Receives a borrowed view of an element start tag.</summary>
/// <param name="element">The element view, valid only during the callback.</param>
public delegate void XmlStartElementCallback(XmlStartElement element);
/// <summary>Receives a borrowed view of an element end tag.</summary>
/// <param name="element">The element view, valid only during the callback.</param>
public delegate void XmlEndElementCallback(XmlEndElement element);
/// <summary>Receives a chunk of character content.</summary>
/// <param name="text">The borrowed characters, valid only during the callback.</param>
public delegate void XmlTextCallback(ReadOnlySpan<char> text);
/// <summary>Receives comment content without its markup delimiters.</summary>
/// <param name="text">The borrowed comment text, valid only during the callback.</param>
public delegate void XmlCommentCallback(ReadOnlySpan<char> text);
/// <summary>Receives a processing instruction without its markup delimiters.</summary>
/// <param name="target">The borrowed target, valid only during the callback.</param>
/// <param name="data">The borrowed data, valid only during the callback.</param>
public delegate void XmlProcessingInstructionCallback(ReadOnlySpan<char> target, ReadOnlySpan<char> data);
/// <summary>Receives notification that a CDATA section has started.</summary>
public delegate void XmlStartCDataCallback();
/// <summary>Receives notification that a CDATA section has ended.</summary>
public delegate void XmlEndCDataCallback();
/// <summary>Receives the XML declaration.</summary>
/// <param name="declaration">The borrowed declaration view, valid only during the callback.</param>
public delegate void XmlDeclarationCallback(XmlDeclaration declaration);
/// <summary>Receives the start of a document type declaration.</summary>
/// <param name="documentType">The borrowed document type view, valid only during the callback.</param>
public delegate void XmlStartDtdCallback(XmlDocumentTypeView documentType);
/// <summary>Receives a declaration from the document type definition.</summary>
/// <param name="declaration">The borrowed declaration view, valid only during the callback.</param>
public delegate void XmlDtdDeclarationCallback(XmlDtdDeclaration declaration);
/// <summary>Receives notification that document type processing has ended.</summary>
public delegate void XmlEndDtdCallback();
/// <summary>Receives an entity reference that was not expanded.</summary>
/// <param name="entity">The borrowed entity view, valid only during the callback.</param>
public delegate void XmlSkippedEntityCallback(XmlSkippedEntity entity);
/// <summary>Receives a default attribute declaration whose value could not be fully expanded.</summary>
/// <param name="declaration">The incomplete default declaration.</param>
public delegate void XmlIncompleteAttributeDefaultCallback(XmlIncompleteAttributeDefault declaration);
/// <summary>Receives a parsing diagnostic.</summary>
/// <param name="diagnostic">The reported diagnostic.</param>
public delegate void XmlDiagnosticCallback(XmlDiagnostic diagnostic);
/// <summary>Receives synchronous notifications from SAX parsing.</summary>
/// <remarks>Spans and ref-struct views are borrowed and expire when the receiving method returns. Copy any data that must be retained.</remarks>
public interface IXmlSaxHandler
{
    /// <summary>Notifies the handler that document parsing has started.</summary>
    void StartDocument();
    /// <summary>Notifies the handler that document parsing has finished.</summary>
    void EndDocument();
    /// <summary>Receives an element start tag.</summary>
    /// <param name="element">The borrowed start-tag view.</param>
    void StartElement(XmlStartElement element);
    /// <summary>Receives an element end tag.</summary>
    /// <param name="element">The borrowed end-tag view.</param>
    void EndElement(XmlEndElement element);
    /// <summary>Receives a chunk of character content, including content within CDATA sections.</summary>
    /// <param name="text">The borrowed character content.</param>
    void Text(ReadOnlySpan<char> text);
    /// <summary>Receives comment content without markup delimiters.</summary>
    /// <param name="text">The borrowed comment content.</param>
    void Comment(ReadOnlySpan<char> text);
    /// <summary>Receives a processing instruction.</summary>
    /// <param name="target">The borrowed target name.</param>
    /// <param name="data">The borrowed instruction data.</param>
    void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data);
    /// <summary>Notifies the handler that a CDATA section has started.</summary>
    void StartCData();
    /// <summary>Notifies the handler that a CDATA section has ended.</summary>
    void EndCData();
    /// <summary>Receives the XML declaration.</summary>
    /// <param name="declaration">The borrowed XML declaration view.</param>
    void Declaration(XmlDeclaration declaration);
    /// <summary>Receives the start of a document type declaration.</summary>
    /// <param name="documentType">The borrowed document type view.</param>
    void StartDtd(XmlDocumentTypeView documentType);
    /// <summary>Receives a declaration from the document type definition.</summary>
    /// <param name="declaration">The borrowed DTD declaration view.</param>
    void DtdDeclaration(XmlDtdDeclaration declaration);
    /// <summary>Notifies the handler that document type processing has ended.</summary>
    void EndDtd();
    /// <summary>Receives an entity reference that was not expanded.</summary>
    /// <param name="entity">The borrowed skipped-entity view.</param>
    void SkippedEntity(XmlSkippedEntity entity);
    /// <summary>Receives a default attribute declaration with incomplete expansion.</summary>
    /// <param name="declaration">The incomplete default declaration.</param>
    void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration);
    /// <summary>Receives a parsing diagnostic.</summary>
    /// <param name="diagnostic">The reported diagnostic.</param>
    void Diagnostic(XmlDiagnostic diagnostic);
}
/// <summary>Provides no-op SAX handlers that derived classes can selectively override.</summary>
public abstract class XmlSaxHandler : IXmlSaxHandler
{
    /// <inheritdoc/>
    public virtual void StartDocument()
    {
    }
    /// <inheritdoc/>
    public virtual void EndDocument()
    {
    }
    /// <inheritdoc/>
    public virtual void StartElement(XmlStartElement element)
    {
    }
    /// <inheritdoc/>
    public virtual void EndElement(XmlEndElement element)
    {
    }
    /// <inheritdoc/>
    public virtual void Text(ReadOnlySpan<char> text)
    {
    }
    /// <inheritdoc/>
    public virtual void Comment(ReadOnlySpan<char> text)
    {
    }
    /// <inheritdoc/>
    public virtual void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data)
    {
    }
    /// <inheritdoc/>
    public virtual void StartCData()
    {
    }
    /// <inheritdoc/>
    public virtual void EndCData()
    {
    }
    /// <inheritdoc/>
    public virtual void Declaration(XmlDeclaration declaration)
    {
    }
    /// <inheritdoc/>
    public virtual void StartDtd(XmlDocumentTypeView documentType)
    {
    }
    /// <inheritdoc/>
    public virtual void DtdDeclaration(XmlDtdDeclaration declaration)
    {
    }
    /// <inheritdoc/>
    public virtual void EndDtd()
    {
    }
    /// <inheritdoc/>
    public virtual void SkippedEntity(XmlSkippedEntity entity)
    {
    }
    /// <inheritdoc/>
    public virtual void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration)
    {
    }
    /// <inheritdoc/>
    public virtual void Diagnostic(XmlDiagnostic diagnostic)
    {
    }
}
/// <summary>Dispatches SAX notifications to optional delegates; unset callbacks are ignored.</summary>
public sealed class XmlSaxCallbacks : XmlSaxHandler
{
    /// <summary>Gets the callback invoked when document parsing starts.</summary>
    public XmlStartDocumentCallback? OnStartDocument
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void StartDocument() => OnStartDocument?.Invoke();
    /// <summary>Gets the callback invoked when document parsing finishes.</summary>
    public XmlEndDocumentCallback? OnEndDocument
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void EndDocument() => OnEndDocument?.Invoke();
    /// <summary>Gets the callback receiving borrowed element start tags.</summary>
    public XmlStartElementCallback? OnStartElement
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void StartElement(XmlStartElement element) => OnStartElement?.Invoke(element);
    /// <summary>Gets the callback receiving borrowed element end tags.</summary>
    public XmlEndElementCallback? OnEndElement
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void EndElement(XmlEndElement element) => OnEndElement?.Invoke(element);
    /// <summary>Gets the callback receiving borrowed character-content chunks.</summary>
    public XmlTextCallback? OnText
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void Text(ReadOnlySpan<char> text) => OnText?.Invoke(text);
    /// <summary>Gets the callback receiving borrowed comment content.</summary>
    public XmlCommentCallback? OnComment
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void Comment(ReadOnlySpan<char> text) => OnComment?.Invoke(text);
    /// <summary>Gets the callback receiving borrowed processing instructions.</summary>
    public XmlProcessingInstructionCallback? OnProcessingInstruction
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) => OnProcessingInstruction?.Invoke(target, data);
    /// <summary>Gets the callback invoked at the start of a CDATA section.</summary>
    public XmlStartCDataCallback? OnStartCData
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void StartCData() => OnStartCData?.Invoke();
    /// <summary>Gets the callback invoked at the end of a CDATA section.</summary>
    public XmlEndCDataCallback? OnEndCData
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void EndCData() => OnEndCData?.Invoke();
    /// <summary>Gets the callback receiving the borrowed XML declaration.</summary>
    public XmlDeclarationCallback? OnDeclaration
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void Declaration(XmlDeclaration declaration) => OnDeclaration?.Invoke(declaration);
    /// <summary>Gets the callback receiving the borrowed document type header.</summary>
    public XmlStartDtdCallback? OnStartDtd
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void StartDtd(XmlDocumentTypeView documentType) => OnStartDtd?.Invoke(documentType);
    /// <summary>Gets the callback receiving borrowed DTD declarations.</summary>
    public XmlDtdDeclarationCallback? OnDtdDeclaration
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void DtdDeclaration(XmlDtdDeclaration declaration) => OnDtdDeclaration?.Invoke(declaration);
    /// <summary>Gets the callback invoked when document type processing ends.</summary>
    public XmlEndDtdCallback? OnEndDtd
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void EndDtd() => OnEndDtd?.Invoke();
    /// <summary>Gets the callback receiving borrowed skipped-entity information.</summary>
    public XmlSkippedEntityCallback? OnSkippedEntity
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void SkippedEntity(XmlSkippedEntity entity) => OnSkippedEntity?.Invoke(entity);
    /// <summary>Gets the callback receiving incompletely expanded attribute defaults.</summary>
    public XmlIncompleteAttributeDefaultCallback? OnIncompleteAttributeDefault
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration) => OnIncompleteAttributeDefault?.Invoke(declaration);
    /// <summary>Gets the callback receiving parsing diagnostics.</summary>
    public XmlDiagnosticCallback? OnDiagnostic
    {
        get; init;
    }
    /// <inheritdoc/>
    public override void Diagnostic(XmlDiagnostic diagnostic) => OnDiagnostic?.Invoke(diagnostic);
}

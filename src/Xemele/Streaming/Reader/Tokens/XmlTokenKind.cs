namespace Xemele.Streaming;

/// <summary>Identifies the current event of a forward XML reader.</summary>
public enum XmlTokenKind
{
    /// <summary>No event is current.</summary>
    None = 0,
    /// <summary>Document parsing has started.</summary>
    StartDocument = 1,
    /// <summary>An XML declaration.</summary>
    Declaration = 2,
    /// <summary>An element start tag, with attributes and namespace declarations.</summary>
    StartElement = 3,
    /// <summary>An element has ended.</summary>
    EndElement = 4,
    /// <summary>A chunk of character content.</summary>
    Text = 5,
    /// <summary>A comment's content.</summary>
    Comment = 6,
    /// <summary>A processing instruction's target and data.</summary>
    ProcessingInstruction = 7,
    /// <summary>A CDATA section has started.</summary>
    StartCData = 8,
    /// <summary>A CDATA section has ended.</summary>
    EndCData = 9,
    /// <summary>The header of a document type declaration.</summary>
    StartDtd = 10,
    /// <summary>An element, attribute, entity, or notation declaration.</summary>
    DtdDeclaration = 11,
    /// <summary>Document type processing has ended.</summary>
    EndDtd = 12,
    /// <summary>An entity reference was not expanded.</summary>
    SkippedEntity = 13,
    /// <summary>An attribute default could not be fully expanded.</summary>
    IncompleteAttributeDefault = 14,
    /// <summary>A parsing diagnostic.</summary>
    Diagnostic = 15,
    /// <summary>Document parsing has finished.</summary>
    EndDocument = 16
}

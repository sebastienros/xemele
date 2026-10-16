namespace Xemele;

// Immutable lexical metadata, separate from the document node arena. Strings
// belong to the record; no borrowed source or decoding buffers are retained.
/// <summary>Contains immutable lexical metadata from a document type declaration.</summary>
/// <param name="Name">The declared document type name.</param>
/// <param name="PublicId">The public identifier, or <see langword="null"/> when absent.</param>
/// <param name="SystemId">The system identifier, or <see langword="null"/> when absent.</param>
/// <param name="InternalSubset">The internal subset text, or <see langword="null"/> when absent.</param>
/// <remarks>Strings are owned values; this record does not retain borrowed input buffers.</remarks>
public sealed record XmlDocumentType(string Name, string? PublicId, string? SystemId, string? InternalSubset);

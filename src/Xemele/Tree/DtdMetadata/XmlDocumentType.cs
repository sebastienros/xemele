namespace Xemele;

// Immutable lexical metadata, separate from the document node arena. Strings
// belong to the record; no borrowed source or decoding buffers are retained.
public sealed record XmlDocumentType(string Name, string? PublicId, string? SystemId, string? InternalSubset);

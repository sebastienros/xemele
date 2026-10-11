namespace Xemele.Validation;

internal sealed class SchemaCompileContext
{
    internal SchemaCompileOptions Options { get; }
    internal SchemaCompileOperation Operation { get; }
    internal SchemaResourceLoader Resources { get; }
    private readonly SchemaParsing parsing;

    internal SchemaCompileContext(SchemaCompileOptions? options = null)
    {
        Options = SchemaSnapshots.Compile(options);
        Operation = new(Options); parsing = new(Operation); Resources = new(Options, Operation, parsing);
    }
    internal SchemaDocumentScope Open(XmlDocument document, Uri? sourceUri = null) => new(document, Operation, sourceUri ?? Resources.SourceUri(document));
    internal SchemaDocumentScope Parse(string input) => Open(parsing.Parse(input, Options.BaseUri));
    internal SchemaDocumentScope Parse(Stream input) => Open(parsing.Parse(input, Options.BaseUri));
}

// A traversal guard borrows a document for this compilation only. Published engines
// must store their immutable owned graph, never this scope or its node handles.
internal sealed class SchemaDocumentScope
{
    private readonly long revision;
    private readonly ISchemaOperation operation;
    internal XmlDocument Document { get; }
    internal Uri? SourceUri { get; }
    internal SchemaDocumentScope(XmlDocument document, ISchemaOperation operation, Uri? sourceUri)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(operation);
        if (sourceUri is { IsAbsoluteUri: false }) throw new ArgumentException("Absolute source URI required.", nameof(sourceUri));
        this.operation = operation; operation.CancellationToken.ThrowIfCancellationRequested();
        if (!document.SourceInputComplete)
        {
            operation.Refuse("SCHEMA.IncompleteInput", "Schema input is incomplete.", new(sourceUri, 0, 0, null));
            throw SchemaSnapshots.Failure(operation);
        }
        Document = document; SourceUri = sourceUri; revision = document.ContentRevision; CheckUnchanged();
    }
    internal void CheckUnchanged()
    {
        operation.CancellationToken.ThrowIfCancellationRequested();
        if (revision < 0 || Document.ContentRevision != revision) throw new InvalidOperationException("Schema source changed during compilation.");
    }
    internal SchemaNamespaceContext Context(XmlNode element)
    {
        if (!ReferenceEquals(element.OwnerDocument, Document)) throw new ArgumentException("Node belongs to another source.", nameof(element));
        CheckUnchanged(); var context = SchemaSnapshots.Context(element, operation, SourceUri); CheckUnchanged(); return context;
    }
    internal XmlLocation Location(XmlNode node)
    {
        CheckUnchanged(); var location = Document.OriginalLocation(node); CheckUnchanged();
        return location with { SourceUri = location.SourceUri ?? SourceUri };
    }
    internal XmlLocation Location(XmlAttribute attribute)
    {
        CheckUnchanged(); var location = Document.OriginalLocation(attribute); CheckUnchanged();
        return location with { SourceUri = location.SourceUri ?? SourceUri };
    }
    internal XmlLocation Location(XmlNamespaceDeclaration declaration)
    {
        CheckUnchanged(); var location = Document.OriginalLocation(declaration); CheckUnchanged();
        return location with { SourceUri = location.SourceUri ?? SourceUri };
    }
}

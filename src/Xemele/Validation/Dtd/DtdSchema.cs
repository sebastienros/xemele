using Xemele.Parsing.Dtd;

namespace Xemele.Validation;

/// <summary>Contains compiled DTD declarations for validating XML documents.</summary>
/// <remarks>Compilation retains the declaration model, while each validation call uses separate operation state. Validation does not apply default attributes to the document.</remarks>
public sealed class DtdSchema
{
    private readonly CompiledDtdModel model;
    private readonly string? rootName;
    private DtdSchema(CompiledDtdModel model, string? rootName)
    {
        this.model = model;
        this.rootName = rootName;
    }

    /// <summary>Compiles a DTD subset without a supplied source URI.</summary>
    /// <param name="dtd">The DTD subset text, not an XML document.</param>
    /// <param name="resolver">The resolver for external DTD resources, or null to deny external resolution.</param>
    /// <param name="limits">The resource limits, or null to use defaults.</param>
    /// <param name="cancellationToken">The token used to cancel compilation.</param>
    /// <returns>The compiled declarations, without a document-type root-name constraint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dtd"/> is null.</exception>
    /// <exception cref="XmlParseException">DTD parsing or compilation fails, including incomplete input or a resource-limit refusal.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static DtdSchema Compile(string dtd, XmlResourceResolver? resolver = null,
        XmlResourceLimits? limits = null, CancellationToken cancellationToken = default)
        => CompileCore(dtd, resolver, limits, null, cancellationToken);

    /// <summary>Compiles a DTD subset with an absolute source URI for resource resolution and diagnostics.</summary>
    /// <param name="dtd">The DTD subset text, not an XML document.</param>
    /// <param name="resolver">The resolver for external DTD resources, or null to deny external resolution.</param>
    /// <param name="limits">The resource limits, or null to use defaults.</param>
    /// <param name="sourceUri">The absolute URI identifying the subset.</param>
    /// <param name="cancellationToken">The token used to cancel compilation.</param>
    /// <returns>The compiled declarations, without a document-type root-name constraint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dtd"/> or <paramref name="sourceUri"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceUri"/> is relative.</exception>
    /// <exception cref="XmlParseException">DTD parsing or compilation fails, including incomplete input or a resource-limit refusal.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static DtdSchema Compile(string dtd, XmlResourceResolver? resolver,
        XmlResourceLimits? limits, Uri sourceUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);
        if (!sourceUri.IsAbsoluteUri)
        {
            throw new ArgumentException("Source URI must be absolute.", nameof(sourceUri));
        }

        return CompileCore(dtd, resolver, limits, sourceUri, cancellationToken);
    }

    private static DtdSchema CompileCore(string dtd, XmlResourceResolver? resolver,
        XmlResourceLimits? limits, Uri? sourceUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dtd);
        var operation = new ValidationOperation(new()
        {
            Limits = limits ?? new(),
            CancellationToken = cancellationToken
        });
        var source = DtdSubsetReader.Parse(dtd, resolver, operation, sourceUri);
        return Create(source, null, operation);
    }

    /// <summary>Compiles a document's retained DTD declarations and document-type root-name constraint.</summary>
    /// <param name="document">The parsed document containing the retained DTD model.</param>
    /// <returns>The compiled schema derived from the document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The document has no retained DTD model or changes during compilation.</exception>
    /// <exception cref="XmlParseException">The retained DTD is invalid, required input is incomplete, or compilation exceeds its limits.</exception>
    public static DtdSchema FromDocument(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var revision = document.ContentRevision;
        DtdEvaluator.CheckRevision(document, revision);
        var source = document.Dtd ?? throw new InvalidOperationException("Document has no retained DTD model.");
        if (source.DeclarationValidityDiagnostic is { } diagnostic)
        {
            throw new XmlParseException([diagnostic]);
        }

        if (source.UnreadParameterLocation is { } unread)
        {
            throw new XmlParseException([new("DTD.IncompleteInput", XmlDiagnosticSeverity.Fatal, "DTD input is incomplete.", unread)]);
        }

        var operation = new ValidationOperation();
        var undeclared = document.UnresolvedReferences.Count != 0 && DtdEvaluator.ProvesUndeclaredReferences(document, operation);
        if (!operation.IsComplete)
        {
            throw new XmlParseException(operation.Complete().Diagnostics);
        }

        if (!document.SourceInputComplete || document.HasIncompleteAttributes ||
            document.UnresolvedReferences.Count != 0 && !undeclared)
        {
            throw new XmlParseException([new("DTD.IncompleteInput", XmlDiagnosticSeverity.Fatal, "Document input is incomplete.", default)]);
        }

        var schema = Create(source, document.DocumentType?.Name, operation);
        DtdEvaluator.CheckRevision(document, revision);
        return schema;
    }

    private static DtdSchema Create(DtdModel source, string? rootName, ValidationOperation operation)
    {
        if (source.DeclarationValidityDiagnostic is { } diagnostic)
        {
            throw new XmlParseException([diagnostic]);
        }

        return new(DtdCompiler.Compile(source, operation), rootName);
    }

    /// <summary>Checks a document against the compiled DTD without modifying it.</summary>
    /// <param name="document">The XML document to validate.</param>
    /// <param name="options">The operation budgets and cancellation token, or null to use defaults.</param>
    /// <returns>The validity, completion status, and diagnostics; a stopped assessment is not valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The document changes during validation.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public ValidationResult Validate(XmlDocument document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var operation = new ValidationOperation(options);
        return new DtdEvaluator(model, rootName, document, operation).Run();
    }
}

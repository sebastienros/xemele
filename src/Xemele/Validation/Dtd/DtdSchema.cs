using Xemele.Parsing.Dtd;

namespace Xemele.Validation;

public sealed class DtdSchema
{
    private readonly CompiledDtdModel model;
    private readonly string? rootName;
    private DtdSchema(CompiledDtdModel model, string? rootName) { this.model = model; this.rootName = rootName; }

    public static DtdSchema Compile(string dtd, XmlResourceResolver? resolver = null,
        XmlResourceLimits? limits = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dtd);
        var operation = new ValidationOperation(new() { Limits = limits ?? new(), CancellationToken = cancellationToken });
        var source = DtdSubsetReader.Parse(dtd, resolver, operation);
        return Create(source, null, operation);
    }

    public static DtdSchema FromDocument(XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        long revision = document.ContentRevision;
        DtdEvaluator.CheckRevision(document, revision);
        var source = document.Dtd ?? throw new InvalidOperationException("Document has no retained DTD model.");
        if (source.DeclarationValidityDiagnostic is { } diagnostic) throw new XmlParseException([diagnostic]);
        if (!document.SourceInputComplete || document.HasIncompleteAttributes || document.UnresolvedReferences.Count != 0)
            throw new XmlParseException([new("DTD.IncompleteInput", XmlDiagnosticSeverity.Fatal, "Document input is incomplete.", default)]);
        var schema = Create(source, document.DocumentType?.Name, new());
        DtdEvaluator.CheckRevision(document, revision);
        return schema;
    }

    private static DtdSchema Create(DtdModel source, string? rootName, ValidationOperation operation)
    {
        if (source.DeclarationValidityDiagnostic is { } diagnostic) throw new XmlParseException([diagnostic]);
        return new(DtdCompiler.Compile(source, operation), rootName);
    }

    public ValidationResult Validate(XmlDocument document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var operation = new ValidationOperation(options);
        return new DtdEvaluator(model, rootName, document, operation).Run();
    }
}

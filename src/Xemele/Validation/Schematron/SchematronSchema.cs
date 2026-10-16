namespace Xemele.Validation;

/// <summary>Contains a compiled ISO Schematron 2016 schema using the XPath 1.0 query binding.</summary>
/// <remarks>Compiled rules and expressions are detached from source documents; each validation creates its own evaluation state. Multi-document bindings and result-tree-valued lets are not supported.</remarks>
public sealed class SchematronSchema
{
    private readonly SchematronCompiledModel model;
    private SchematronSchema()
    {
        model = null!;
    }
    private SchematronSchema(SchematronCompiledModel model) => this.model = model;

    /// <summary>Compiles a parsed Schematron schema using the supported XPath 1.0 profile.</summary>
    /// <param name="schema">The complete Schematron schema document.</param>
    /// <param name="options">The schema, query-binding, and XPath compilation options, or null to use defaults.</param>
    /// <returns>The compiled schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="SchemaCompileException">The schema is invalid, uses unsupported constructs, or compilation cannot complete within resource policy.</exception>
    /// <exception cref="InvalidOperationException">The schema changes during compilation.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static SchematronSchema Compile(XmlDocument schema, SchematronCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var snapshot = SchemaSnapshots.Compile(options ?? new());
        return new(new SchematronCompiler().Compile(schema, snapshot, new SchemaCompileOperation(snapshot.Schema)));
    }
    /// <summary>Parses and compiles Schematron schema text with DTD processing prohibited.</summary>
    /// <param name="schema">The Schematron XML text.</param>
    /// <param name="options">The schema, query-binding, and XPath compilation options, or null to use defaults.</param>
    /// <returns>The compiled schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="SchemaCompileException">Parsing or compilation fails, including unsupported constructs or resource-policy refusal.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static SchematronSchema Compile(string schema, SchematronCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var snapshot = SchemaSnapshots.Compile(options ?? new());
        var context = new SchemaCompileContext(snapshot.Schema);
        return new(new SchematronCompiler().Compile(context.Parse(schema).Document, snapshot, context.Operation));
    }
    /// <summary>Reads and compiles a Schematron schema with DTD processing prohibited.</summary>
    /// <param name="schema">The readable schema stream, consumed from its current position and left open.</param>
    /// <param name="options">The schema, query-binding, and XPath compilation options, or null to use defaults.</param>
    /// <returns>The compiled schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is not readable.</exception>
    /// <exception cref="SchemaCompileException">Parsing or compilation fails, including unsupported constructs or resource-policy refusal.</exception>
    /// <exception cref="IOException">Reading the caller's stream fails.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static SchematronSchema Compile(Stream schema, SchematronCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var snapshot = SchemaSnapshots.Compile(options ?? new());
        var context = new SchemaCompileContext(snapshot.Schema);
        return new(new SchematronCompiler().Compile(context.Parse(schema).Document, snapshot, context.Operation));
    }
    /// <summary>Evaluates the selected phase against a parsed instance without modifying it.</summary>
    /// <param name="document">The XML instance document.</param>
    /// <param name="options">The phase, validation budgets, and XPath evaluation limits, or null to use defaults.</param>
    /// <returns>The validation status and detached pattern, rule-firing, and finding snapshots.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">The selected phase is unknown.</exception>
    /// <exception cref="InvalidOperationException">The instance changes during validation.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public SchematronResult Validate(XmlDocument document, SchematronValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new SchematronEvaluator().Validate(model, document, options ?? new());
    }
    /// <summary>Parses XML instance text and evaluates the selected Schematron phase.</summary>
    /// <param name="document">The instance XML text; DTD processing is prohibited.</param>
    /// <param name="options">The phase, shared parsing and validation budgets, and XPath evaluation limits, or null to use defaults.</param>
    /// <returns>The validation status and detached report data, including diagnostics for instance parsing failures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">The selected phase is unknown.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public SchematronResult Validate(string document, SchematronValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var snapshot = SchemaSnapshots.Validation(options ?? new());
        var phase = SchematronEvaluator.SelectPhase(model, snapshot.Phase);
        var operation = new SchemaValidationOperation(snapshot.Validation);
        var parsed = SchematronInput.Parse(document, operation);
        return parsed is null ? SchematronEvaluator.Stopped(model, phase, operation) : SchematronEvaluator.Evaluate(model, parsed, snapshot, phase, operation);
    }
    /// <summary>Reads an XML instance and evaluates the selected Schematron phase.</summary>
    /// <param name="document">The readable instance stream, consumed from its current position and left open; DTD processing is prohibited.</param>
    /// <param name="options">The phase, shared parsing and validation budgets, and XPath evaluation limits, or null to use defaults.</param>
    /// <returns>The validation status and detached report data, including diagnostics for instance parsing failures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is not readable or the selected phase is unknown.</exception>
    /// <exception cref="IOException">Reading the caller's stream fails.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public SchematronResult Validate(Stream document, SchematronValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.CanRead)
        {
            throw new ArgumentException("Readable stream required.", nameof(document));
        }

        var snapshot = SchemaSnapshots.Validation(options ?? new());
        var phase = SchematronEvaluator.SelectPhase(model, snapshot.Phase);
        var operation = new SchemaValidationOperation(snapshot.Validation);
        var parsed = SchematronInput.Parse(document, operation);
        return parsed is null ? SchematronEvaluator.Stopped(model, phase, operation) : SchematronEvaluator.Evaluate(model, parsed, snapshot, phase, operation);
    }
}

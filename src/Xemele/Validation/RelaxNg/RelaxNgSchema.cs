namespace Xemele.Validation;

/// <summary>Contains a compiled RELAX NG XML-syntax schema for repeated instance validation.</summary>
/// <remarks>Compact syntax is not accepted. The supported datatype libraries are the built-in RELAX NG library and the XML Schema datatype library. Compiled patterns are retained independently of source documents; evaluation state is created per operation.</remarks>
public sealed class RelaxNgSchema
{
    private readonly RngProgram program;
    private RelaxNgSchema()
    {
        program = null!;
    }
    private RelaxNgSchema(RngProgram program) => this.program = program;
    private static RelaxNgSchema Compile(SchemaCompileContext context, XmlDocument schema)
    {
        var revision = schema.ContentRevision;
        var graph = new RelaxNgCompiler().Compile(schema, context.Options, context.Operation);
        var program = RngProgram.Finalize(graph, context.Operation);
        context.Options.CancellationToken.ThrowIfCancellationRequested();
        if (revision < 0 || schema.ContentRevision != revision)
        {
            throw new InvalidOperationException("Schema source changed during compilation.");
        }

        return new(program);
    }
    /// <summary>Compiles a parsed RELAX NG XML-syntax schema.</summary>
    /// <param name="schema">The complete schema document.</param>
    /// <param name="options">The resource-resolution and compilation options, or null to use defaults.</param>
    /// <returns>The compiled schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="SchemaCompileException">The schema is invalid, uses unsupported constructs, or compilation cannot complete within resource policy.</exception>
    /// <exception cref="InvalidOperationException">The schema document changes during compilation.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static RelaxNgSchema Compile(XmlDocument schema, SchemaCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return Compile(new(options), schema);
    }
    /// <summary>Parses and compiles RELAX NG XML-syntax schema text with DTD processing prohibited.</summary>
    /// <param name="schema">The XML schema text.</param>
    /// <param name="options">The resource-resolution and compilation options, or null to use defaults.</param>
    /// <returns>The compiled schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="SchemaCompileException">Parsing or compilation fails, including unsupported constructs or resource-policy refusal.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static RelaxNgSchema Compile(string schema, SchemaCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var context = new SchemaCompileContext(options);
        return Compile(context, context.Parse(schema).Document);
    }
    /// <summary>Reads and compiles a RELAX NG XML-syntax schema with DTD processing prohibited.</summary>
    /// <param name="schema">The readable schema stream, consumed from its current position and left open.</param>
    /// <param name="options">The resource-resolution and compilation options, or null to use defaults.</param>
    /// <returns>The compiled schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is not readable.</exception>
    /// <exception cref="SchemaCompileException">Parsing or compilation fails, including unsupported constructs or resource-policy refusal.</exception>
    /// <exception cref="IOException">Reading the caller's stream fails.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static RelaxNgSchema Compile(Stream schema, SchemaCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!schema.CanRead)
        {
            throw new ArgumentException("Readable stream required.", nameof(schema));
        }

        var context = new SchemaCompileContext(options);
        return Compile(context, context.Parse(schema).Document);
    }
    /// <summary>Checks a parsed document against the compiled RELAX NG patterns without modifying it.</summary>
    /// <param name="document">The instance document.</param>
    /// <param name="options">The validation budgets and cancellation token, or null to use defaults.</param>
    /// <returns>The validity, completion status, and diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The instance changes during validation.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public ValidationResult Validate(XmlDocument document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new RelaxNgEvaluator(program).Validate(program.Schema, document, options ?? new());
    }
    /// <summary>Parses XML instance text and checks it against the compiled RELAX NG patterns.</summary>
    /// <param name="document">The instance XML text; DTD processing is prohibited.</param>
    /// <param name="options">The shared parsing and validation budgets and cancellation token, or null to use defaults.</param>
    /// <returns>The validity, completion status, and diagnostics, including instance parsing failures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public ValidationResult Validate(string document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var operation = new SchemaValidationOperation(options);
        var parsed = RngInput.Parse(document, operation);
        return parsed is null ? operation.Complete() : RelaxNgEvaluator.Evaluate(program, parsed, operation);
    }
    /// <summary>Reads an XML instance and checks it against the compiled RELAX NG patterns.</summary>
    /// <param name="document">The readable instance stream, consumed from its current position and left open; DTD processing is prohibited.</param>
    /// <param name="options">The shared parsing and validation budgets and cancellation token, or null to use defaults.</param>
    /// <returns>The validity, completion status, and diagnostics, including instance parsing failures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is not readable.</exception>
    /// <exception cref="IOException">Reading the caller's stream fails.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public ValidationResult Validate(Stream document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.CanRead)
        {
            throw new ArgumentException("Readable stream required.", nameof(document));
        }

        var operation = new SchemaValidationOperation(options);
        var parsed = RngInput.Parse(document, operation);
        return parsed is null ? operation.Complete() : RelaxNgEvaluator.Evaluate(program, parsed, operation);
    }
}

namespace Xemele.Validation;

public sealed class RelaxNgSchema
{
    private readonly RngProgram program;
    private RelaxNgSchema() { program = null!; }
    private RelaxNgSchema(RngProgram program) => this.program = program;
    private static RelaxNgSchema Compile(SchemaCompileContext context, XmlDocument schema)
    {
        long revision = schema.ContentRevision;
        var graph = new RelaxNgCompiler().Compile(schema, context.Options, context.Operation);
        var program = RngProgram.Finalize(graph, context.Operation);
        context.Options.CancellationToken.ThrowIfCancellationRequested();
        if (revision < 0 || schema.ContentRevision != revision) throw new InvalidOperationException("Schema source changed during compilation.");
        return new(program);
    }
    public static RelaxNgSchema Compile(XmlDocument schema, SchemaCompileOptions? options = null)
    { ArgumentNullException.ThrowIfNull(schema); return Compile(new(options), schema); }
    public static RelaxNgSchema Compile(string schema, SchemaCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema); var context = new SchemaCompileContext(options);
        return Compile(context, context.Parse(schema).Document);
    }
    public static RelaxNgSchema Compile(Stream schema, SchemaCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!schema.CanRead) throw new ArgumentException("Readable stream required.", nameof(schema));
        var context = new SchemaCompileContext(options); return Compile(context, context.Parse(schema).Document);
    }
    public ValidationResult Validate(XmlDocument document, ValidationOptions? options = null)
    { ArgumentNullException.ThrowIfNull(document); return new RelaxNgEvaluator(program).Validate(program.Schema, document, options ?? new()); }
    public ValidationResult Validate(string document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document); var operation = new SchemaValidationOperation(options);
        var parsed = RngInput.Parse(document, operation);
        return parsed is null ? operation.Complete() : RelaxNgEvaluator.Evaluate(program, parsed, operation);
    }
    public ValidationResult Validate(Stream document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.CanRead) throw new ArgumentException("Readable stream required.", nameof(document));
        var operation = new SchemaValidationOperation(options); var parsed = RngInput.Parse(document, operation);
        return parsed is null ? operation.Complete() : RelaxNgEvaluator.Evaluate(program, parsed, operation);
    }
}

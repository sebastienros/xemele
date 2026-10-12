namespace Xemele.Validation;

public sealed class SchematronSchema
{
    private readonly SchematronCompiledModel model;
    private SchematronSchema() { model = null!; }
    private SchematronSchema(SchematronCompiledModel model) => this.model = model;

    public static SchematronSchema Compile(XmlDocument schema, SchematronCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var snapshot = SchemaSnapshots.Compile(options ?? new());
        return new(new SchematronCompiler().Compile(schema, snapshot, new SchemaCompileOperation(snapshot.Schema)));
    }
    public static SchematronSchema Compile(string schema, SchematronCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var snapshot = SchemaSnapshots.Compile(options ?? new());
        var context = new SchemaCompileContext(snapshot.Schema);
        return new(new SchematronCompiler().Compile(context.Parse(schema).Document, snapshot, context.Operation));
    }
    public static SchematronSchema Compile(Stream schema, SchematronCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var snapshot = SchemaSnapshots.Compile(options ?? new());
        var context = new SchemaCompileContext(snapshot.Schema);
        return new(new SchematronCompiler().Compile(context.Parse(schema).Document, snapshot, context.Operation));
    }
    public SchematronResult Validate(XmlDocument document, SchematronValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new SchematronEvaluator().Validate(model, document, options ?? new());
    }
    public SchematronResult Validate(string document, SchematronValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var snapshot = SchemaSnapshots.Validation(options ?? new());
        string phase = SchematronEvaluator.SelectPhase(model, snapshot.Phase);
        var operation = new SchemaValidationOperation(snapshot.Validation);
        var parsed = SchematronInput.Parse(document, operation);
        return parsed is null ? SchematronEvaluator.Stopped(model, phase, operation) : SchematronEvaluator.Evaluate(model, parsed, snapshot, phase, operation);
    }
    public SchematronResult Validate(Stream document, SchematronValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.CanRead) throw new ArgumentException("Readable stream required.", nameof(document));
        var snapshot = SchemaSnapshots.Validation(options ?? new());
        string phase = SchematronEvaluator.SelectPhase(model, snapshot.Phase);
        var operation = new SchemaValidationOperation(snapshot.Validation);
        var parsed = SchematronInput.Parse(document, operation);
        return parsed is null ? SchematronEvaluator.Stopped(model, phase, operation) : SchematronEvaluator.Evaluate(model, parsed, snapshot, phase, operation);
    }
}

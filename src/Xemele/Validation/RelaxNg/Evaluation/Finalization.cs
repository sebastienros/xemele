namespace Xemele.Validation;

// Published arrays are private to the facade; entries are immutable owned values.
internal sealed class RngProgram
{
    internal RngCompiledSchema Schema { get; }
    internal XmlFacetEvaluator Facets { get; }
    private readonly XmlDatatypeDefinition?[] definitions;
    private readonly XmlDatatypeValue?[] values;
    internal XmlDatatypeDefinition? Definition(int id) => definitions[id];
    internal XmlDatatypeValue? Value(int id) => values[id];
    private RngProgram(RngCompiledSchema schema, XmlFacetEvaluator facets, XmlDatatypeDefinition?[] definitions, XmlDatatypeValue?[] values)
    { Schema = schema; Facets = facets; this.definitions = definitions; this.values = values; }

    internal static RngProgram Finalize(RngCompiledSchema compiled, SchemaCompileOperation operation)
    {
        var op = new RngOperation(operation, true);
        try
        {
            op.Charge(compiled.Patterns.Count, default); op.ChargeComponents(4, default);
            var registry = new XmlDatatypeRegistry([], op);
            var facets = new XmlFacetEvaluator(); var adapter = new RngDatatypeFacetAdapter(facets);
            var definitions = new XmlDatatypeDefinition?[compiled.Patterns.Count];
            var values = new XmlDatatypeValue?[compiled.Patterns.Count];
            // Check all checkpoints, including unreachable and simplified occurrences.
            foreach (var pattern in compiled.Patterns)
            {
                op.Charge(1, pattern.Location);
                if (pattern.Datatype is not SchemaName datatype) continue;
                if (!adapter.ValidateParameters(datatype, pattern.Parameters, pattern.Location, registry, op, out var definition))
                    throw operation.Exception();
                definitions[pattern.Id] = definition;
                if (pattern.Kind != RngPatternKind.Value) continue;
                var library = registry.Find(datatype.NamespaceUri)!;
                if (!library.TryParse(datatype, pattern.Value.AsSpan(), pattern.Context, op, out var value, out var error))
                {
                    op.AddDiagnostic(new("RNG.Datatype", XmlDiagnosticSeverity.Error, error?.Message ?? "Invalid schema value.", pattern.Location));
                    throw operation.Exception();
                }
                values[pattern.Id] = value;
            }
            op.Charge(compiled.Patterns.Count, default); op.ChargeComponents(2, default);
            var graph = new RngCompiledSchema(compiled.Start, compiled.Patterns, registry);
            op.Charge(0, default); return new(graph, facets, definitions, values);
        }
        catch (RngOperation.Stopped) { throw operation.Exception(); }
    }
}

// Only refusals/diagnostics from our private engines use this sentinel. External
// parser/resolver calls happen outside this boundary and preserve exception provenance.
internal sealed class RngOperation(ISchemaOperation inner, bool finalizing = false) : ISchemaOperation
{
    internal sealed class Stopped : Exception;
    public XmlResourceLimits Limits => inner.Limits;
    public CancellationToken CancellationToken => inner.CancellationToken;
    public bool IsComplete => inner.IsComplete;
    public long? RemainingWork => inner.RemainingWork;
    public bool Charge(long work, XmlLocation at) { if (!inner.Charge(work, at)) throw new Stopped(); return true; }
    public bool ChargeComponents(int count, XmlLocation at) { if (!inner.ChargeComponents(count, at)) throw new Stopped(); return true; }
    public bool AddDiagnostic(XmlDiagnostic diagnostic)
    {
        if (finalizing && diagnostic.Severity == XmlDiagnosticSeverity.Error) diagnostic = diagnostic with { Code = "RNG.Datatype" };
        if (!inner.AddDiagnostic(diagnostic) || finalizing && diagnostic.Severity is XmlDiagnosticSeverity.Error or XmlDiagnosticSeverity.Fatal)
            throw new Stopped();
        return true;
    }
    public void Refuse(string code, string message, XmlLocation at)
    { inner.Refuse(!finalizing && code == "SCHEMA.Limit" ? "VALIDATION.Limit" : code, message, at); throw new Stopped(); }
}

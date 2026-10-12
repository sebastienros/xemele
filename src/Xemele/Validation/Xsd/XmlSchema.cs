using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;

namespace Xemele.Validation;

// Published state contains only owned immutable component/program snapshots.
public sealed class XmlSchema
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private readonly XsdCompiledSchema schema;
    private readonly IReadOnlyDictionary<SchemaName, XsdContentProgram> programs;
    private readonly XmlFacetEvaluator facets;
    private readonly XsdIdentityProgram identity;
    private XmlSchema() { schema = null!; programs = null!; facets = null!; identity = null!; }
    private XmlSchema(XsdCompiledSchema schema, IReadOnlyDictionary<SchemaName, XsdContentProgram> programs,
        XmlFacetEvaluator facets, XsdIdentityProgram identity)
    { this.schema = schema; this.programs = programs; this.facets = facets; this.identity = identity; }

    public static XmlSchema Compile(XmlDocument schema, SchemaCompileOptions? options = null)
    { ArgumentNullException.ThrowIfNull(schema); return Compile(schema, new SchemaCompileContext(options)); }
    public static XmlSchema Compile(string schema, SchemaCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema); var context = new SchemaCompileContext(options);
        return Compile(context.Parse(schema).Document, context);
    }
    public static XmlSchema Compile(Stream schema, SchemaCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!schema.CanRead) throw new ArgumentException("Readable stream required.", nameof(schema));
        var context = new SchemaCompileContext(options); return Compile(context.Parse(schema).Document, context);
    }
    private static XmlSchema Compile(XmlDocument source, SchemaCompileContext context)
    {
        long revision = source.ContentRevision;
        var compiled = new XmlSchemaCompiler().Compile(source, context.Options, context.Operation);
        // Compiler builtins are declaration carriers; replace them with executable metadata.
        var definitions = new List<XmlDatatypeDefinition>();
        foreach (var definition in compiled.SimpleTypes)
        {
            Tick(context.Operation, definition.Location);
            if (definition.Name.NamespaceUri != Xs) definitions.Add(definition);
        }
        var registry = new XmlDatatypeRegistry(definitions, context.Operation);
        compiled = compiled with { Datatypes = registry };
        var facets = new XmlFacetEvaluator();
        foreach (var definition in compiled.SimpleTypes)
        {
            var executable = registry.FindType(definition.Name)!;
            if (!facets.ValidateDefinition(executable, registry, context.Operation)) throw SchemaSnapshots.Failure(context.Operation);
        }
        var programs = new Dictionary<SchemaName, XsdContentProgram>();
        foreach (var type in compiled.Types)
        {
            Tick(context.Operation, type.Location);
            programs.Add(type.Name, new XsdContentCompiler().Compile(compiled, type, context.Operation));
        }
        FinalizeDeclarations(compiled, programs, facets, context.Operation);
        var identity = new XsdIdentityCompiler().Compile(compiled, context.Operation);
        Tick(context.Operation, default, 0);
        if (revision < 0 || source.ContentRevision != revision) throw new InvalidOperationException("Schema source changed during compilation.");
        return new(compiled, XsdSnapshots.Map(programs), facets, identity);
    }
    private static void Tick(ISchemaOperation op, XmlLocation at, long work = 1)
    { if (!op.Charge(work, at)) throw SchemaSnapshots.Failure(op); }
    private static void Reject(ISchemaOperation op, XmlLocation at, string message)
    {
        op.AddDiagnostic(new("XSD.SchemaConstraint", XmlDiagnosticSeverity.Error, message, at));
        throw SchemaSnapshots.Failure(op);
    }
    private static bool Derived(XsdCompiledSchema schema, SchemaName name, string builtin, ISchemaOperation op)
    {
        while (true)
        {
            Tick(op, default);
            if (name == new SchemaName(Xs, builtin)) return true;
            if (schema.Datatypes.FindType(name)?.BaseType is not { } parent) return false;
            name = parent;
        }
    }
    private static void NotationType(XsdCompiledSchema schema, SchemaName name, ISchemaOperation op, XmlLocation at)
    {
        if (!Derived(schema, name, "NOTATION", op)) return;
        bool enumeration = false;
        for (var type = schema.Datatypes.FindType(name); type is not null; type = type.BaseType is { } parent ? schema.Datatypes.FindType(parent) : null)
        {
            Tick(op, at);
            foreach (var facet in type.Facets) { Tick(op, facet.Location); enumeration |= facet.Kind == XmlFacetKind.Enumeration; }
        }
        if (!enumeration) Reject(op, at, "NOTATION requires an effective enumeration restriction.");
    }
    private static void NotationValue(XsdCompiledSchema schema, XmlDatatypeValue value, ReadOnlySpan<char> lexical,
        SchemaNamespaceContext context, ISchemaOperation op, XmlLocation at)
    {
        Tick(op, at);
        while (value.SelectedMember is { } member) { Tick(op, at); value = member; }
        if (value.Items is { } items)
        {
            var normalized = schema.Datatypes.Find(Xs)!.Normalize(lexical, XmlWhitespaceRule.Collapse, op).AsSpan();
            foreach (var item in items)
            {
                Tick(op, at, normalized.Length);
                int end = normalized.IndexOf(' '); if (end < 0) end = normalized.Length;
                NotationValue(schema, item, normalized[..end], context, op, at);
                normalized = end == normalized.Length ? [] : normalized[(end + 1)..];
            }
            return;
        }
        if (!Derived(schema, value.TypeName, "NOTATION", op)) return;
        var text = schema.Datatypes.Find(Xs)!.Normalize(lexical, XmlWhitespaceRule.Collapse, op);
        Tick(op, at, text.Length);
        int colon = text.IndexOf(':'); string local = colon < 0 ? text : text[(colon + 1)..];
        string? uri = colon < 0 ? context.DefaultNamespace : context.Prefixes.GetValueOrDefault(text[..colon]);
        if (uri is null || !schema.Notations.ContainsKey(new(uri, local))) Reject(op, at, "NOTATION value must name a declared schema notation.");
    }
    private static DatatypeConstraintRepresentation? Constraint(XsdCompiledSchema schema, XmlFacetEvaluator facets,
        SchemaName type, string? lexical, SchemaNamespaceContext context, XmlLocation at, ISchemaOperation op)
    {
        if (lexical is null) return null;
        Tick(op, at);
        if (Derived(schema, type, "ID", op)) Reject(op, at, "ID declarations cannot have default or fixed value constraints.");
        if (!DatatypeConstraintRepresentation.TryCreate(schema.Datatypes.FindType(type)!, lexical, context,
            schema.Datatypes, facets, op, out var prepared, out var error))
        {
            if (op.IsComplete) op.AddDiagnostic(new(error?.Code ?? "XSD.SchemaConstraint", XmlDiagnosticSeverity.Error,
                error?.Message ?? "Invalid value constraint.", at));
            throw SchemaSnapshots.Failure(op);
        }
        NotationValue(schema, prepared!.DeclaredValue, lexical, context, op, at);
        return prepared;
    }
    private static void FinalizeDeclarations(XsdCompiledSchema schema, IReadOnlyDictionary<SchemaName, XsdContentProgram> programs,
        XmlFacetEvaluator facets, ISchemaOperation op)
    {
        var types = new Dictionary<SchemaName, XsdTypeDefinition>();
        foreach (var type in schema.Types) { Tick(op, type.Location); types.Add(type.Name, type); }
        // Include unused, anonymous and displaced redefine definitions, not just reachable instance types.
        foreach (var type in schema.SimpleTypes)
        {
            Tick(op, type.Location);
            if (type.Name.NamespaceUri != Xs)
            {
                NotationType(schema, type.Name, op, type.Location);
                if (type.ItemType is { } item) NotationType(schema, item, op, type.Location);
                foreach (var member in type.MemberTypes) { Tick(op, type.Location); NotationType(schema, member, op, type.Location); }
            }
            foreach (var facet in type.Facets)
            {
                Tick(op, facet.Location);
                if (facet.Kind != XmlFacetKind.Enumeration) continue;
                // Enumeration lexical forms are parsed against the base at their authored context.
                var basis = schema.Datatypes.FindType(type.BaseType ?? type.Name)!;
                // Direct list/union construction has anySimpleType as a nominal
                // base. Parse with its member graph so selected NOTATION values
                // are retained; omit only this level's authored facets.
                if (type.Variety != XmlDatatypeVariety.Atomic && basis.Name == new SchemaName(Xs, "anySimpleType"))
                    basis = type with { Facets = [] };
                if (!facets.TryValidate(basis, facet.LexicalValue, facet.Context, schema.Datatypes, op, out var value, out var error))
                {
                    if (op.IsComplete) op.AddDiagnostic(new(error?.Code ?? "XSD.SchemaConstraint", XmlDiagnosticSeverity.Error,
                        error?.Message ?? "Invalid enumeration value.", facet.Location));
                    throw SchemaSnapshots.Failure(op);
                }
                NotationValue(schema, value!, facet.LexicalValue, facet.Context, op, facet.Location);
            }
        }
        foreach (var attribute in schema.Attributes)
        {
            Tick(op, attribute.Location); NotationType(schema, attribute.Type, op, attribute.Location);
            Constraint(schema, facets, attribute.Type, attribute.DefaultValue ?? attribute.FixedValue, attribute.Context, attribute.Location, op);
        }
        foreach (var type in schema.Types)
        {
            int ids = 0;
            if (type.Name.NamespaceUri != Xs && type.SimpleType is { } simple) NotationType(schema, simple, op, type.Location);
            foreach (var binding in type.Attributes)
            {
                Tick(op, binding.Location); var attribute = schema.Attributes[binding.DeclarationId];
                if (binding.Use != XsdAttributeUse.Prohibited && Derived(schema, attribute.Type, "ID", op) && ++ids > 1)
                    Reject(op, binding.Location, "A complex type cannot have more than one ID attribute use.");
                var local = Constraint(schema, facets, attribute.Type, binding.DefaultValue ?? binding.FixedValue, binding.Context, binding.Location, op);
                if (local is not null && binding.FixedValue is not null && attribute.FixedValue is not null)
                {
                    var declared = Constraint(schema, facets, attribute.Type, attribute.FixedValue, attribute.Context, attribute.Location, op)!;
                    if (!schema.Datatypes.Find(Xs)!.ValueEquals(local.DeclaredValue, declared.DeclaredValue, op))
                        Reject(op, binding.Location, "Attribute binding changes the referenced typed fixed value.");
                }
            }
        }
        foreach (var element in schema.Elements)
        {
            Tick(op, element.Location); var type = types[element.Type];
            if (type.SimpleType is { } simple) NotationType(schema, simple, op, element.Location);
            string? lexical = element.DefaultValue ?? element.FixedValue; if (lexical is null) continue;
            if (type.ContentKind is XsdContentKind.Empty or XsdContentKind.ElementOnly ||
                type.ContentKind == XsdContentKind.Mixed && !new XsdContentMatcher().CreateCursor(programs[type.Name]).Complete(op))
                Reject(op, element.Location, "Element value constraint requires simple or emptiable mixed content.");
            Constraint(schema, facets, type.SimpleType ?? new(Xs, "anySimpleType"), lexical, element.Context, element.Location, op);
        }
    }
    public ValidationResult Validate(XmlDocument document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new XmlSchemaValidator().Validate(schema, document, options ?? new(), programs, new XsdContentMatcher(),
            facets, new XsdIdentityFactory(), identity);
    }
    public ValidationResult Validate(string document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document); var snapshot = Snapshot(options);
        var operation = new SchemaValidationOperation(snapshot); var parsed = XsdFacadeInput.Parse(document, operation);
        return Evaluate(parsed, snapshot, operation);
    }
    public ValidationResult Validate(Stream document, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.CanRead) throw new ArgumentException("Readable stream required.", nameof(document));
        var snapshot = Snapshot(options); var operation = new SchemaValidationOperation(snapshot);
        return Evaluate(XsdFacadeInput.Parse(document, operation), snapshot, operation);
    }
    private static ValidationOptions Snapshot(ValidationOptions? options)
    {
        options ??= new(); ArgumentNullException.ThrowIfNull(options.Limits);
        return options with { Limits = options.Limits with { } };
    }
    private ValidationResult Evaluate(XmlDocument? parsed, ValidationOptions snapshot, SchemaValidationOperation operation)
    {
        if (parsed is null) return operation.Complete();
        // The verified DOM evaluator has its own ledger. Transfer the exact remaining
        // allowance so parsing and assessment cannot each consume a full work cap.
        if (operation.RemainingWork == 0)
        { operation.Refuse("VALIDATION.Limit", "Instance assessment allowance exhausted.", default); return operation.Complete(); }
        return Validate(parsed, snapshot with { MaxOperations = operation.RemainingWork });
    }
}

// Structured parser output distinguishes owned syntax refusal from caller I/O exceptions.
internal static class XsdFacadeInput
{
    private static OperationBudget? Budget(SchemaValidationOperation operation)
    {
        if (!operation.Charge(0, default)) return null;
        long? characters = operation.Limits.DocumentCharacters;
        if (operation.RemainingWork is long work)
        {
            if (work == 0) { operation.Refuse("VALIDATION.Limit", "Instance parsing allowance exhausted.", default); return null; }
            characters = characters is long cap ? Math.Min(cap, work) : work;
        }
        return new(new() { Limits = operation.Limits with { DocumentCharacters = characters }, DtdProcessing = XmlDtdProcessing.Prohibit }, operation.CancellationToken);
    }
    internal static XmlDocument? Parse(string input, SchemaValidationOperation operation)
    {
        var budget = Budget(operation); if (budget is null) return null;
        long charged = 0; Exception? primary = null;
        try
        {
            using var core = new XmlParserCore(budget.Options, operationBudget: budget);
            var sink = new DomSink(budget.Options, cancellationToken: operation.CancellationToken);
            var result = core.Append(input.AsSpan(), [], true, sink, operation.CancellationToken);
            if (!Debit(budget, operation, ref charged)) return null;
            return Finish(sink.Document, sink.Diagnostic ?? result.Diagnostic, operation);
        }
        catch (Exception error) { primary = error; throw; }
        finally { try { Debit(budget, operation, ref charged); } catch when (primary is not null) { } }
    }
    internal static XmlDocument? Parse(Stream input, SchemaValidationOperation operation)
    {
        var budget = Budget(operation); if (budget is null) return null;
        long charged = 0; Exception? primary = null;
        try
        {
            using var parser = new ByteDomAdapter(budget.Options, operation.CancellationToken, budget);
            // One bounded input buffer belongs to this operation. Caller streams stay open.
            byte[] buffer = new byte[4096];
            while (parser.Diagnostic is null)
            {
                operation.CancellationToken.ThrowIfCancellationRequested();
                int count = input.Read(buffer.AsSpan());
                if ((uint)count > buffer.Length) throw new IOException("Invalid stream read count.");
                if (count == 0) parser.Complete(); else parser.Append(buffer.AsSpan(0, count));
                if (!Debit(budget, operation, ref charged)) return null;
                if (count == 0) break;
            }
            return Finish(parser.Document, parser.Diagnostic, operation);
        }
        catch (Exception error) { primary = error; throw; }
        finally { try { Debit(budget, operation, ref charged); } catch when (primary is not null) { } }
    }
    private static bool Debit(OperationBudget budget, SchemaValidationOperation operation, ref long charged)
    {
        long actual = budget.Count(Quota.DocumentCharacters), used = actual - charged; charged = actual;
        return operation.Charge(used, default);
    }
    private static XmlDocument? Finish(XmlDocument document, XmlDiagnostic? diagnostic, SchemaValidationOperation operation)
    {
        operation.CancellationToken.ThrowIfCancellationRequested();
        if (diagnostic is null) return document;
        operation.Refuse(diagnostic.Code.Contains("Limit", StringComparison.OrdinalIgnoreCase) ? "VALIDATION.Limit" : "SCHEMA.Syntax", diagnostic.Message, diagnostic.Location);
        return null;
    }
}

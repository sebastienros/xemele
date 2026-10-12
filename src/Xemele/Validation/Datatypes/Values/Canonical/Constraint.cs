namespace Xemele.Validation;

// Only validated declaration text can create this pair. The assessment lexical
// is not required to pass the DECLARED patterns again: it is for the actual type.
internal sealed class DatatypeConstraintRepresentation
{
    internal string Lexical { get; }
    internal XmlDatatypeValue DeclaredValue { get; }
    internal SchemaNamespaceContext Context { get; }
    private DatatypeConstraintRepresentation(string lexical, XmlDatatypeValue value, SchemaNamespaceContext context)
    { Lexical = lexical; DeclaredValue = value; Context = context; }

    internal static bool TryCreate(XmlDatatypeDefinition declaredType, ReadOnlySpan<char> authoredLexical,
        SchemaNamespaceContext declarationContext, IXmlDatatypeRegistry registry, IXmlFacetEvaluator facets,
        ISchemaOperation operation, out DatatypeConstraintRepresentation? representation, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(declaredType); ArgumentNullException.ThrowIfNull(declarationContext);
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(facets); ArgumentNullException.ThrowIfNull(operation);
        representation = null; error = null;
        try
        {
            // Sentinel catches ONLY returned operation refusals, never caller exceptions.
            if (!operation.Charge(0, declaredType.Location) || !operation.ChargeComponents(1, declaredType.Location))
            { error = DatatypeErrors.Stopped; return false; }
            var op = new ConstraintOperation(operation);
            if (!facets.TryValidate(declaredType, authoredLexical, declarationContext, registry, op, out var value, out error)) return false;
            var measure = new ConstraintWriter(default, false, op);
            Format(value!, authoredLexical, ref measure);
            int length = measure.Length;
            // String storage blocks, result object, and bounded formatting state.
            op.ChargeComponents(checked(2 + length / 16 + (length % 16 == 0 ? 0 : 1)), default);
            string lexical = string.Create(length, new FormatState(value!, authoredLexical, op), static (destination, state) =>
            {
                var writer = new ConstraintWriter(destination, true, state.Operation);
                Format(state.Value, state.Authored, ref writer);
                if (writer.Length != destination.Length) throw new InvalidOperationException("Constraint length changed during formatting.");
            });
            op.Charge(0, default);
            representation = new(lexical, value!, declarationContext); return true;
        }
        catch (ConstraintOperation.Stopped) { error = DatatypeErrors.Stopped; return false; }
    }
    private readonly ref struct FormatState(XmlDatatypeValue value, ReadOnlySpan<char> authored, ISchemaOperation operation)
    { internal readonly XmlDatatypeValue Value = value; internal readonly ReadOnlySpan<char> Authored = authored; internal readonly ISchemaOperation Operation = operation; }

    private static void Format(XmlDatatypeValue value, ReadOnlySpan<char> authored, ref ConstraintWriter writer)
    {
        var unwrapped = DatatypeValues.Unwrap(value, writer.Operation);
        if (unwrapped.Variety == XmlDatatypeVariety.List)
        {
            var items = unwrapped.Items!; int cursor = 0;
            for (int item = 0; item < items.Count; item++)
            {
                writer.Operation.Charge(1, default);
                while (cursor < authored.Length)
                { writer.Operation.Charge(1, default); if (!DatatypeNormalization.IsWhitespace(authored[cursor])) break; cursor++; }
                int start = cursor;
                while (cursor < authored.Length)
                { writer.Operation.Charge(1, default); if (DatatypeNormalization.IsWhitespace(authored[cursor])) break; cursor++; }
                if (item != 0) writer.Append(' ');
                // Lists contain atomic members or unions selecting atomic members.
                Format(items[item], authored[start..cursor], ref writer);
            }
            return;
        }
        var atomic = (DatatypeAtomicValue)unwrapped;
        switch (atomic.Provider)
        {
            case NumericDatatypeProvider: NumericDatatypeProvider.WriteConstraint(atomic, ref writer); break;
            case TextBinaryDatatypeProvider: TextBinaryDatatypeProvider.WriteConstraint(atomic, authored, ref writer); break;
            case TemporalDatatypeProvider: TemporalDatatypeProvider.WriteConstraint(atomic, authored, ref writer); break;
            default: throw new ArgumentException("Unsupported datatype provider.", nameof(value));
        }
    }
}

internal sealed class ConstraintOperation(ISchemaOperation inner) : ISchemaOperation
{
    internal sealed class Stopped : Exception;
    public XmlResourceLimits Limits => inner.Limits;
    public CancellationToken CancellationToken => inner.CancellationToken;
    public bool IsComplete => inner.IsComplete;
    public long? RemainingWork => inner.RemainingWork;
    public bool Charge(long work, XmlLocation location) { if (!inner.Charge(work, location)) throw new Stopped(); return true; }
    public bool ChargeComponents(int count, XmlLocation location) { if (!inner.ChargeComponents(count, location)) throw new Stopped(); return true; }
    public bool AddDiagnostic(XmlDiagnostic diagnostic) { if (!inner.AddDiagnostic(diagnostic)) throw new Stopped(); return true; }
    public void Refuse(string code, string message, XmlLocation location) { inner.Refuse(code, message, location); throw new Stopped(); }
}

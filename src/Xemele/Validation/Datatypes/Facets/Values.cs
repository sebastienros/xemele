using System.Buffers;
using System.Text;
namespace Xemele.Validation;

internal sealed partial class XmlFacetEvaluator
{
    private bool Parse(FacetPlan plan, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        ISchemaOperation op, out XmlDatatypeValue? value, out XmlDatatypeError? error) =>
        Parse(plan, lexical, context, op, out value, out error, out _);
    private bool Parse(FacetPlan plan, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        ISchemaOperation op, out XmlDatatypeValue? value, out XmlDatatypeError? error, out XmlWhitespaceRule effective)
    {
        op.Charge(1, plan.Definition.Location); value = null; error = DatatypeErrors.InvalidLexical; effective = plan.Whitespace;
        if (plan.Variety == XmlDatatypeVariety.Union)
        {
            for (int i = 0; i < plan.Members.Length; i++)
            {
                op.Charge(1, plan.Definition.Location);
                if (!Parse(plan.Members[i], lexical, context, op, out var selected, out error, out var memberRule)) continue;
                if (!NormalizedCheck(plan, lexical, memberRule, selected!, op, out error)) continue;
                value = plan.Library.CreateUnion(plan.Definition.Name, selected!, op);
                effective = memberRule; error = null; return op.Charge(0, plan.Definition.Location);
            }
            error ??= FacetMismatch; return false;
        }
        if (!DatatypeNormalization.TryMeasure(lexical, effective, op, out int length, out _, out error)) return false;
        char[]? rented = null;
        Span<char> normalized = length <= 256 ? stackalloc char[length] : default;
        try
        {
            if (length > 256) { op.Charge(1, default); op.ChargeComponents(1, default); rented = ArrayPool<char>.Shared.Rent(length); normalized = rented.AsSpan(0, length); }
            if (!DatatypeNormalization.TryNormalize(lexical, effective, normalized, op, out int n, out error)) return false;
            if (!ParseRaw(plan, normalized[..n], context, op, out var parsed, out error)) return false;
            if (!Check(plan, normalized[..n], parsed!, op)) { error = FacetMismatch; return false; }
            value = parsed; error = null; return op.Charge(0, plan.Definition.Location);
        }
        finally { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }
    private bool NormalizedCheck(FacetPlan plan, ReadOnlySpan<char> lexical, XmlWhitespaceRule rule,
        XmlDatatypeValue value, ISchemaOperation op, out XmlDatatypeError? error)
    {
        if (!DatatypeNormalization.TryMeasure(lexical, rule, op, out int length, out _, out error)) return false;
        char[]? rented = null; Span<char> scratch = length <= 256 ? stackalloc char[length] : default;
        try
        {
            if (length > 256) { op.Charge(1, default); op.ChargeComponents(1, default); rented = ArrayPool<char>.Shared.Rent(length); scratch = rented.AsSpan(0, length); }
            if (!DatatypeNormalization.TryNormalize(lexical, rule, scratch, op, out int n, out error)) return false;
            if (!Check(plan, scratch[..n], value, op)) { error = FacetMismatch; return false; }
            error = null; return true;
        }
        finally { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }
    private bool ParseRaw(FacetPlan plan, ReadOnlySpan<char> normalized, SchemaNamespaceContext context,
        ISchemaOperation op, out XmlDatatypeValue? value, out XmlDatatypeError? error)
    {
        op.Charge(1, plan.Definition.Location); value = null; error = null;
        if (plan.Builtin) return plan.Library.TryParse(plan.Definition.Name, normalized, context, op, out value, out error);
        if (plan.Variety == XmlDatatypeVariety.Union) return Parse(plan, normalized, context, op, out value, out error);
        if (plan.Base is not null && (plan.Variety == XmlDatatypeVariety.Atomic || plan.Base.Definition.Name != DatatypeTypes.Name("anySimpleType")))
        {
            if (!ParseRaw(plan.Base, normalized, context, op, out var parent, out error)) return false;
            value = plan.Library.Retype(plan.Definition.Name, parent!, op); return true;
        }
        if (plan.Variety != XmlDatatypeVariety.List) { error = InvalidDefinition; return false; }
        int count = normalized.IsEmpty ? 0 : 1;
        for (int i = 0; i < normalized.Length;)
        {
            op.Charge(1, plan.Definition.Location);
            if (Rune.DecodeFromUtf16(normalized[i..], out var scalar, out int consumed) != OperationStatus.Done)
            { error = DatatypeErrors.InvalidUnicode; return false; }
            if (scalar.Value == ' ') count = checked(count + 1);
            i += consumed;
        }
        op.ChargeComponents(count + 1, plan.Definition.Location);
        var items = new XmlDatatypeValue[count]; int start = 0, index = 0;
        for (int i = 0; i <= normalized.Length && count != 0;)
        {
            bool end = i == normalized.Length;
            if (!end)
            {
                op.Charge(1, plan.Definition.Location);
                Rune.DecodeFromUtf16(normalized[i..], out var scalar, out int n);
                if (scalar.Value != ' ') { i += n; continue; }
            }
            if (!Parse(plan.Item!, normalized[start..i], context, op, out var item, out error)) return false;
            op.Charge(1, plan.Definition.Location); items[index++] = item!;
            if (end) break;
            start = ++i;
        }
        value = plan.Library.CreateList(plan.Definition.Name, items, op); return true;
    }
    // Borrowed lexical matching and typed comparisons use only immutable plans.
    // This primitive is separate from normalization, parsing and owned results.
    private bool Check(FacetPlan plan, ReadOnlySpan<char> normalized, XmlDatatypeValue value, ISchemaOperation op)
    {
        for (FacetPlan? level = plan; level is not null; level = level.Base)
        {
            if (!op.Charge(1, level.Definition.Location)) return false;
            if (level.Patterns.Length != 0)
            {
                bool matched = level.ConjunctivePatterns;
                for (int i = 0; i < level.Patterns.Length; i++)
                {
                    if (!op.Charge(1, level.Definition.Location)) return false;
                    bool hit = matcher.IsMatch(level.Patterns[i], normalized, op);
                    if (!op.IsComplete) return false;
                    if (level.ConjunctivePatterns) { if (!hit) return false; }
                    else if (hit) { matched = true; break; }
                }
                if (!matched) return false;
            }
            if (level.Enumeration.Length != 0)
            {
                bool matched = false;
                for (int i = 0; i < level.Enumeration.Length; i++)
                { if (!op.Charge(1, level.Definition.Location)) return false; if (level.Library.ValueEquals(value, level.Enumeration[i], op)) { matched = true; break; } }
                if (!matched) return false;
            }
            for (int i = 0; i < level.Facets.Length; i++)
            {
                if (!op.Charge(1, level.Facets[i].Source.Location)) return false;
                if (!CheckScalar(level, level.Facets[i], value, op)) return false;
            }
        }
        return op.Charge(0, plan.Definition.Location);
    }
    private static bool CheckScalar(FacetPlan plan, PreparedFacet facet, XmlDatatypeValue value, ISchemaOperation op)
    {
        var kind = facet.Source.Kind;
        if (FacetRules.IsBound(kind))
        {
            XmlValueOrder order = plan.Library.Compare(value, facet.Value!, op);
            return kind switch
            {
                XmlFacetKind.MinInclusive => order is XmlValueOrder.Greater or XmlValueOrder.Equal,
                XmlFacetKind.MinExclusive => order == XmlValueOrder.Greater,
                XmlFacetKind.MaxInclusive => order is XmlValueOrder.Less or XmlValueOrder.Equal,
                _ => order == XmlValueOrder.Less
            };
        }
        if (kind == XmlFacetKind.WhiteSpace) return true;
        if (kind is XmlFacetKind.TotalDigits or XmlFacetKind.FractionDigits)
        {
            int? measure = kind == XmlFacetKind.TotalDigits ? plan.Library.TotalDigits(value, op) : plan.Library.FractionDigits(value, op);
            return measure is not null && measure <= facet.Number;
        }
        if (plan.Primitive is DatatypePrimitive.QName or DatatypePrimitive.Notation) return true;
        long? length = plan.Library.ValueLength(value, op);
        if (length is null) return false;
        return kind switch { XmlFacetKind.Length => length == facet.Number, XmlFacetKind.MinLength => length >= facet.Number,
            XmlFacetKind.MaxLength => length <= facet.Number, _ => false };
    }
}

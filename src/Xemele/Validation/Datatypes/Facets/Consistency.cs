namespace Xemele.Validation;

internal sealed partial class XmlFacetEvaluator
{
    private static void SelectEffectiveScalars(FacetPlan plan, ISchemaOperation op)
    {
        int own = 0, inherited = 0;
        foreach (var facet in plan.Facets) { op.Charge(1, facet.Source.Location); own |= 1 << (int)facet.Source.Kind; }
        int blocked = own;
        if ((own & ((1 << (int)XmlFacetKind.MinInclusive) | (1 << (int)XmlFacetKind.MinExclusive))) != 0)
            blocked |= (1 << (int)XmlFacetKind.MinInclusive) | (1 << (int)XmlFacetKind.MinExclusive);
        if ((own & ((1 << (int)XmlFacetKind.MaxInclusive) | (1 << (int)XmlFacetKind.MaxExclusive))) != 0)
            blocked |= (1 << (int)XmlFacetKind.MaxInclusive) | (1 << (int)XmlFacetKind.MaxExclusive);
        var parent = plan.Base?.EffectiveScalars ?? [];
        foreach (var facet in parent) { op.Charge(1, facet.Source.Location); inherited |= (1 << (int)facet.Source.Kind) & ~blocked; }
        int count = System.Numerics.BitOperations.PopCount((uint)(own | inherited));
        op.ChargeComponents(count + 1, plan.Definition.Location);
        var result = new PreparedFacet[count]; int index = 0;
        foreach (var facet in plan.Facets) { op.Charge(1, facet.Source.Location); result[index++] = facet; }
        foreach (var facet in parent)
        { op.Charge(1, facet.Source.Location); if ((inherited & (1 << (int)facet.Source.Kind)) != 0) result[index++] = facet; }
        plan.EffectiveScalars = result;
    }
    private static bool Consistent(FacetPlan plan, ISchemaOperation op)
    {
        for (int i = 0; i < plan.Facets.Length; i++)
        {
            var current = plan.Facets[i]; var kind = current.Source.Kind;
            for (int j = i + 1; j < plan.Facets.Length; j++)
            {
                var other = plan.Facets[j]; op.Charge(1, current.Source.Location);
                if (FacetRules.IsBound(kind) && FacetRules.IsBound(other.Source.Kind) && FacetRules.IsMinimum(kind) == FacetRules.IsMinimum(other.Source.Kind))
                    return FacetRules.Reject(op, current.Source.Location, "Inclusive and exclusive bounds cannot coexist at one restriction level.");
                if (!Compatible(plan, current, other, op)) return FacetRules.Reject(op, current.Source.Location, "Conflicting facets.");
            }
            for (var ancestor = plan.Base; ancestor is not null; ancestor = ancestor.Base)
            {
                op.Charge(1, current.Source.Location);
                for (int j = 0; j < ancestor.Facets.Length; j++)
                {
                    var previous = ancestor.Facets[j]; op.Charge(1, current.Source.Location);
                    if (!Restriction(plan, current, previous, op))
                        return FacetRules.Reject(op, current.Source.Location, "Facet weakens, conflicts with, or changes a fixed inherited restriction.");
                }
            }
            if (FacetRules.IsBound(kind) && plan.Base is not null && !plan.Builtin)
            {
                // Bound values must inhabit the base value space. Equality with
                // a repeated exclusive endpoint is the explicit XSD exception.
                for (var ancestor = plan.Base; ancestor is not null; ancestor = ancestor.Base)
                {
                    op.Charge(1, current.Source.Location);
                    for (int j = 0; j < ancestor.EffectiveScalars.Length; j++)
                    {
                        var inherited = ancestor.EffectiveScalars[j]; op.Charge(1, current.Source.Location);
                        bool endpoint = inherited.Source.Kind == kind && FacetRules.IsExclusive(kind) && plan.Library.ValueEquals(current.Value!, inherited.Value!, op);
                        if (!endpoint && !CheckScalar(ancestor, inherited, current.Value!, op))
                            return FacetRules.Reject(op, current.Source.Location, "Bound value is outside the inherited value space.");
                    }
                    // Pattern is lexical and not a value-space test; bounds do
                    // not acquire a new lexical pattern restriction themselves.
                    if (ancestor.Enumeration.Length != 0)
                    {
                        bool found = false;
                        for (int j = 0; j < ancestor.Enumeration.Length; j++)
                        { op.Charge(1, current.Source.Location); if (plan.Library.ValueEquals(current.Value!, ancestor.Enumeration[j], op)) { found = true; break; } }
                        if (!found) return FacetRules.Reject(op, current.Source.Location, "Bound value is outside inherited enumeration.");
                    }
                }
            }
        }
        for (int i = 0; i < plan.EffectiveScalars.Length; i++)
        {
            var a = plan.EffectiveScalars[i]; op.Charge(1, a.Source.Location);
            for (int j = i + 1; j < plan.EffectiveScalars.Length; j++)
            { op.Charge(1, a.Source.Location); if (!Compatible(plan, a, plan.EffectiveScalars[j], op)) return FacetRules.Reject(op, a.Source.Location, "Conflicting effective facets."); }
            if (a.Source.Kind != XmlFacetKind.Length) continue;
            foreach (var facet in plan.EffectiveScalars)
            {
                op.Charge(1, facet.Source.Location);
                if (facet.Source.Kind is XmlFacetKind.MinLength or XmlFacetKind.MaxLength && !HasLengthOrigin(plan, facet, op))
                    return FacetRules.Reject(op, facet.Source.Location, "Length bounds require an unchanged value in an ancestor without length.");
            }
        }
        return op.Charge(0, plan.Definition.Location);
    }
    private static bool HasLengthOrigin(FacetPlan plan, PreparedFacet bound, ISchemaOperation op)
    {
        for (var ancestor = plan.Base; ancestor is not null; ancestor = ancestor.Base)
        {
            op.Charge(1, bound.Source.Location); bool hasLength = false, match = false;
            foreach (var facet in ancestor.EffectiveScalars)
            {
                op.Charge(1, bound.Source.Location);
                hasLength |= facet.Source.Kind == XmlFacetKind.Length;
                if (facet.Source.Kind == bound.Source.Kind) match |= facet.Number.Compare(bound.Number, op, bound.Source.Location) is 0;
            }
            if (!hasLength && match) return true;
        }
        return false;
    }
    private static bool Compatible(FacetPlan plan, PreparedFacet a, PreparedFacet b, ISchemaOperation op)
    {
        var x = a.Source.Kind; var y = b.Source.Kind;
        if (FacetRules.IsBound(x) && FacetRules.IsBound(y) && FacetRules.IsMinimum(x) != FacetRules.IsMinimum(y))
        {
            var min = FacetRules.IsMinimum(x) ? a : b; var max = FacetRules.IsMinimum(x) ? b : a;
            var order = plan.Library.Compare(min.Value!, max.Value!, op);
            return order == XmlValueOrder.Less || order == XmlValueOrder.Equal && FacetRules.IsExclusive(min.Source.Kind) == FacetRules.IsExclusive(max.Source.Kind);
        }
        if (x == XmlFacetKind.Length && y == XmlFacetKind.Length) return a.Number.Compare(b.Number, op, a.Source.Location) is 0;
        if (x == XmlFacetKind.Length && y == XmlFacetKind.MinLength) return a.Number.Compare(b.Number, op, a.Source.Location) is >= 0;
        if (x == XmlFacetKind.Length && y == XmlFacetKind.MaxLength) return a.Number.Compare(b.Number, op, a.Source.Location) is <= 0;
        if (y == XmlFacetKind.Length && x == XmlFacetKind.MinLength) return b.Number.Compare(a.Number, op, a.Source.Location) is >= 0;
        if (y == XmlFacetKind.Length && x == XmlFacetKind.MaxLength) return b.Number.Compare(a.Number, op, a.Source.Location) is <= 0;
        if (x == XmlFacetKind.MinLength && y == XmlFacetKind.MaxLength) return a.Number.Compare(b.Number, op, a.Source.Location) is <= 0;
        if (y == XmlFacetKind.MinLength && x == XmlFacetKind.MaxLength) return b.Number.Compare(a.Number, op, a.Source.Location) is <= 0;
        if (x == XmlFacetKind.FractionDigits && y == XmlFacetKind.TotalDigits) return a.Number.Compare(b.Number, op, a.Source.Location) is <= 0;
        if (y == XmlFacetKind.FractionDigits && x == XmlFacetKind.TotalDigits) return b.Number.Compare(a.Number, op, a.Source.Location) is <= 0;
        return true;
    }
    private static bool Restriction(FacetPlan plan, PreparedFacet current, PreparedFacet previous, ISchemaOperation op)
    {
        var x = current.Source.Kind; var y = previous.Source.Kind;
        if (x == y && previous.Source.Fixed)
        {
            bool equal = FacetRules.IsBound(x) ? plan.Library.ValueEquals(current.Value!, previous.Value!, op) : current.Number.Compare(previous.Number, op, current.Source.Location) is 0;
            if (!equal) return false;
        }
        if (x == y)
        {
            if (x == XmlFacetKind.Length) return current.Number.Compare(previous.Number, op, current.Source.Location) is 0;
            if (x == XmlFacetKind.MinLength) return current.Number.Compare(previous.Number, op, current.Source.Location) is >= 0;
            if (x is XmlFacetKind.MaxLength or XmlFacetKind.TotalDigits or XmlFacetKind.FractionDigits) return current.Number.Compare(previous.Number, op, current.Source.Location) is <= 0;
        }
        if (FacetRules.IsBound(x) && FacetRules.IsBound(y) && FacetRules.IsMinimum(x) == FacetRules.IsMinimum(y))
        {
            var order = plan.Library.Compare(current.Value!, previous.Value!, op);
            return order == (FacetRules.IsMinimum(x) ? XmlValueOrder.Greater : XmlValueOrder.Less) ||
                order == XmlValueOrder.Equal && (!FacetRules.IsExclusive(y) || FacetRules.IsExclusive(x));
        }
        return true;
    }
}

namespace Xemele.Validation;

internal sealed partial class XmlFacetEvaluator
{
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
                    if (!Compatible(plan, current, previous, op) || !Restriction(plan, current, previous, op))
                        return FacetRules.Reject(op, current.Source.Location, "Facet weakens, conflicts with, or changes a fixed inherited restriction.");
                }
            }
            if (FacetRules.IsBound(kind) && plan.Base is not null && !plan.Builtin)
            {
                // Bound values must inhabit the base value space. Equality with
                // a repeated exclusive endpoint is the explicit XSD exception.
                for (var ancestor = plan.Base; ancestor is not null; ancestor = ancestor.Base)
                {
                    for (int j = 0; j < ancestor.Facets.Length; j++)
                    {
                        var inherited = ancestor.Facets[j]; op.Charge(1, current.Source.Location);
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
        return op.Charge(0, plan.Definition.Location);
    }
    private static bool Compatible(FacetPlan plan, PreparedFacet a, PreparedFacet b, ISchemaOperation op)
    {
        var x = a.Source.Kind; var y = b.Source.Kind;
        if (FacetRules.IsBound(x) && FacetRules.IsBound(y) && FacetRules.IsMinimum(x) != FacetRules.IsMinimum(y))
        {
            var min = FacetRules.IsMinimum(x) ? a : b; var max = FacetRules.IsMinimum(x) ? b : a;
            var order = plan.Library.Compare(min.Value!, max.Value!, op);
            return order == XmlValueOrder.Less || order == XmlValueOrder.Equal && !FacetRules.IsExclusive(min.Source.Kind) && !FacetRules.IsExclusive(max.Source.Kind);
        }
        if (x == XmlFacetKind.Length && y == XmlFacetKind.Length) return a.Number == b.Number;
        if (x == XmlFacetKind.Length && y == XmlFacetKind.MinLength) return a.Number >= b.Number;
        if (x == XmlFacetKind.Length && y == XmlFacetKind.MaxLength) return a.Number <= b.Number;
        if (y == XmlFacetKind.Length && x == XmlFacetKind.MinLength) return b.Number >= a.Number;
        if (y == XmlFacetKind.Length && x == XmlFacetKind.MaxLength) return b.Number <= a.Number;
        if (x == XmlFacetKind.MinLength && y == XmlFacetKind.MaxLength) return a.Number <= b.Number;
        if (y == XmlFacetKind.MinLength && x == XmlFacetKind.MaxLength) return b.Number <= a.Number;
        if (x == XmlFacetKind.FractionDigits && y == XmlFacetKind.TotalDigits) return a.Number <= b.Number;
        if (y == XmlFacetKind.FractionDigits && x == XmlFacetKind.TotalDigits) return b.Number <= a.Number;
        return true;
    }
    private static bool Restriction(FacetPlan plan, PreparedFacet current, PreparedFacet previous, ISchemaOperation op)
    {
        var x = current.Source.Kind; var y = previous.Source.Kind;
        if (x == y && previous.Source.Fixed)
        {
            bool equal = FacetRules.IsBound(x) ? plan.Library.ValueEquals(current.Value!, previous.Value!, op) : current.Number == previous.Number;
            if (!equal) return false;
        }
        if (x == y)
        {
            if (x == XmlFacetKind.MinLength) return current.Number >= previous.Number;
            if (x is XmlFacetKind.MaxLength or XmlFacetKind.TotalDigits or XmlFacetKind.FractionDigits) return current.Number <= previous.Number;
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

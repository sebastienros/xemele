using System.Numerics;
namespace Xemele.Validation;

// Retained only after a successful definition check. Arrays are private and never
// escape; each level preserves its own pattern/enumeration grouping and context.
internal sealed class FacetPlan
{
    internal required XmlDatatypeDefinition Definition;
    internal required IXmlDatatypeLibrary Library;
    internal FacetPlan? Base, Item;
    internal FacetPlan[] Members = [];
    internal XmlWhitespaceRule Whitespace;
    internal bool WhitespaceFixed, Builtin;
    internal DatatypePrimitive? Primitive;
    internal PreparedFacet[] Facets = [];
    internal XmlSchemaRegexProgram[] Patterns = [];
    internal XmlDatatypeValue[] Enumeration = [];
    internal bool ConjunctivePatterns;
    internal XmlDatatypeVariety Variety => Definition.Variety;
}
internal readonly record struct PreparedFacet(XmlFacet Source, BigInteger Number = default, XmlDatatypeValue? Value = null);

internal static class FacetRules
{
    internal static bool IsBound(XmlFacetKind kind) => kind is XmlFacetKind.MinInclusive or XmlFacetKind.MinExclusive or XmlFacetKind.MaxInclusive or XmlFacetKind.MaxExclusive;
    internal static bool IsMinimum(XmlFacetKind kind) => kind is XmlFacetKind.MinInclusive or XmlFacetKind.MinExclusive;
    internal static bool IsExclusive(XmlFacetKind kind) => kind is XmlFacetKind.MinExclusive or XmlFacetKind.MaxExclusive;
    internal static bool IsNumber(XmlFacetKind kind) => kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength or XmlFacetKind.TotalDigits or XmlFacetKind.FractionDigits;
    internal static bool Applicable(FacetPlan plan, XmlFacetKind kind)
    {
        if (plan.Variety == XmlDatatypeVariety.Union) return kind is XmlFacetKind.Pattern or XmlFacetKind.Enumeration;
        if (kind is XmlFacetKind.Pattern or XmlFacetKind.Enumeration or XmlFacetKind.WhiteSpace) return true;
        if (plan.Variety == XmlDatatypeVariety.List) return kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength;
        if (IsBound(kind)) return plan.Primitive is DatatypePrimitive.Decimal or DatatypePrimitive.Float or DatatypePrimitive.Double
            or DatatypePrimitive.Duration or DatatypePrimitive.DateTime or DatatypePrimitive.Time or DatatypePrimitive.Date
            or DatatypePrimitive.GYearMonth or DatatypePrimitive.GYear or DatatypePrimitive.GMonthDay or DatatypePrimitive.GDay or DatatypePrimitive.GMonth;
        if (kind is XmlFacetKind.TotalDigits or XmlFacetKind.FractionDigits) return plan.Primitive == DatatypePrimitive.Decimal;
        return plan.Primitive is DatatypePrimitive.String or DatatypePrimitive.HexBinary or DatatypePrimitive.Base64Binary or DatatypePrimitive.AnyUri or DatatypePrimitive.QName or DatatypePrimitive.Notation;
    }
    internal static bool Reject(ISchemaOperation operation, XmlLocation at, string message)
    { operation.AddDiagnostic(new("XSD.SchemaRestriction", XmlDiagnosticSeverity.Error, message, at)); return false; }
}

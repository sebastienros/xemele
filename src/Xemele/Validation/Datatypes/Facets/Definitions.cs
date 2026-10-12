using System.Numerics;
using System.Buffers;
using System.Text;
namespace Xemele.Validation;

internal sealed partial class XmlFacetEvaluator
{
    private FacetPlan? Prepare(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation op)
    {
        op.ChargeComponents(3, type.Location);
        return Build(type, registry, op, new HashSet<SchemaName>(), new Dictionary<SchemaName, FacetPlan>(), 0);
    }
    private FacetPlan? Build(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation op,
        HashSet<SchemaName> active, Dictionary<SchemaName, FacetPlan> completed, int depth)
    {
        op.Charge(1, type.Location); ChargeName(type.Name, op, type.Location);
        if (depth >= 256) { op.Refuse("SCHEMA.Limit", "Datatype graph depth exceeded.", type.Location); op.Charge(0, type.Location); return null; }
        if (completed.TryGetValue(type.Name, out var known)) return known;
        op.ChargeComponents(2, type.Location);
        if (!active.Add(type.Name)) { FacetRules.Reject(op, type.Location, "Cyclic datatype definition."); return null; }
        try
        {
            bool validFinal = true;
            for (int i = 0; i < type.Final.Count; i++) { op.Charge(1, type.Location); validFinal &= Enum.IsDefined(type.Final[i]); }
            if (!Enum.IsDefined(type.Variety) || !validFinal)
            { FacetRules.Reject(op, type.Location, "Invalid datatype variety or final derivation."); return null; }
            var lib = registry.Find(DatatypeTypes.XsdNamespace);
            if (lib is null) { FacetRules.Reject(op, type.Location, "Missing executable XML Schema datatype library."); return null; }
            bool builtin = DatatypeTypes.TryGet(type.Name, out var metadata) && ReferenceEquals(registry.FindType(type.Name), type);
            bool intrinsicAlias = type.Name.NamespaceUri == DatatypeTypes.RngNamespace;
            if (intrinsicAlias && (registry.Find(type.Name.NamespaceUri)?.IsDefined(type.Name) != true || type.BaseType != DatatypeTypes.Name(type.Name.LocalName) ||
                registry.FindType(DatatypeTypes.Name(type.Name.LocalName))?.Variety != type.Variety || type.ItemType is not null || type.MemberTypes.Count != 0))
            { FacetRules.Reject(op, type.Location, "Unknown datatype library alias or mismatched canonical base."); return null; }
            var plan = new FacetPlan { Definition = type, Library = lib, Builtin = builtin,
                ConjunctivePatterns = type.Name.NamespaceUri == DatatypeTypes.RngNamespace };
            FacetPlan? Resolve(SchemaName name)
            {
                ChargeName(name, op, type.Location);
                var definition = registry.FindType(name);
                if (definition is null) { FacetRules.Reject(op, type.Location, "Unknown referenced datatype."); return null; }
                return Build(definition, registry, op, active, completed, depth + 1);
            }
            if (type.BaseType is SchemaName baseName)
            { plan.Base = Resolve(baseName); if (plan.Base is null) return null; }
            bool restriction = plan.Base is not null && plan.Base.Definition.Name != DatatypeTypes.Name("anySimpleType");
            var derivation = restriction || type.Variety == XmlDatatypeVariety.Atomic ? XmlDatatypeDerivation.Restriction
                : type.Variety == XmlDatatypeVariety.List ? XmlDatatypeDerivation.List : XmlDatatypeDerivation.Union;
            if (!builtin && plan.Base is not null && HasFinal(plan.Base, derivation, op))
            { FacetRules.Reject(op, type.Location, "Base datatype prohibits this derivation."); return null; }
            if (restriction && (plan.Base!.Variety != type.Variety || type.ItemType is not null || type.MemberTypes.Count != 0))
            { FacetRules.Reject(op, type.Location, "A restriction must preserve the base variety and members."); return null; }
            if (type.Variety == XmlDatatypeVariety.Atomic)
            {
                if (!builtin && plan.Base is null || type.ItemType is not null || type.MemberTypes.Count != 0)
                { FacetRules.Reject(op, type.Location, "Invalid atomic datatype graph."); return null; }
                plan.Primitive = builtin ? metadata.Primitive : plan.Base!.Primitive;
                if (!builtin && plan.Primitive is null && !(intrinsicAlias && type.Name.LocalName == "anySimpleType" && type.Facets.Count == 0))
                { FacetRules.Reject(op, type.Location, "A user atomic type cannot restrict anySimpleType directly."); return null; }
            }
            else if (type.Variety == XmlDatatypeVariety.List)
            {
                if (type.MemberTypes.Count != 0) { FacetRules.Reject(op, type.Location, "List cannot declare union members."); return null; }
                plan.Item = restriction ? plan.Base!.Item : type.ItemType is SchemaName itemName ? Resolve(itemName) : null;
                if (plan.Item is null || !ListItemAllowed(plan.Item, op, 0) || !builtin && HasFinal(plan.Item, XmlDatatypeDerivation.List, op))
                { FacetRules.Reject(op, type.Location, "Invalid or final list item datatype."); return null; }
            }
            else
            {
                if (type.ItemType is not null) { FacetRules.Reject(op, type.Location, "Union cannot declare a list item."); return null; }
                if (restriction) plan.Members = plan.Base!.Members;
                else
                {
                    if (type.MemberTypes.Count == 0) { FacetRules.Reject(op, type.Location, "Union requires member datatypes."); return null; }
                    if (type.MemberTypes.Count == int.MaxValue) { op.Refuse("SCHEMA.Limit", "Union member representation exceeded.", type.Location); return null; }
                    op.ChargeComponents(type.MemberTypes.Count + 1, type.Location);
                    plan.Members = new FacetPlan[type.MemberTypes.Count];
                    for (int i = 0; i < type.MemberTypes.Count; i++)
                    {
                        var member = Resolve(type.MemberTypes[i]);
                        if (member is null || member.Primitive is null && member.Variety == XmlDatatypeVariety.Atomic || HasFinal(member, XmlDatatypeDerivation.Union, op))
                        { FacetRules.Reject(op, type.Location, "Invalid or final union member datatype."); return null; }
                        plan.Members[i] = member;
                    }
                }
            }
            plan.Whitespace = builtin ? metadata.Whitespace : type.Variety == XmlDatatypeVariety.Atomic ? plan.Base!.Whitespace : XmlWhitespaceRule.Collapse;
            plan.WhitespaceFixed = builtin ? metadata.WhitespaceFixed : type.Variety == XmlDatatypeVariety.List || plan.Base?.WhitespaceFixed == true;
            if (!CompileFacets(plan, op)) return null;
            completed.Add(type.Name, plan); return plan;
        }
        finally { active.Remove(type.Name); }
    }
    private static bool HasFinal(FacetPlan plan, XmlDatatypeDerivation kind, ISchemaOperation op)
    {
        for (int i = 0; i < plan.Definition.Final.Count; i++)
        { op.Charge(1, plan.Definition.Location); if (plan.Definition.Final[i] == kind) return true; }
        return false;
    }
    private static bool ListItemAllowed(FacetPlan plan, ISchemaOperation op, int depth)
    {
        op.Charge(1, plan.Definition.Location);
        if (depth >= 256) { op.Refuse("SCHEMA.Limit", "List member graph depth exceeded.", plan.Definition.Location); op.Charge(0, default); return false; }
        if (plan.Variety == XmlDatatypeVariety.List) return false;
        if (plan.Variety == XmlDatatypeVariety.Atomic) return plan.Primitive is not null;
        for (int i = 0; i < plan.Members.Length; i++) if (!ListItemAllowed(plan.Members[i], op, depth + 1)) return false;
        return true;
    }
    private bool CompileFacets(FacetPlan plan, ISchemaOperation op)
    {
        var definition = plan.Definition;
        if (definition.Facets.Count > (int.MaxValue - 4) / 3) { op.Refuse("SCHEMA.Limit", "Facet representation exceeded.", definition.Location); return false; }
        op.ChargeComponents(4 + definition.Facets.Count * 3, definition.Location);
        var scalar = new List<PreparedFacet>(definition.Facets.Count);
        var patterns = new List<XmlSchemaRegexProgram>(); var enumeration = new List<XmlDatatypeValue>();
        int seen = 0;
        // Compute the effective whitespace first, independently of authored order.
        for (int i = 0; i < definition.Facets.Count; i++)
        {
            var f = definition.Facets[i]; op.Charge(1, f.Location);
            if (!Enum.IsDefined(f.Kind) || !FacetRules.Applicable(plan, f.Kind)) return FacetRules.Reject(op, f.Location, "Facet is not applicable to this datatype.");
            if (plan.ConjunctivePatterns && f.Fixed) return FacetRules.Reject(op, f.Location, "RELAX NG parameters do not have a fixed property.");
            if (f.Kind is XmlFacetKind.Pattern or XmlFacetKind.Enumeration)
            {
                if (f.Fixed) return FacetRules.Reject(op, f.Location, "Pattern and enumeration do not have a fixed property.");
            }
            else
            {
                int flag = 1 << (int)f.Kind;
                if ((seen & flag) != 0) return FacetRules.Reject(op, f.Location, "Duplicate scalar facet.");
                seen |= flag;
            }
            if (plan.ConjunctivePatterns && f.Kind is XmlFacetKind.Enumeration or XmlFacetKind.WhiteSpace)
                return FacetRules.Reject(op, f.Location, "Unsupported RELAX NG datatype parameter.");
            if (f.Kind != XmlFacetKind.WhiteSpace) continue;
            string? text = NormalizeFacet(f.LexicalValue, XmlWhitespaceRule.Collapse, op);
            if (text is null) return FacetRules.Reject(op, f.Location, "Invalid Unicode in facet value.");
            XmlWhitespaceRule? rule = text switch { "preserve" => XmlWhitespaceRule.Preserve, "replace" => XmlWhitespaceRule.Replace, "collapse" => XmlWhitespaceRule.Collapse, _ => null };
            if (rule is null || rule < plan.Whitespace || plan.WhitespaceFixed && rule != plan.Whitespace)
                return FacetRules.Reject(op, f.Location, "Invalid, weakened or fixed whitespace facet.");
            plan.Whitespace = rule.Value; plan.WhitespaceFixed |= f.Fixed;
        }
        for (int i = 0; i < definition.Facets.Count; i++)
        {
            var f = definition.Facets[i]; op.Charge(1, f.Location);
            if (f.Kind == XmlFacetKind.Pattern) { patterns.Add(compiler.Compile(f.LexicalValue, f.Location, op)); continue; }
            if (f.Kind == XmlFacetKind.WhiteSpace) { scalar.Add(new(f, (int)plan.Whitespace)); continue; }
            if (FacetRules.IsNumber(f.Kind))
            {
                if (!ParseNumber(f.LexicalValue, op, out var number) || f.Kind == XmlFacetKind.TotalDigits && number.IsZero)
                    return FacetRules.Reject(op, f.Location, "Invalid nonnegative integer facet value.");
                scalar.Add(new(f, number)); continue;
            }
            var basePlan = plan.Builtin || plan.Base is null || plan.Base.Definition.Name == DatatypeTypes.Name("anySimpleType") ? plan : plan.Base;
            string? normalized = NormalizeFacet(f.LexicalValue, plan.Variety == XmlDatatypeVariety.Union ? XmlWhitespaceRule.Preserve : plan.Whitespace, op);
            if (normalized is null) return FacetRules.Reject(op, f.Location, "Invalid Unicode in facet value.");
            if (!ParseRaw(basePlan, normalized, f.Context, op, out var value, out _))
                return FacetRules.Reject(op, f.Location, "Facet lexical value is not in the base datatype.");
            if (f.Kind == XmlFacetKind.Enumeration)
            {
                if (plan.Base is not null && plan.Variety != XmlDatatypeVariety.Union && !Check(plan.Base, normalized, value!, op))
                    return FacetRules.Reject(op, f.Location, "Enumeration value is outside the base value space.");
                enumeration.Add(value!);
            }
            else scalar.Add(new(f, Value: value));
        }
        plan.Facets = scalar.ToArray(); plan.Patterns = patterns.ToArray(); plan.Enumeration = enumeration.ToArray();
        if (!Consistent(plan, op)) return false;
        return true;
    }
    private static bool ParseNumber(string lexical, ISchemaOperation op, out BigInteger number)
    {
        string? s = NormalizeFacet(lexical, XmlWhitespaceRule.Collapse, op); number = BigInteger.Zero;
        if (s is null) return false;
        int start = 0; bool negative = false;
        if (s.Length != 0 && s[0] is '+' or '-') { negative = s[0] == '-'; start = 1; }
        if (start == s.Length) return false;
        for (int i = start; i < s.Length; i++)
        {
            // Decimal accumulation grows with the already scanned prefix. Charge
            // before arbitrary precision arithmetic and its owned temporary limbs.
            op.Charge(1 + (i - start) / 9, default); op.ChargeComponents(1, default);
            if (s[i] is < '0' or > '9') return false;
            number = number * 10 + s[i] - '0';
        }
        return !negative || number.IsZero;
    }
    private static string? NormalizeFacet(string lexical, XmlWhitespaceRule rule, ISchemaOperation op)
    {
        if (!DatatypeNormalization.TryMeasure(lexical, rule, op, out int length, out _, out _)) return null;
        op.ChargeComponents(1, default);
        char[]? rented = null; Span<char> output = length <= 256 ? stackalloc char[length] : default;
        try
        {
            if (length > 256) { op.Charge(1, default); op.ChargeComponents(1, default); rented = ArrayPool<char>.Shared.Rent(length); output = rented.AsSpan(0, length); }
            if (!DatatypeNormalization.TryNormalize(lexical, rule, output, op, out int n, out _)) return null;
            return new string(output[..n]);
        }
        finally { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }
    private static void ChargeName(SchemaName name, ISchemaOperation op, XmlLocation at)
    {
        ChargeText(name.NamespaceUri, op, at); ChargeText(name.LocalName, op, at);
        op.Charge(1, at);
    }
    private static void ChargeText(string text, ISchemaOperation op, XmlLocation at)
    {
        for (int i = 0; i < text.Length;)
        { op.Charge(1, at); if (Rune.DecodeFromUtf16(text.AsSpan(i), out _, out int n) != OperationStatus.Done) throw new ArgumentException("Invalid datatype name."); i += n; }
    }
}

using System.Buffers;
using System.Text;
namespace Xemele.Validation;

internal sealed partial class XmlFacetEvaluator
{
    private bool CheckAvailableGraph(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation op,
        AvailabilityBuilder availability, HashSet<SchemaName> active, int depth)
    {
        op.Charge(1, type.Location);
        if (depth >= 256)
        {
            op.Refuse("SCHEMA.Limit", "Datatype graph depth exceeded.", type.Location);
            return false;
        }
        op.ChargeComponents(1, type.Location);
        if (!active.Add(type.Name))
        {
            return FacetRules.Reject(op, type.Location, "Cyclic datatype definition.");
        }

        try
        {
            if (!availability.Unavailable(ComponentAvailability.Simple(type.Name)))
            {
                return ValidateDefinition(type, registry, op);
            }

            if (!Enum.IsDefined(type.Variety))
            {
                return FacetRules.Reject(op, type.Location, "Invalid datatype variety.");
            }

            foreach (var final in type.Final)
            {
                op.Charge(1, type.Location);
                if (!Enum.IsDefined(final))
                {
                    return FacetRules.Reject(op, type.Location, "Invalid final derivation.");
                }
            }
            if (!CheckIndependentFacets(type, registry, op))
            {
                return false;
            }

            if (type.BaseType is { } baseName && registry.FindType(baseName) is { } basis && basis.Name != DatatypeTypes.Name("anySimpleType"))
            {
                if (basis.Variety != type.Variety)
                {
                    return FacetRules.Reject(op, type.Location, "Restriction changes datatype variety.");
                }

                if (!CheckAvailableGraph(basis, registry, op, availability, active, depth + 1))
                {
                    return false;
                }
            }
            if (type.Variety == XmlDatatypeVariety.Union)
            {
                if (type.ItemType is not null || type.MemberTypes.Count == 0)
                {
                    return FacetRules.Reject(op, type.Location, "Invalid union datatype graph.");
                }

                foreach (var name in type.MemberTypes)
                {
                    op.Charge(1, type.Location);
                    var member = registry.FindType(name);
                    if (member is null)
                    {
                        return FacetRules.Reject(op, type.Location, "Unknown union member datatype.");
                    }
                    // anySimpleType has no primitive value space. That fact is
                    // decidable even when a different union member is missing.
                    var atomic = member;
                    for (var n = 0; atomic.Variety == XmlDatatypeVariety.Atomic; n++)
                    {
                        op.Charge(1, atomic.Location);
                        if (n >= 256)
                        {
                            op.Refuse("SCHEMA.Limit", "Datatype graph depth exceeded.", atomic.Location);
                            return false;
                        }
                        if (DatatypeTypes.TryGet(atomic.Name, out var metadata))
                        {
                            if (metadata.Primitive is null)
                            {
                                return FacetRules.Reject(op, type.Location, "Invalid atomic union member datatype.");
                            }

                            break;
                        }
                        if (atomic.BaseType is not { } parent || registry.FindType(parent) is not { } next)
                        {
                            return FacetRules.Reject(op, type.Location, "Invalid atomic union member graph.");
                        }

                        atomic = next;
                    }
                    foreach (var final in member.Final)
                    {
                        op.Charge(1, member.Location);
                        if (final == XmlDatatypeDerivation.Union)
                        {
                            return FacetRules.Reject(op, type.Location, "Final union member datatype.");
                        }
                    }
                    if (!CheckAvailableGraph(member, registry, op, availability, active, depth + 1))
                    {
                        return false;
                    }
                }
            }
            else if (type.Variety == XmlDatatypeVariety.List && type.ItemType is { } item && registry.FindType(item) is { } definition)
            {
                if (!CheckAvailableGraph(definition, registry, op, availability, active, depth + 1))
                {
                    return false;
                }
            }
            return op.Charge(0, type.Location);
        }
        finally { active.Remove(type.Name); }
    }
    // A partial union is impossible only when every branch independently
    // rejects. Branch failure emits no diagnostic; cancellation/refusal still
    // propagates immediately. Complete members use their real executable plan.
    private bool CheckIndependentConstraint(XmlDatatypeDefinition type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        IXmlDatatypeRegistry registry, ISchemaOperation op, AvailabilityBuilder availability)
    {
        // A union selects its member's whitespace rule. Keep each possibility
        // through enclosing patterns rather than collapsing before selection.
        for (var rule = XmlWhitespaceRule.Preserve; rule <= XmlWhitespaceRule.Collapse; rule++)
        {
            if (PotentialValue(type, lexical, context, registry, op, availability, rule, 0))
            {
                return op.Charge(0, type.Location);
            }
        }

        return FacetRules.Reject(op, type.Location, "Value constraint independently fails every available alternative.");
    }
    private bool PotentialValue(XmlDatatypeDefinition type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        IXmlDatatypeRegistry registry, ISchemaOperation op, AvailabilityBuilder availability, XmlWhitespaceRule rule, int depth)
    {
        op.Charge(1, type.Location);
        if (depth >= 256)
        {
            op.Refuse("SCHEMA.Limit", "Datatype graph depth exceeded.", type.Location);
            op.Charge(0, type.Location);
            return false;
        }
        if (!availability.Unavailable(ComponentAvailability.Simple(type.Name)))
        {
            if (!prepared.TryGetValue(registry, out var cache) || !cache.TryGetValue(type, out var plan))
            {
                plan = Prepare(type, registry, op);
                if (plan is not null)
                {
                    Publish(type, registry, plan, op);
                }
            }
            var valid = plan is not null && CompletePotential(plan, lexical, context, rule, op, out _);
            op.Charge(0, type.Location);
            return valid;
        }
        if (type.Variety == XmlDatatypeVariety.List && rule != XmlWhitespaceRule.Collapse)
        {
            return false;
        }

        var library = registry.Find(DatatypeTypes.XsdNamespace)!;
        // Compilation owns this temporary normalized text for the candidate
        // branch; no partial typed value or executable plan is manufactured.
        var text = library.Normalize(lexical, rule, op);
        var tokens = 0;
        var inToken = false;
        foreach (var c in text)
        {
            op.Charge(1, type.Location);
            if (c == ' ')
            {
                inToken = false;
            }
            else if (!inToken)
            {
                tokens++;
                inToken = true;
            }
        }
        var count = new FacetNumber((ulong)tokens);
        var levels = 0;
        for (var level = type; level is not null; level = level.BaseType is { } name ? registry.FindType(name) : null)
        {
            op.Charge(1, level.Location);
            if (++levels > 256)
            {
                op.Refuse("SCHEMA.Limit", "Datatype graph depth exceeded.", level.Location);
                op.Charge(0, level.Location);
                return false;
            }
            bool hasPattern = false, matched = false;
            foreach (var facet in level.Facets)
            {
                op.Charge(1, facet.Location);
                if (facet.Kind == XmlFacetKind.Pattern)
                {
                    hasPattern = true;
                    var regex = compiler.Compile(facet.LexicalValue, facet.Location, op);
                    matched |= matcher.IsMatch(regex, text.AsSpan(), op);
                }
                else if (type.Variety == XmlDatatypeVariety.List && facet.Kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength)
                {
                    if (!ParseNumber(facet.LexicalValue, op, out var bound))
                    {
                        return false;
                    }

                    var comparison = count.Compare(bound, op, facet.Location);
                    if (comparison is null || (facet.Kind == XmlFacetKind.Length ? comparison != 0 : facet.Kind == XmlFacetKind.MinLength ? comparison < 0 : comparison > 0))
                    {
                        return false;
                    }
                }
            }
            if (hasPattern && !matched)
            {
                return false;
            }
        }
        if (type.Variety == XmlDatatypeVariety.Union)
        {
            foreach (var name in type.MemberTypes)
            {
                op.Charge(1, type.Location);
                var member = registry.FindType(name)!;
                if (PotentialValue(member, lexical, context, registry, op, availability, rule, depth + 1))
                {
                    return true;
                }
            }
            return false;
        }
        // The list item remains unavailable. Decidable grammar/pattern/count
        // checks passed; no fabricated item value or partial plan is produced.
        return op.Charge(0, type.Location);
    }
    private bool CompletePotential(FacetPlan plan, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        XmlWhitespaceRule rule, ISchemaOperation op, out XmlDatatypeValue? value)
    {
        op.Charge(1, plan.Definition.Location);
        value = null;
        if (plan.Variety != XmlDatatypeVariety.Union)
        {
            return plan.Whitespace == rule && Parse(plan, lexical, context, op, out value, out _);
        }

        foreach (var member in plan.Members)
        {
            op.Charge(1, plan.Definition.Location);
            if (!CompletePotential(member, lexical, context, rule, op, out var selected))
            {
                continue;
            }

            if (!NormalizedCheck(plan, lexical, rule, selected!, op, out _))
            {
                continue;
            }

            value = plan.Library.CreateUnion(plan.Definition.Name, selected!, op);
            return op.Charge(0, plan.Definition.Location);
        }
        return false;
    }
    private bool CheckIndependentFacets(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation op)
    {
        op.ChargeComponents(1, type.Location);
        var counts = new List<(XmlFacet Facet, FacetNumber Number, int Level)>();
        var level = 0;
        for (var definition = type; definition is not null; definition = definition.BaseType is { } parent ? registry.FindType(parent) : null)
        {
            op.Charge(1, definition.Location);
            // Intrinsic definitions have their own complete-plan validation;
            // anySimpleType's nominal whitespace is not a list restriction.
            if (DatatypeTypes.TryGet(definition.Name, out _) && ReferenceEquals(registry.FindType(definition.Name), definition))
            {
                break;
            }

            if (++level > 256)
            {
                op.Refuse("SCHEMA.Limit", "Datatype graph depth exceeded.", definition.Location);
                return false;
            }
            var seen = 0;
            foreach (var facet in definition.Facets)
            {
                op.Charge(1, facet.Location);
                if (!Enum.IsDefined(facet.Kind) || (definition.Variety == XmlDatatypeVariety.List
                    ? facet.Kind is not (XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength or XmlFacetKind.Pattern or XmlFacetKind.Enumeration or XmlFacetKind.WhiteSpace)
                    : facet.Kind is not (XmlFacetKind.Pattern or XmlFacetKind.Enumeration)))
                {
                    return FacetRules.Reject(op, facet.Location, "Facet is not applicable to this datatype.");
                }

                if (facet.Kind is XmlFacetKind.Pattern or XmlFacetKind.Enumeration)
                {
                    if (facet.Fixed)
                    {
                        return FacetRules.Reject(op, facet.Location, "Pattern and enumeration do not have a fixed property.");
                    }
                }
                else
                {
                    var flag = 1 << (int)facet.Kind;
                    if ((seen & flag) != 0)
                    {
                        return FacetRules.Reject(op, facet.Location, "Duplicate scalar facet.");
                    }

                    seen |= flag;
                }
                if (facet.Kind == XmlFacetKind.Pattern)
                {
                    compiler.Compile(facet.LexicalValue, facet.Location, op);
                    continue;
                }
                if (facet.Kind == XmlFacetKind.WhiteSpace)
                {
                    if (NormalizeFacet(facet.LexicalValue, XmlWhitespaceRule.Collapse, op) != "collapse")
                    {
                        return FacetRules.Reject(op, facet.Location, "List whitespace must remain collapse.");
                    }
                }
                else if (FacetRules.IsNumber(facet.Kind))
                {
                    if (!ParseNumber(facet.LexicalValue, op, out var number))
                    {
                        return FacetRules.Reject(op, facet.Location, "Invalid nonnegative integer facet value.");
                    }

                    op.ChargeComponents(1, facet.Location);
                    counts.Add((facet, number, level));
                }
                else if (NormalizeFacet(facet.LexicalValue, XmlWhitespaceRule.Collapse, op) is null)
                {
                    return FacetRules.Reject(op, facet.Location, "Invalid Unicode in facet value.");
                }
                // Enumeration's typed operand is unavailable. Its grammar,
                // fixed flag and Unicode are still checked above.
            }
        }
        foreach (var (Facet, Number, Level) in counts)
        {
            foreach (var b in counts)
            {
                op.Charge(1, Facet.Location);
                var cmp = Number.Compare(b.Number, op, Facet.Location);
                bool minA = Facet.Kind == XmlFacetKind.MinLength, maxA = Facet.Kind == XmlFacetKind.MaxLength;
                bool minB = b.Facet.Kind == XmlFacetKind.MinLength, maxB = b.Facet.Kind == XmlFacetKind.MaxLength;
                if ((!maxA && !minB && cmp > 0) || (!minA && !maxB && cmp < 0) ||
                    Level < b.Level && Facet.Kind == b.Facet.Kind && (b.Facet.Fixed && cmp != 0 || minA && cmp < 0 || maxA && cmp > 0))
                {
                    return FacetRules.Reject(op, Facet.Location, "Conflicting or weakened length facets.");
                }
            }
        }

        foreach (var (Facet, Number, Level) in counts)
        {
            op.Charge(1, Facet.Location);
            if (Facet.Kind != XmlFacetKind.Length)
            {
                continue;
            }

            foreach (var bound in counts)
            {
                op.Charge(1, bound.Facet.Location);
                if (bound.Facet.Kind is not (XmlFacetKind.MinLength or XmlFacetKind.MaxLength))
                {
                    continue;
                }

                var effective = true;
                foreach (var earlier in counts)
                {
                    op.Charge(1, earlier.Facet.Location);
                    if (earlier.Level < bound.Level && earlier.Facet.Kind == bound.Facet.Kind)
                    {
                        effective = false;
                    }
                }
                if (!effective)
                {
                    continue;
                }

                var origin = false;
                foreach (var previous in counts)
                {
                    op.Charge(1, previous.Facet.Location);
                    if (previous.Level <= Level || previous.Facet.Kind != bound.Facet.Kind || previous.Number.Compare(bound.Number, op) != 0)
                    {
                        continue;
                    }

                    var hasLength = false;
                    foreach (var old in counts)
                    {
                        op.Charge(1, old.Facet.Location);
                        hasLength |= old.Level >= previous.Level && old.Facet.Kind == XmlFacetKind.Length;
                    }
                    origin |= !hasLength;
                }
                if (!origin)
                {
                    return FacetRules.Reject(op, bound.Facet.Location, "Length bounds require an unchanged ancestor without length.");
                }
            }
        }
        return op.Charge(0, type.Location);
    }
    private FacetPlan? Prepare(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation op)
    {
        op.ChargeComponents(3, type.Location);
        return Build(type, registry, op, [], [], 0);
    }
    private FacetPlan? Build(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation op,
        HashSet<SchemaName> active, Dictionary<SchemaName, FacetPlan> completed, int depth)
    {
        op.Charge(1, type.Location);
        ChargeName(type.Name, op, type.Location);
        if (depth >= 256)
        {
            op.Refuse("SCHEMA.Limit", "Datatype graph depth exceeded.", type.Location);
            op.Charge(0, type.Location);
            return null;
        }
        if (completed.TryGetValue(type.Name, out var known))
        {
            return known;
        }

        op.ChargeComponents(2, type.Location);
        if (!active.Add(type.Name))
        {
            FacetRules.Reject(op, type.Location, "Cyclic datatype definition.");
            return null;
        }
        try
        {
            var validFinal = true;
            for (var i = 0; i < type.Final.Count; i++)
            {
                op.Charge(1, type.Location);
                validFinal &= Enum.IsDefined(type.Final[i]);
            }
            if (!Enum.IsDefined(type.Variety) || !validFinal)
            {
                FacetRules.Reject(op, type.Location, "Invalid datatype variety or final derivation.");
                return null;
            }
            var lib = registry.Find(DatatypeTypes.XsdNamespace);
            if (lib is null)
            {
                FacetRules.Reject(op, type.Location, "Missing executable XML Schema datatype library.");
                return null;
            }
            var builtin = DatatypeTypes.TryGet(type.Name, out var metadata) && ReferenceEquals(registry.FindType(type.Name), type);
            var intrinsicAlias = type.Name.NamespaceUri == DatatypeTypes.RngNamespace;
            if (intrinsicAlias && (registry.Find(type.Name.NamespaceUri)?.IsDefined(type.Name) != true || type.BaseType != DatatypeTypes.Name(type.Name.LocalName) ||
                registry.FindType(DatatypeTypes.Name(type.Name.LocalName))?.Variety != type.Variety || type.ItemType is not null || type.MemberTypes.Count != 0))
            {
                FacetRules.Reject(op, type.Location, "Unknown datatype library alias or mismatched canonical base.");
                return null;
            }
            var plan = new FacetPlan
            {
                Definition = type,
                Library = lib,
                Builtin = builtin,
                ConjunctivePatterns = type.Name.NamespaceUri == DatatypeTypes.RngNamespace
            };
            FacetPlan? Resolve(SchemaName name)
            {
                ChargeName(name, op, type.Location);
                var definition = registry.FindType(name);
                if (definition is null)
                {
                    FacetRules.Reject(op, type.Location, "Unknown referenced datatype.");
                    return null;
                }
                return Build(definition, registry, op, active, completed, depth + 1);
            }
            if (type.BaseType is SchemaName baseName)
            {
                plan.Base = Resolve(baseName);
                if (plan.Base is null)
                {
                    return null;
                }
            }
            var restriction = plan.Base is not null && plan.Base.Definition.Name != DatatypeTypes.Name("anySimpleType");
            var derivation = restriction || type.Variety == XmlDatatypeVariety.Atomic ? XmlDatatypeDerivation.Restriction
                : type.Variety == XmlDatatypeVariety.List ? XmlDatatypeDerivation.List : XmlDatatypeDerivation.Union;
            if (!builtin && plan.Base is not null && HasFinal(plan.Base, derivation, op))
            {
                FacetRules.Reject(op, type.Location, "Base datatype prohibits this derivation.");
                return null;
            }
            if (restriction && (plan.Base!.Variety != type.Variety || !PreservesMembers(type, plan.Base, op)))
            {
                FacetRules.Reject(op, type.Location, "A restriction must preserve the base variety and members.");
                return null;
            }
            if (type.Variety == XmlDatatypeVariety.Atomic)
            {
                if (!builtin && plan.Base is null || type.ItemType is not null || type.MemberTypes.Count != 0)
                {
                    FacetRules.Reject(op, type.Location, "Invalid atomic datatype graph.");
                    return null;
                }
                plan.Primitive = builtin ? metadata.Primitive : plan.Base!.Primitive;
                if (!builtin && plan.Primitive is null && !(intrinsicAlias && type.Name.LocalName == "anySimpleType" && type.Facets.Count == 0))
                {
                    FacetRules.Reject(op, type.Location, "A user atomic type cannot restrict anySimpleType directly.");
                    return null;
                }
            }
            else if (type.Variety == XmlDatatypeVariety.List)
            {
                if (type.MemberTypes.Count != 0)
                {
                    FacetRules.Reject(op, type.Location, "List cannot declare union members.");
                    return null;
                }
                plan.Item = restriction ? plan.Base!.Item : type.ItemType is SchemaName itemName ? Resolve(itemName) : null;
                if (plan.Item is null || !ListItemAllowed(plan.Item, op, 0) || !builtin && HasFinal(plan.Item, XmlDatatypeDerivation.List, op))
                {
                    FacetRules.Reject(op, type.Location, "Invalid or final list item datatype.");
                    return null;
                }
            }
            else
            {
                if (type.ItemType is not null)
                {
                    FacetRules.Reject(op, type.Location, "Union cannot declare a list item.");
                    return null;
                }
                if (restriction)
                {
                    plan.Members = plan.Base!.Members;
                }
                else
                {
                    if (type.MemberTypes.Count == 0)
                    {
                        FacetRules.Reject(op, type.Location, "Union requires member datatypes.");
                        return null;
                    }
                    if (type.MemberTypes.Count == int.MaxValue)
                    {
                        op.Refuse("SCHEMA.Limit", "Union member representation exceeded.", type.Location);
                        return null;
                    }
                    op.ChargeComponents(type.MemberTypes.Count + 1, type.Location);
                    plan.Members = new FacetPlan[type.MemberTypes.Count];
                    for (var i = 0; i < type.MemberTypes.Count; i++)
                    {
                        var member = Resolve(type.MemberTypes[i]);
                        if (member is null || member.Primitive is null && member.Variety == XmlDatatypeVariety.Atomic || HasFinal(member, XmlDatatypeDerivation.Union, op))
                        {
                            FacetRules.Reject(op, type.Location, "Invalid or final union member datatype.");
                            return null;
                        }
                        plan.Members[i] = member;
                    }
                }
            }
            plan.Whitespace = builtin ? metadata.Whitespace : type.Variety == XmlDatatypeVariety.Atomic ? plan.Base!.Whitespace : XmlWhitespaceRule.Collapse;
            plan.WhitespaceFixed = builtin ? metadata.WhitespaceFixed : type.Variety == XmlDatatypeVariety.List || plan.Base?.WhitespaceFixed == true;
            if (!CompileFacets(plan, op))
            {
                return null;
            }

            completed.Add(type.Name, plan);
            return plan;
        }
        finally { active.Remove(type.Name); }
    }
    private static bool HasFinal(FacetPlan plan, XmlDatatypeDerivation kind, ISchemaOperation op)
    {
        for (var i = 0; i < plan.Definition.Final.Count; i++)
        {
            op.Charge(1, plan.Definition.Location);
            if (plan.Definition.Final[i] == kind)
            {
                return true;
            }
        }
        return false;
    }
    private static bool ListItemAllowed(FacetPlan plan, ISchemaOperation op, int depth)
    {
        op.Charge(1, plan.Definition.Location);
        if (depth >= 256)
        {
            op.Refuse("SCHEMA.Limit", "List member graph depth exceeded.", plan.Definition.Location);
            op.Charge(0, default);
            return false;
        }
        if (plan.Variety == XmlDatatypeVariety.List)
        {
            return false;
        }

        if (plan.Variety == XmlDatatypeVariety.Atomic)
        {
            return plan.Primitive is not null;
        }

        for (var i = 0; i < plan.Members.Length; i++)
        {
            if (!ListItemAllowed(plan.Members[i], op, depth + 1))
            {
                return false;
            }
        }

        return true;
    }
    private bool CompileFacets(FacetPlan plan, ISchemaOperation op)
    {
        var definition = plan.Definition;
        if (definition.Facets.Count > (int.MaxValue - 4) / 3)
        {
            op.Refuse("SCHEMA.Limit", "Facet representation exceeded.", definition.Location);
            return false;
        }
        op.ChargeComponents(4 + definition.Facets.Count * 3, definition.Location);
        var scalar = new List<PreparedFacet>(definition.Facets.Count);
        var patterns = new List<XmlSchemaRegexProgram>();
        var enumeration = new List<XmlDatatypeValue>();
        var seen = 0;
        // Compute the effective whitespace first, independently of authored order.
        for (var i = 0; i < definition.Facets.Count; i++)
        {
            var f = definition.Facets[i];
            op.Charge(1, f.Location);
            if (!Enum.IsDefined(f.Kind) || !FacetRules.Applicable(plan, f.Kind))
            {
                return FacetRules.Reject(op, f.Location, "Facet is not applicable to this datatype.");
            }

            if (plan.ConjunctivePatterns && f.Fixed)
            {
                return FacetRules.Reject(op, f.Location, "RELAX NG parameters do not have a fixed property.");
            }

            if (f.Kind is XmlFacetKind.Pattern or XmlFacetKind.Enumeration)
            {
                if (f.Fixed)
                {
                    return FacetRules.Reject(op, f.Location, "Pattern and enumeration do not have a fixed property.");
                }
            }
            else
            {
                var flag = 1 << (int)f.Kind;
                if ((seen & flag) != 0)
                {
                    return FacetRules.Reject(op, f.Location, "Duplicate scalar facet.");
                }

                seen |= flag;
            }
            if (plan.ConjunctivePatterns && f.Kind is XmlFacetKind.Enumeration or XmlFacetKind.WhiteSpace)
            {
                return FacetRules.Reject(op, f.Location, "Unsupported RELAX NG datatype parameter.");
            }

            if (f.Kind != XmlFacetKind.WhiteSpace)
            {
                continue;
            }

            var text = NormalizeFacet(f.LexicalValue, XmlWhitespaceRule.Collapse, op);
            if (text is null)
            {
                return FacetRules.Reject(op, f.Location, "Invalid Unicode in facet value.");
            }

            XmlWhitespaceRule? rule = text switch
            {
                "preserve" => XmlWhitespaceRule.Preserve,
                "replace" => XmlWhitespaceRule.Replace,
                "collapse" => XmlWhitespaceRule.Collapse,
                _ => null
            };
            if (rule is null || rule < plan.Whitespace || plan.WhitespaceFixed && rule != plan.Whitespace)
            {
                return FacetRules.Reject(op, f.Location, "Invalid, weakened or fixed whitespace facet.");
            }

            plan.Whitespace = rule.Value;
            plan.WhitespaceFixed |= f.Fixed;
        }
        for (var i = 0; i < definition.Facets.Count; i++)
        {
            var f = definition.Facets[i];
            op.Charge(1, f.Location);
            if (f.Kind == XmlFacetKind.Pattern)
            {
                patterns.Add(compiler.Compile(f.LexicalValue, f.Location, op));
                continue;
            }
            if (f.Kind == XmlFacetKind.WhiteSpace)
            {
                scalar.Add(new(f, new FacetNumber((ulong)plan.Whitespace)));
                continue;
            }
            if (FacetRules.IsNumber(f.Kind))
            {
                if (!ParseNumber(f.LexicalValue, op, out var number) || f.Kind == XmlFacetKind.TotalDigits && number.IsZero)
                {
                    return FacetRules.Reject(op, f.Location, "Invalid nonnegative integer facet value.");
                }

                scalar.Add(new(f, number));
                continue;
            }
            var basePlan = plan.Builtin || plan.Base is null || plan.Base.Definition.Name == DatatypeTypes.Name("anySimpleType") ? plan : plan.Base;
            var normalized = NormalizeFacet(f.LexicalValue, plan.Variety == XmlDatatypeVariety.Union ? XmlWhitespaceRule.Preserve : plan.Whitespace, op);
            if (normalized is null)
            {
                return FacetRules.Reject(op, f.Location, "Invalid Unicode in facet value.");
            }

            if (!ParseRaw(basePlan, normalized, f.Context, op, out var value, out _))
            {
                return FacetRules.Reject(op, f.Location, "Facet lexical value is not in the base datatype.");
            }

            if (f.Kind == XmlFacetKind.Enumeration)
            {
                if (plan.Base is not null && plan.Variety != XmlDatatypeVariety.Union && !Check(plan.Base, normalized, value!, op))
                {
                    return FacetRules.Reject(op, f.Location, "Enumeration value is outside the base value space.");
                }

                enumeration.Add(value!);
            }
            else
            {
                scalar.Add(new(f, Value: value));
            }
        }
        plan.Facets = [.. scalar];
        plan.Patterns = [.. patterns];
        plan.Enumeration = [.. enumeration];
        SelectEffectiveScalars(plan, op);
        if (!Consistent(plan, op))
        {
            return false;
        }

        return true;
    }
    private static bool ParseNumber(string lexical, ISchemaOperation op, out FacetNumber number)
    {
        var text = NormalizeFacet(lexical, XmlWhitespaceRule.Collapse, op);
        number = default;
        return text is not null && FacetNumber.TryParse(text, op, out number);
    }
    private static bool PreservesMembers(XmlDatatypeDefinition type, FacetPlan parent, ISchemaOperation op)
    {
        if (type.ItemType is SchemaName item && (parent.Item is null || !SameName(item, parent.Item.Definition.Name, op, type.Location)))
        {
            return false;
        }

        if (type.MemberTypes.Count == 0)
        {
            return true;
        }

        if (type.MemberTypes.Count != parent.Members.Length)
        {
            return false;
        }

        for (var i = 0; i < type.MemberTypes.Count; i++)
        {
            op.Charge(1, type.Location);
            if (!SameName(type.MemberTypes[i], parent.Members[i].Definition.Name, op, type.Location))
            {
                return false;
            }
        }
        return true;
    }
    private static bool SameName(SchemaName a, SchemaName b, ISchemaOperation op, XmlLocation at) =>
        SameText(a.NamespaceUri, b.NamespaceUri, op, at) && SameText(a.LocalName, b.LocalName, op, at);
    private static bool SameText(string a, string b, ISchemaOperation op, XmlLocation at)
    {
        op.Charge(1, at);
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            op.Charge(1, at);
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }
    private static string? NormalizeFacet(string lexical, XmlWhitespaceRule rule, ISchemaOperation op)
    {
        if (!DatatypeNormalization.TryMeasure(lexical, rule, op, out var length, out _, out _))
        {
            return null;
        }

        op.ChargeComponents(1, default);
        char[]? rented = null;
        var output = length <= 256 ? stackalloc char[length] : default;
        try
        {
            if (length > 256)
            {
                op.Charge(1, default);
                op.ChargeComponents(1, default);
                rented = ArrayPool<char>.Shared.Rent(length);
                output = rented.AsSpan(0, length);
            }
            if (!DatatypeNormalization.TryNormalize(lexical, rule, output, op, out var n, out _))
            {
                return null;
            }

            return new string(output[..n]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
    private static void ChargeName(SchemaName name, ISchemaOperation op, XmlLocation at)
    {
        ChargeText(name.NamespaceUri, op, at);
        ChargeText(name.LocalName, op, at);
        op.Charge(1, at);
    }
    private static void ChargeText(string text, ISchemaOperation op, XmlLocation at)
    {
        for (var i = 0; i < text.Length;)
        {
            op.Charge(1, at);
            if (Rune.DecodeFromUtf16(text.AsSpan(i), out _, out var n) != OperationStatus.Done)
            {
                throw new ArgumentException("Invalid datatype name.");
            }

            i += n;
        }
    }
}

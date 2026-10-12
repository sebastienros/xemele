using System.Numerics;
namespace Xemele.Validation;

internal sealed partial class ContentCompilation
{
    private void CheckDerivation(XsdTypeDefinition type)
    {
        if (type.BaseType is not { } baseName || type.Name == Any) return;
        var basis = types[baseName]; ContentWork.Tick(op, type.Location);
        CheckAttributes(type, basis);
        if (type.ContentKind == XsdContentKind.Simple && type.SimpleType == type.Name) return; // Datatype wrapper; facets own its definition.
        if (basis.Final.Contains(type.Derivation)) ContentWork.Error(op, type.Location, "Base final forbids this derivation.");
        if (type.Derivation == XsdDerivation.Extension)
        {
            bool valid = basis.ContentKind switch
            {
                XsdContentKind.Simple => type.ContentKind == XsdContentKind.Simple && type.SimpleType == basis.SimpleType,
                XsdContentKind.Empty => type.ContentKind != XsdContentKind.Simple,
                _ => type.ContentKind == basis.ContentKind && (basis.Particle is null ? true : type.Particle is not null && Extension(type.Particle, basis.Particle))
            };
            if (!valid) ContentWork.Error(op, type.Location, "Extension changes content kind or fails to preserve the base particle.");
            CheckExtensionHistory(type);
        }
        else if (type.Derivation == XsdDerivation.Restriction && basis.Name != Any && !RestrictionContent(type, basis))
            ContentWork.Error(op, type.Location, "Content is not a valid restriction of its base type.");
    }
    private bool RestrictionContent(XsdTypeDefinition type, XsdTypeDefinition basis)
    {
        // Dispatch on content kind before treating a missing/pointless particle
        // as empty: a mixed type still accepts character data with no particle.
        return type.ContentKind switch
        {
            XsdContentKind.Simple => basis.ContentKind == XsdContentKind.Simple
                ? type.SimpleType is { } d && basis.SimpleType is { } b && TypeDerives(d, b, false)
                : basis.ContentKind == XsdContentKind.Mixed && Nullable(basis.Particle),
            XsdContentKind.Empty => basis.ContentKind != XsdContentKind.Simple && Nullable(basis.Particle),
            XsdContentKind.Mixed => basis.ContentKind == XsdContentKind.Mixed && Restricts(type.Particle, basis.Particle, false),
            _ => basis.ContentKind != XsdContentKind.Simple &&
                (IsPointlessEmpty(type.Particle) ? Nullable(basis.Particle) : Restricts(type.Particle, basis.Particle, false))
        };
    }
    private void CheckAttributes(XsdTypeDefinition type, XsdTypeDefinition basis)
    {
        var current = Uses(type); var inherited = Uses(basis);
        bool extension = type.Derivation == XsdDerivation.Extension;
        foreach (var pair in inherited)
        {
            ContentWork.Tick(op, pair.Value.Location);
            bool present = current.TryGetValue(pair.Key, out var use);
            if ((extension || pair.Value.Use == XsdAttributeUse.Required) && (!present || use!.Use == XsdAttributeUse.Prohibited || pair.Value.Use == XsdAttributeUse.Required && use.Use != XsdAttributeUse.Required))
                ContentWork.Error(op, type.Location, "Derivation removes or weakens an inherited attribute use.");
            if (extension && present && schema.Attributes[use!.DeclarationId].Type != schema.Attributes[pair.Value.DeclarationId].Type)
                ContentWork.Error(op, use.Location, "Extension changes an inherited attribute type.");
        }
        foreach (var pair in current)
        {
            var use = pair.Value; if (use.Use == XsdAttributeUse.Prohibited) continue;
            var declaration = schema.Attributes[use.DeclarationId]; ContentWork.Tick(op, use.Location);
            if (inherited.TryGetValue(pair.Key, out var old))
            {
                if (!extension && !TypeDerives(declaration.Type, schema.Attributes[old.DeclarationId].Type, false))
                    ContentWork.Error(op, use.Location, "Restricted attribute type is not derived from its base attribute type.");
                string? fixedBase = old.FixedValue ?? schema.Attributes[old.DeclarationId].FixedValue;
                string? fixedCurrent = use.FixedValue ?? declaration.FixedValue;
                // §3.2.2/§3.5 constraints contain actual datatype values. The
                // frozen structural lane retains its lexical-only comparison.
                if (fixedBase is not null && (fixedCurrent is null ||
                    (facets is null ? fixedCurrent != fixedBase : !FixedEquals(use, old))))
                    ContentWork.Error(op, use.Location, "Derivation changes an inherited fixed attribute use.");
            }
            else if (!extension && (basis.AnyAttribute is null || !ContentNamespaces.Allows(basis.AnyAttribute, declaration.Name.NamespaceUri, op, use.Location)))
                ContentWork.Error(op, use.Location, "Restricted attribute is outside the base attribute wildcard.");
        }
        if (extension)
        {
            if (basis.AnyAttribute is { } old && (type.AnyAttribute is not { } next || !ContentNamespaces.Subset(old, next, op, type.Location)))
                ContentWork.Error(op, type.Location, "Extension does not preserve its base attribute wildcard.");
        }
        else if (type.AnyAttribute is { } next)
        {
            if (basis.AnyAttribute is not { } old || !ContentNamespaces.Subset(next, old, op, type.Location) || basis.Name != Any && next.ProcessContents > old.ProcessContents)
                ContentWork.Error(op, type.Location, "Attribute wildcard restriction widens namespaces or weakens processContents.");
        }
    }
    private Dictionary<SchemaName, XsdAttributeBinding> Uses(XsdTypeDefinition type)
    {
        var result = new Dictionary<SchemaName, XsdAttributeBinding>();
        foreach (var use in type.Attributes)
        {
            ContentWork.Tick(op, use.Location);
            if (use.Use == XsdAttributeUse.Prohibited) continue;
            ContentWork.Retain(op, use.Location);
            if (!result.TryAdd(schema.Attributes[use.DeclarationId].Name, use)) ContentWork.Error(op, use.Location, "Duplicate effective attribute name.");
        }
        return result;
    }
    private bool TypeDerives(SchemaName derived, SchemaName basis, bool onlyRestriction)
    {
        // Work-list permits the special union-member rule without recursive CLR stack growth.
        var queue = new Queue<(SchemaName D, SchemaName B)>(); var seen = new HashSet<(SchemaName, SchemaName)>();
        ContentWork.Retain(op, default); queue.Enqueue((derived, basis));
        while (queue.TryDequeue(out var pair))
        {
            ContentWork.Tick(op, default); if (pair.D == pair.B) return true;
            if (!seen.Add(pair)) continue; ContentWork.Retain(op, default);
            if (schema.Datatypes.FindType(pair.B) is { Variety: XmlDatatypeVariety.Union } union)
                foreach (var member in union.MemberTypes) { ContentWork.Retain(op, union.Location); queue.Enqueue((pair.D, member)); }
            if (!types.TryGetValue(pair.D, out var type) || type.BaseType is not { } parent) continue;
            if (onlyRestriction && type.Derivation == XsdDerivation.Extension) continue;
            if (types.TryGetValue(parent, out var parentType) && parentType.Final.Contains(type.Derivation)) continue;
            ContentWork.Retain(op, type.Location); queue.Enqueue((parent, pair.B));
        }
        return false;
    }
    private bool Extension(XsdParticle? derived, XsdParticle basis)
    {
        if (SameParticle(derived, basis)) return true;
        return derived is { MinOccurs.IsOne: true, MaxOccurs: { IsOne: true }, Term: XsdGroupTerm { Compositor: XsdCompositor.Sequence } group }
            && group.Particles.Count > 0 && SameParticle(group.Particles[0], basis);
    }
    private bool SameParticle(XsdParticle? a, XsdParticle? b)
    {
        var work = new Stack<(XsdParticle? A, XsdParticle? B)>(); work.Push((a, b));
        while (work.TryPop(out var pair))
        {
            ContentWork.Tick(op, pair.A?.Location ?? default);
            if (ReferenceEquals(pair.A, pair.B)) continue;
            if (pair.A is not { } x || pair.B is not { } y || ContentIntegers.Compare(x.MinOccurs, y.MinOccurs, op, x.Location) != 0 || !EqualMaximum(x.MaxOccurs, y.MaxOccurs, x.Location)) return false;
            ContentWork.Integer(op, x.Location, x.MinOccurs); if (x.MaxOccurs is { } max) ContentWork.Integer(op, x.Location, max);
            if (x.Term is XsdElementTerm xe && y.Term is XsdElementTerm ye) { if (xe.DeclarationId != ye.DeclarationId) return false; }
            else if (x.Term is XsdWildcardTerm xw && y.Term is XsdWildcardTerm yw)
            { if (xw.Wildcard.ProcessContents != yw.Wildcard.ProcessContents || !ContentNamespaces.SetSubset(xw.Wildcard, yw.Wildcard, op, x.Location) || !ContentNamespaces.SetSubset(yw.Wildcard, xw.Wildcard, op, x.Location)) return false; }
            else if (x.Term is XsdGroupTerm xg && y.Term is XsdGroupTerm yg)
            {
                if (xg.Compositor != yg.Compositor || xg.Particles.Count != yg.Particles.Count) return false;
                for (int i = 0; i < xg.Particles.Count; i++) { ContentWork.Retain(op, x.Location); work.Push((xg.Particles[i], yg.Particles[i])); }
            }
            else return false;
        }
        return true;
    }
    private readonly Dictionary<XsdParticle, bool> nullable = new(ReferenceEqualityComparer.Instance);
    private bool Nullable(XsdParticle? p)
    {
        if (p is null) return true;
        var pending = new Stack<(XsdParticle Particle, bool Ready)>();
        ContentWork.Retain(op, p.Location); pending.Push((p, false));
        while (pending.TryPop(out var item))
        {
            var particle = item.Particle; ContentWork.Tick(op, particle.Location);
            if (nullable.ContainsKey(particle)) continue;
            bool result = particle.MinOccurs.IsZero || particle.MaxOccurs == BigInteger.Zero;
            if (!result && particle.Term is XsdGroupTerm group)
            {
                if (!item.Ready)
                {
                    ContentWork.Retain(op, particle.Location); pending.Push((particle, true));
                    foreach (var child in group.Particles) { ContentWork.Retain(op, child.Location); pending.Push((child, false)); }
                    continue;
                }
                result = group.Compositor != XsdCompositor.Choice;
                foreach (var child in group.Particles)
                { ContentWork.Tick(op, child.Location); if (group.Compositor == XsdCompositor.Choice) result |= nullable[child]; else result &= nullable[child]; }
            }
            ContentWork.Retain(op, particle.Location); nullable.Add(particle, result);
        }
        return nullable[p];
    }
    private bool IsPointlessEmpty(XsdParticle? p) => p is null || p.MaxOccurs == BigInteger.Zero || p.Term is XsdGroupTerm { Particles.Count: 0, Compositor: not XsdCompositor.Choice };

    private XsdParticle? Normalize(XsdParticle? p)
    {
        while (p is { Term: XsdGroupTerm group })
        {
            ContentWork.Tick(op, p.Location);
            if (p.MaxOccurs == BigInteger.Zero || group.Particles.Count == 0 && (group.Compositor != XsdCompositor.Choice || p.MinOccurs.IsZero)) return null;
            if (group.Particles.Count == 1 && (group.Compositor == XsdCompositor.All || p.MinOccurs.IsOne && p.MaxOccurs is { IsOne: true }))
            {
                var child = group.Particles[0];
                if (group.Compositor == XsdCompositor.All && p.MinOccurs.IsZero)
                { ContentWork.Retain(op, p.Location); p = child with { MinOccurs = BigInteger.Zero }; }
                else p = child;
            }
            else break;
        }
        return p?.MaxOccurs == BigInteger.Zero ? null : p;
    }
    private IReadOnlyList<XsdParticle> Flatten(XsdGroupTerm group)
    {
        var result = new List<XsdParticle>(); var pending = new Stack<XsdParticle>();
        for (int i = group.Particles.Count - 1; i >= 0; i--) { ContentWork.Retain(op, group.Particles[i].Location); pending.Push(group.Particles[i]); }
        while (pending.TryPop(out var item))
        {
            ContentWork.Tick(op, item.Location); var p = Normalize(item); if (p is null) continue;
            if (p.MinOccurs.IsOne && p.MaxOccurs is { IsOne: true } && p.Term is XsdGroupTerm inner && inner.Compositor == group.Compositor && group.Compositor != XsdCompositor.All)
                for (int i = inner.Particles.Count - 1; i >= 0; i--) { ContentWork.Retain(op, p.Location); pending.Push(inner.Particles[i]); }
            else { ContentWork.Retain(op, p.Location); result.Add(p); }
        }
        return result;
    }
    private bool Range(BigInteger rmin, BigInteger? rmax, BigInteger bmin, BigInteger? bmax, XmlLocation at)
    {
        ContentWork.Integer(op, at, rmin); ContentWork.Integer(op, at, bmin);
        if (rmax is { } r) ContentWork.Integer(op, at, r); if (bmax is { } b) ContentWork.Integer(op, at, b);
        return ContentIntegers.Compare(rmin, bmin, op, at) >= 0 && (bmax is null || rmax is { } limit && ContentIntegers.Compare(limit, bmax.Value, op, at) <= 0);
    }
    private readonly HashSet<XsdParticle> implicitMembers = new(ReferenceEqualityComparer.Instance);
    private XsdParticle ExpandSubstitution(XsdParticle p)
    {
        if (p.Term is not XsdElementTerm e || implicitMembers.Contains(p)) return p;
        var members = Members(e.DeclarationId);
        if (members.Length <= 1 && (members.Length == 0 || members[0].Item2 == e.DeclarationId)) return p;
        ContentWork.Retain(op, p.Location, checked(members.Length * 2 + 2));
        var particles = new XsdParticle[members.Length];
        for (int i = 0; i < members.Length; i++) { ContentWork.Tick(op, p.Location); particles[i] = new(new XsdElementTerm(members[i].Item2), 1, 1, p.Location); ContentWork.Retain(op, p.Location); implicitMembers.Add(particles[i]); }
        return p with { Term = new XsdGroupTerm(XsdCompositor.Choice, particles) };
    }
    private enum RuleKind { Relation, Constant, And, Or }
    private enum RelationState { False, Indeterminate, True }
    private sealed class Rule(RuleKind kind, RelationState value, XsdParticle? derived, XsdParticle? basis, bool anyType, bool synthesized, Rule? left, Rule? right)
    {
        internal readonly RuleKind Kind = kind;
        internal readonly RelationState Value = value;
        internal readonly bool AnyType = anyType, Synthesized = synthesized;
        internal readonly XsdParticle? Derived = derived, Basis = basis;
        internal readonly Rule? Left = left, Right = right;
        internal Rule? Expanded;
    }
    private Rule Constant(bool value) { ContentWork.Retain(op, default); return new(RuleKind.Constant, value ? RelationState.True : RelationState.False, null, null, false, false, null, null); }
    private Rule Relation(XsdParticle? d, XsdParticle? b, bool anyType, bool synthesized = false)
    { ContentWork.Retain(op, d?.Location ?? default); return new(RuleKind.Relation, RelationState.False, d, b, anyType, synthesized, null, null); }
    private Rule Combine(RuleKind kind, Rule a, Rule b)
    { ContentWork.Retain(op, default, 3); return new(kind, RelationState.False, null, null, false, false, a, b); }
    private bool Restricts(XsdParticle? derived, XsdParticle? basis, bool anyType)
        => EvaluateRule(Relation(derived, basis, anyType)) != RelationState.False;
    private RelationState EvaluateRule(Rule root)
    {
        var results = new Dictionary<Rule, RelationState>();
        var stack = new Stack<(Rule Rule, bool Ready)>(); ContentWork.Retain(op, root.Derived?.Location ?? default); stack.Push((root, false));
        while (stack.TryPop(out var frame))
        {
            var rule = frame.Rule; ContentWork.Tick(op, rule.Derived?.Location ?? default);
            if (results.ContainsKey(rule)) continue;
            if (rule.Kind == RuleKind.Constant) { ContentWork.Retain(op, default); results.Add(rule, rule.Value); continue; }
            if (!frame.Ready)
            {
                ContentWork.Retain(op, default); stack.Push((rule, true));
                if (rule.Kind == RuleKind.Relation)
                { rule.Expanded = ExpandRule(rule); ContentWork.Retain(op, default); stack.Push((rule.Expanded, false)); }
                else
                { ContentWork.Retain(op, default, 2); stack.Push((rule.Right!, false)); stack.Push((rule.Left!, false)); }
                continue;
            }
            var result = rule.Kind == RuleKind.Relation ? results[rule.Expanded!] : rule.Kind == RuleKind.And
                ? (RelationState)Math.Min((int)results[rule.Left!], (int)results[rule.Right!])
                : (RelationState)Math.Max((int)results[rule.Left!], (int)results[rule.Right!]);
            ContentWork.Retain(op, default); results.Add(rule, result);
        }
        return results[root];
    }
    private Rule ExpandRule(Rule rule)
    {
        var derived = rule.Synthesized ? rule.Derived : Normalize(rule.Derived); var basis = Normalize(rule.Basis); bool anyType = rule.AnyType;
        if (derived is null) return Constant(Nullable(basis)); if (basis is null) return Constant(IsPointlessEmpty(derived));
        ContentWork.Tick(op, derived.Location);
        if (SameParticle(derived, basis)) return Constant(true);
        derived = ExpandSubstitution(derived); basis = ExpandSubstitution(basis);
        // Expansion can introduce a pointless singleton choice. Normalize it
        // before selecting the relation case, preserving non-unit wrappers and
        // the RecurseAsIfGroup wrapper that deliberately prevents redispatch.
        if (!rule.Synthesized) derived = Normalize(derived);
        basis = Normalize(basis);
        if (derived is null) return Constant(Nullable(basis)); if (basis is null) return Constant(IsPointlessEmpty(derived));
        // Known implicit choices must be expanded before comparing leaf
        // declarations: ref h can admit a known m that a local h never accepts.
        if (derived.Term is XsdElementTerm directD && basis.Term is XsdElementTerm directB)
            return ElementRestriction(derived, basis, directD.DeclarationId, directB.DeclarationId);
        if (basis.Term is XsdWildcardTerm bw)
        {
            if (derived.Term is XsdElementTerm elt)
                return Constant(ContentNamespaces.Allows(bw.Wildcard, schema.Elements[elt.DeclarationId].Name.NamespaceUri, op, derived.Location)
                    && Range(derived.MinOccurs, derived.MaxOccurs, basis.MinOccurs, basis.MaxOccurs, derived.Location));
            if (derived.Term is XsdWildcardTerm dw)
                return Constant(Range(derived.MinOccurs, derived.MaxOccurs, basis.MinOccurs, basis.MaxOccurs, derived.Location)
                    && ContentNamespaces.Subset(dw.Wildcard, bw.Wildcard, op, derived.Location)
                    && (anyType || dw.Wildcard.ProcessContents <= bw.Wildcard.ProcessContents));
            if (derived.Term is XsdGroupTerm dg)
            {
                var total = Total(derived);
                if (!Range(total.Min, total.Max, basis.MinOccurs, basis.MaxOccurs, derived.Location)) return Constant(false);
                var result = Constant(true);
                foreach (var child in dg.Particles) result = Combine(RuleKind.And, result, Relation(child, basis, anyType));
                return result;
            }
        }
        if (basis.Term is not XsdGroupTerm bg || derived.Term is XsdWildcardTerm) return Constant(false);
        if (derived.Term is XsdElementTerm)
        { ContentWork.Retain(op, derived.Location, 3); return Relation(new(new XsdGroupTerm(bg.Compositor, [derived]), 1, 1, derived.Location), basis, anyType, true); }
        if (derived.Term is not XsdGroupTerm rg) return Constant(false);
        var rs = Flatten(rg); var bs = Flatten(bg);
        if (rg.Compositor == XsdCompositor.Sequence && bg.Compositor == XsdCompositor.Choice)
        {
            if (!Range(Multiply(derived.MinOccurs, rs.Count, derived.Location), derived.MaxOccurs is { } m ? Multiply(m, rs.Count, derived.Location) : null, basis.MinOccurs, basis.MaxOccurs, derived.Location)) return Constant(false);
            var result = Constant(true);
            foreach (var r in rs)
            { var match = Constant(false); foreach (var b in bs) match = Combine(RuleKind.Or, match, Relation(r, b, anyType)); result = Combine(RuleKind.And, result, match); }
            return result;
        }
        if (!Range(derived.MinOccurs, derived.MaxOccurs, basis.MinOccurs, basis.MaxOccurs, derived.Location)) return Constant(false);
        bool ordered = rg.Compositor == bg.Compositor && rg.Compositor is XsdCompositor.Sequence or XsdCompositor.All or XsdCompositor.Choice;
        bool unordered = rg.Compositor == XsdCompositor.Sequence && bg.Compositor == XsdCompositor.All;
        if (!ordered && !unordered) return Constant(false);
        if (unordered)
        {
            if (availability?.Unavailable(consumer) == true) return UnorderedPartial(rs, bs, anyType, derived.Location);
            // All's uniquely attributed element children make the name mapping
            // unique. Build a name-based reordering, then use ordered Recurse.
            ContentWork.Retain(op, derived.Location, checked(bs.Count + rs.Count));
            var reordered = new XsdParticle?[bs.Count];
            foreach (var r in rs)
            {
                var n = Normalize(r); if (n?.Term is not XsdElementTerm e) return Constant(false);
                int found = -1; var name = schema.Elements[e.DeclarationId].Name;
                for (int i = 0; i < bs.Count; i++)
                {
                    ContentWork.Tick(op, r.Location);
                    if (bs[i].Term is XsdElementTerm b)
                    {
                        bool match = schema.Elements[b.DeclarationId].Name == name;
                        foreach (var member in Members(b.DeclarationId)) { ContentWork.Tick(op, r.Location); match |= member.Item1 == name; }
                        if (match) { found = i; break; }
                    }
                }
                if (found < 0 || reordered[found] is not null) return Constant(false); reordered[found] = r;
            }
            var result = Constant(true);
            for (int i = 0; i < bs.Count; i++) result = Combine(RuleKind.And, result, reordered[i] is { } r ? Relation(r, bs[i], anyType) : Constant(Nullable(bs[i])));
            return result;
        }
        bool lax = rg.Compositor == XsdCompositor.Choice;
        ContentWork.Retain(op, derived.Location, checked((bs.Count + 1) * 2));
        var previous = new Rule[bs.Count + 1]; var next = new Rule[bs.Count + 1]; previous[0] = Constant(true);
        for (int j = 1; j <= bs.Count; j++) previous[j] = Combine(RuleKind.And, previous[j - 1], Constant(lax || Nullable(bs[j - 1])));
        foreach (var r in rs)
        {
            next[0] = Constant(false);
            for (int j = 1; j <= bs.Count; j++)
                next[j] = Combine(RuleKind.Or, Combine(RuleKind.And, previous[j - 1], Relation(r, bs[j - 1], anyType)),
                    Combine(RuleKind.And, next[j - 1], Constant(lax || Nullable(bs[j - 1]))));
            (previous, next) = (next, previous);
        }
        return previous[bs.Count];
    }
    private Rule State(RelationState value)
    { ContentWork.Retain(op, default); return new(RuleKind.Constant, value, null, null, false, false, null, null); }
    private Rule UnorderedPartial(IReadOnlyList<XsdParticle> derived, IReadOnlyList<XsdParticle> basis, bool anyType, XmlLocation at)
    {
        int count = basis.Count;
        if (derived.Count > count) return Constant(false);
        // This branch contains element children only. Expanded substitution
        // choices use the existing iterative relation solver for each candidate.
        foreach (var particle in derived)
        { ContentWork.Tick(op, at); if (Normalize(particle)?.Term is not XsdElementTerm) return Constant(false); }
        long retained = (long)count * count + 7L * count + 10;
        if (retained > int.MaxValue)
        { op.Refuse("SCHEMA.Limit", "Unordered relation representation exceeded.", at); throw SchemaSnapshots.Failure(op); }
        ContentWork.Retain(op, at, (int)retained);
        var relations = new RelationState[count * count]; var optional = new bool[count];
        for (int j = 0; j < count; j++) { ContentWork.Tick(op, at); optional[j] = Nullable(basis[j]); }
        for (int i = 0; i < derived.Count; i++) for (int j = 0; j < count; j++)
        { ContentWork.Tick(op, at); relations[i * count + j] = EvaluateRule(Relation(derived[i], basis[j], anyType)); }
        var leftMatch = new int[count]; var rightMatch = new int[count]; var queue = new int[count];
        var seenLeft = new bool[count]; var seenRight = new bool[count]; var parentRight = new int[count];
        bool Matches(bool allowIndeterminate)
        {
            for (int i = 0; i < count; i++) { ContentWork.Tick(op, at); leftMatch[i] = rightMatch[i] = -1; }
            // Dummy left nodes match only optional base particles. A perfect
            // injective assignment therefore covers every required base child.
            for (int root = 0; root < count; root++)
            {
                for (int j = 0; j < count; j++) { ContentWork.Tick(op, at); seenLeft[j] = seenRight[j] = false; }
                int first = 0, last = 1, free = -1; queue[0] = root; seenLeft[root] = true;
                while (first < last && free < 0)
                {
                    int left = queue[first++]; ContentWork.Tick(op, at);
                    for (int right = 0; right < count; right++)
                    {
                        ContentWork.Tick(op, at);
                        var state = left < derived.Count ? relations[left * count + right] : optional[right] ? RelationState.True : RelationState.False;
                        // Definite-false candidates never create traversal states.
                        if (seenRight[right] || state == RelationState.False || !allowIndeterminate && state != RelationState.True) continue;
                        seenRight[right] = true; parentRight[right] = left;
                        int next = rightMatch[right];
                        if (next < 0) { free = right; break; }
                        if (!seenLeft[next]) { seenLeft[next] = true; queue[last++] = next; }
                    }
                }
                if (free < 0) return false;
                while (free >= 0)
                {
                    ContentWork.Tick(op, at); int left = parentRight[free], previous = leftMatch[left];
                    leftMatch[left] = free; rightMatch[free] = left; free = previous;
                }
            }
            return true;
        }
        if (Matches(false)) return State(RelationState.True);
        return State(Matches(true) ? RelationState.Indeterminate : RelationState.False);
    }
    private Rule Indeterminate(XsdElementDeclaration operand, DependencyUse operandUse = DependencyUse.TypeAssessment)
    {
        if (availability is null) throw new ArgumentException("Complete restriction operands required.", nameof(schema));
        availability.Link(consumer, ComponentAvailability.Element(operand), DependencyUse.TypeAssessment, operandUse);
        ContentWork.Retain(op, operand.Location);
        return new(RuleKind.Constant, RelationState.Indeterminate, null, null, false, false, null, null);
    }
    private Rule ElementRestriction(XsdParticle derived, XsdParticle basis, int derivedId, int basisId)
    {
        var d = schema.Elements[derivedId]; var b = schema.Elements[basisId];
        if (!Range(derived.MinOccurs, derived.MaxOccurs, basis.MinOccurs, basis.MaxOccurs, derived.Location)) return Constant(false);
        bool globalD = schema.GlobalElements.TryGetValue(d.Name, out int did) && did == d.Id;
        bool globalB = schema.GlobalElements.TryGetValue(b.Name, out int bid) && bid == b.Id;
        Rule nameRelation = Constant(true), typeRelation = Constant(true);
        if (d.Name != b.Name)
        {
            if (!globalD || !globalB || b.Block.Contains(XsdElementBlock.Substitution)) return Constant(false);
            int current = d.Id; bool reaches = false;
            for (int steps = 0; steps < schema.Elements.Count; steps++)
            {
                var step = schema.Elements[current]; ContentWork.Tick(op, step.Location);
                if (step.SubstitutionGroup is not { } head) return Constant(false);
                if (!schema.GlobalElements.TryGetValue(head, out int next))
                { nameRelation = Indeterminate(step, DependencyUse.SubstitutionSelection); reaches = true; break; }
                current = next; if (current == b.Id) { reaches = true; break; }
            }
            if (!reaches) return Constant(false);
            if (d.Type is { } dt && b.Type is { } bt && types.ContainsKey(dt) && types.ContainsKey(bt))
            { if (!SubstitutionType(dt, b)) return Constant(false); }
            else
            {
                if (d.Type is not { } dname || !types.ContainsKey(dname)) typeRelation = Combine(RuleKind.And, typeRelation, Indeterminate(d));
                if (b.Type is not { } bname || !types.ContainsKey(bname)) typeRelation = Combine(RuleKind.And, typeRelation, Indeterminate(b));
            }
        }
        if (globalD && globalB) return Combine(RuleKind.And, nameRelation, typeRelation);
        if (d.Nillable && !b.Nillable || b.FixedValue is not null && d.FixedValue is null) return Constant(false);
        if (d.Type is { } knownD && b.Type is { } knownB && types.ContainsKey(knownD) && types.ContainsKey(knownB))
        { if (!TypeDerives(knownD, knownB, true)) return Constant(false); }
        else
        {
            if (d.Type is not { } dname || !types.ContainsKey(dname)) typeRelation = Combine(RuleKind.And, typeRelation, Indeterminate(d));
            if (b.Type is not { } bname || !types.ContainsKey(bname)) typeRelation = Combine(RuleKind.And, typeRelation, Indeterminate(b));
        }
        foreach (var block in b.Block) { ContentWork.Tick(op, derived.Location); if (!d.Block.Contains(block)) return Constant(false); }
        foreach (int identity in d.IdentityConstraints) { ContentWork.Tick(op, derived.Location); if (!b.IdentityConstraints.Contains(identity)) return Constant(false); }
        if (b.FixedValue is not null && facets is not null && !FixedEquals(d, b)) return Constant(false);
        return Combine(RuleKind.And, nameRelation, typeRelation);
    }
    private bool FixedEquals(XsdElementDeclaration derived, XsdElementDeclaration basis)
    {
        if (availability is not null && (availability.Unavailable(ComponentAvailability.Element(derived)) || availability.Unavailable(ComponentAvailability.Element(basis))))
        {
            availability.Link(consumer, ComponentAvailability.Element(derived)); availability.Link(consumer, ComponentAvailability.Element(basis)); return true;
        }
        return FixedEquals(FixedValue(derived), FixedValue(basis), derived.Location);
    }
    private bool FixedEquals(XsdAttributeBinding derived, XsdAttributeBinding basis)
    {
        var d = ComponentAvailability.Simple(schema.Attributes[derived.DeclarationId].Type);
        var b = ComponentAvailability.Simple(schema.Attributes[basis.DeclarationId].Type);
        if (availability is not null && (availability.Unavailable(d) || availability.Unavailable(b)))
        { availability.Link(consumer, d); availability.Link(consumer, b); return true; }
        return FixedEquals(FixedValue(derived), FixedValue(basis), derived.Location);
    }
    private bool FixedEquals(XmlDatatypeValue left, XmlDatatypeValue right, XmlLocation location)
    {
        var library = schema.Datatypes.Find(Xs)
            ?? throw new ArgumentException("Complete composition requires an executable XSD datatype library.");
        ContentWork.Tick(op, location);
        bool equal = library.ValueEquals(left, right, op);
        ContentWork.Tick(op, location, 0);
        return equal;
    }
    private XmlDatatypeValue FixedValue(XsdAttributeBinding use)
    {
        var declaration = schema.Attributes[use.DeclarationId];
        bool local = use.FixedValue is not null;
        var location = local ? use.Location : declaration.Location;
        ContentWork.Tick(op, location);
        var definition = schema.Datatypes.FindType(declaration.Type)
            ?? throw new ArgumentException("Complete composition requires the declaration's executable datatype.");
        // A local override owns its lexical/context pair; an inherited
        // declaration constraint keeps the declaration's authored map.
        bool valid = facets!.TryValidate(definition,
            (local ? use.FixedValue : declaration.FixedValue).AsSpan(),
            local ? use.Context : declaration.Context, schema.Datatypes, op, out var value, out var error);
        ContentWork.Tick(op, location, 0);
        if (!valid || value is null)
            ContentWork.Error(op, location, error?.Message ?? "Fixed constraint does not satisfy its declaration datatype.");
        return value!;
    }
    private XmlDatatypeValue FixedValue(XsdElementDeclaration declaration)
    {
        ContentWork.Tick(op, declaration.Location);
        // Simple-content complex types use their effective simple type. A
        // constrained emptiable mixed type has the string value space.
        var type = types[declaration.Type!.Value];
        var name = type.SimpleType ?? new SchemaName(Xs, "string");
        var definition = schema.Datatypes.FindType(name)
            ?? throw new ArgumentException("Complete composition requires the declaration's executable datatype.");
        bool valid = facets!.TryValidate(definition, declaration.FixedValue.AsSpan(), declaration.Context,
            schema.Datatypes, op, out var value, out var error);
        ContentWork.Tick(op, declaration.Location, 0);
        if (!valid || value is null)
            ContentWork.Error(op, declaration.Location, error?.Message ?? "Fixed constraint does not satisfy its declaration datatype.");
        return value!;
    }
    private bool EqualMaximum(BigInteger? a, BigInteger? b, XmlLocation at)
        => a is null ? b is null : b is { } value && ContentIntegers.Compare(a.Value, value, op, at) == 0;
    private readonly Dictionary<XsdParticle, (BigInteger Min, BigInteger? Max)> totals = new(ReferenceEqualityComparer.Instance);
    private (BigInteger Min, BigInteger? Max) Total(XsdParticle p)
    {
        var pending = new Stack<(XsdParticle Particle, bool Ready)>(); ContentWork.Retain(op, p.Location); pending.Push((p, false));
        while (pending.TryPop(out var item))
        {
            var particle = item.Particle; ContentWork.Tick(op, particle.Location); if (totals.ContainsKey(particle)) continue;
            BigInteger min = 1; BigInteger? max = 1;
            if (particle.Term is XsdGroupTerm group)
            {
                if (!item.Ready)
                {
                    ContentWork.Retain(op, particle.Location); pending.Push((particle, true));
                    foreach (var child in group.Particles) { ContentWork.Retain(op, child.Location); pending.Push((child, false)); }
                    continue;
                }
                min = 0; max = 0; bool first = true;
                foreach (var child in group.Particles)
                {
                    ContentWork.Tick(op, child.Location); var range = totals[child];
                    if (group.Compositor == XsdCompositor.Choice)
                    {
                        if (first || ContentIntegers.Compare(range.Min, min, op, child.Location) < 0) min = range.Min;
                        if (first) max = range.Max; else if (max is null || range.Max is null) max = null;
                        else if (ContentIntegers.Compare(range.Max.Value, max.Value, op, child.Location) > 0) max = range.Max;
                    }
                    else { min = ContentIntegers.Add(min, range.Min, op, particle.Location); max = max is { } a && range.Max is { } b ? ContentIntegers.Add(a, b, op, particle.Location) : null; }
                    first = false;
                }
            }
            var total = (Multiply(min, particle.MinOccurs, particle.Location),
                max == BigInteger.Zero || particle.MaxOccurs == BigInteger.Zero ? BigInteger.Zero : max is { } x && particle.MaxOccurs is { } y ? Multiply(x, y, particle.Location) : (BigInteger?)null);
            ContentWork.Retain(op, particle.Location); totals.Add(particle, total);
        }
        return totals[p];
    }
    private BigInteger Multiply(BigInteger a, BigInteger b, XmlLocation at) => ContentIntegers.Multiply(a, b, op, at);
    private void CheckExtensionHistory(XsdTypeDefinition type)
    {
        // §3.4.6 Extension 1.5: construct the extension-first intermediate,
        // then check the final definition as a restriction of that intermediate.
        var chain = new List<XsdTypeDefinition>(); var cursor = type; bool restriction = false;
        while (true)
        {
            ContentWork.Retain(op, cursor.Location); chain.Add(cursor);
            if (cursor.BaseType is not { } parent || parent == Any) break;
            cursor = types[parent]; restriction |= cursor.Derivation == XsdDerivation.Restriction && cursor.SimpleType != cursor.Name;
        }
        if (!restriction || chain.Count < 3) return;
        var anchor = chain[^1]; var particle = anchor.Particle; var attributes = Uses(anchor); var wildcard = anchor.AnyAttribute;
        for (int index = chain.Count - 2; index >= 0; index--)
        {
            var step = chain[index]; ContentWork.Tick(op, step.Location);
            if (step.Derivation != XsdDerivation.Extension) continue;
            var parent = chain[index + 1]; XsdParticle? addition = null;
            if (parent.Particle is null) addition = step.Particle;
            else if (!SameParticle(step.Particle, parent.Particle))
            {
                var group = (XsdGroupTerm)step.Particle!.Term;
                ContentWork.Retain(op, step.Location, group.Particles.Count);
                var suffix = new XsdParticle[group.Particles.Count - 1];
                for (int i = 1; i < group.Particles.Count; i++) { ContentWork.Tick(op, step.Location); suffix[i - 1] = group.Particles[i]; }
                if (suffix.Length == 1) addition = suffix[0];
                else if (suffix.Length > 1) { ContentWork.Retain(op, step.Location, 2); addition = new(new XsdGroupTerm(XsdCompositor.Sequence, suffix), 1, 1, step.Location); }
            }
            if (addition is not null)
            {
                if (particle is { Term: XsdGroupTerm { Compositor: XsdCompositor.All } } || addition.Term is XsdGroupTerm { Compositor: XsdCompositor.All } && particle is not null)
                    ContentWork.Error(op, step.Location, "Extension history cannot extend an all particle after moving restrictions.");
                if (particle is null) particle = addition;
                else { ContentWork.Retain(op, step.Location, 4); particle = new(new XsdGroupTerm(XsdCompositor.Sequence, [particle, addition]), 1, 1, step.Location); }
            }
            var before = Uses(parent);
            foreach (var pair in Uses(step))
            {
                ContentWork.Tick(op, pair.Value.Location); if (before.ContainsKey(pair.Key)) continue;
                if (attributes.TryGetValue(pair.Key, out var old))
                {
                    if (schema.Attributes[old.DeclarationId].Type != schema.Attributes[pair.Value.DeclarationId].Type)
                        ContentWork.Error(op, step.Location, "Extension-first intermediate changes an inherited attribute declaration type.");
                }
                else { ContentWork.Retain(op, step.Location); attributes.Add(pair.Key, pair.Value); }
            }
            if (step.AnyAttribute is { } extra && !ReferenceEquals(extra, parent.AnyAttribute))
                wildcard = wildcard is null ? extra : UnionNamespaces(wildcard, extra, step.Location);
        }
        ContentWork.Retain(op, type.Location, attributes.Count + 1);
        var uses = new XsdAttributeBinding[attributes.Count]; int offset = 0;
        foreach (var use in attributes.Values) { ContentWork.Tick(op, type.Location); uses[offset++] = use; }
        var intermediate = anchor with { Particle = particle, Attributes = uses, AnyAttribute = wildcard,
            ContentKind = particle is null ? anchor.ContentKind : anchor.ContentKind == XsdContentKind.Mixed ? XsdContentKind.Mixed : XsdContentKind.ElementOnly };
        _ = Build(intermediate);
        ContentWork.Retain(op, type.Location); CheckAttributes(type with { Derivation = XsdDerivation.Restriction }, intermediate);
        bool valid = RestrictionContent(type, intermediate);
        if (!valid) ContentWork.Error(op, type.Location, "Extension history cannot be expressed as extension followed by restriction.");
    }
    private XsdWildcard UnionNamespaces(XsdWildcard a, XsdWildcard b, XmlLocation at)
    {
        var result = new List<string>();
        if (a.Negated && b.Negated)
        {
            foreach (string ns in a.Namespaces) if (!ContentNamespaces.Allows(b, ns, op, at)) { ContentWork.Retain(op, at); result.Add(ns); }
        }
        else if (a.Negated || b.Negated)
        {
            var negative = a.Negated ? a : b; var positive = a.Negated ? b : a;
            foreach (string ns in negative.Namespaces) if (!ContentNamespaces.Allows(positive, ns, op, at)) { ContentWork.Retain(op, at); result.Add(ns); }
        }
        else
        {
            foreach (string ns in a.Namespaces) { ContentWork.Retain(op, at); result.Add(ns); }
            foreach (string ns in b.Namespaces)
            {
                bool contains = false; foreach (string old in result) { ContentWork.Tick(op, at); contains |= StringComparer.Ordinal.Equals(old, ns); }
                if (!contains) { ContentWork.Retain(op, at); result.Add(ns); }
            }
        }
        ContentWork.Retain(op, at, result.Count + 1);
        var union = new XsdWildcard(a.Negated || b.Negated, result, b.ProcessContents);
        ContentNamespaces.Validate(union, op, at); return union;
    }
}

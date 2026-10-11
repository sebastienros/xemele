using Xemele.Parsing.Dtd;

namespace Xemele.Validation;

internal static class DtdCompiler
{
    internal static CompiledDtdModel Compile(DtdModel source, ValidationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(operation);
        operation.CancellationToken.ThrowIfCancellationRequested();
        var budget = new CompilationBudget(operation);
        var elements = new List<CompiledDtdElement>();
        var attributes = new List<CompiledDtdAttribute>();
        var entityTable = new Dictionary<string, DtdEntityDeclaration>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var notation in source.Notations)
        {
            budget.Retain(1, notation.Location); Name(notation.Name, true, notation.Location);
            if (!names.Add(notation.Name)) Error("DTD.DuplicateNotation", "Notation must be declared once.", notation.Location);
        }
        names.Clear();
        foreach (var entity in source.GeneralEntities)
        {
            budget.Retain(1, entity.Location); Name(entity.Name, true, entity.Location);
            // M2 already applies first-binding precedence. Do not second-merge.
            if (!entityTable.TryAdd(entity.Name, entity)) Error("DTD.InvalidDeclaration", "Effective model contains duplicate entities.", entity.Location);
            if (entity.NotationName is { } notation && !HasNotation(notation, entity.Location))
                Error("DTD.UndeclaredNotation", "Unparsed entity notation is undeclared.", entity.Location);
        }
        foreach (var declaration in source.Elements)
        {
            budget.Retain(1, declaration.Location); Name(declaration.Name, false, declaration.Location);
            if (!names.Add(declaration.Name)) Error("DTD.DuplicateElement", "Element must be declared once.", declaration.Location);
            elements.Add(new(declaration, CompileContent(declaration, budget)));
        }
        var model = new CompiledDtdModel(source, elements, attributes);
        var attributeNames = new HashSet<(string, string)>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var notationAttributes = new HashSet<string>(StringComparer.Ordinal);
        var expansion = new DefaultBudget(operation);
        foreach (var declaration in source.Attributes)
        {
            var at = declaration.Location;
            budget.Retain(1, at); Name(declaration.ElementName, false, at); Name(declaration.Name, false, at);
            if (!Enum.IsDefined(declaration.Type) || !Enum.IsDefined(declaration.DefaultKind)) Error("DTD.InvalidDeclaration", "Invalid attribute kind.", at);
            if (!attributeNames.Add((declaration.ElementName, declaration.Name))) Error("DTD.InvalidDeclaration", "Effective model contains duplicate attributes.", at);
            if (declaration.Type == DtdAttributeType.Id)
            {
                if (!ids.Add(declaration.ElementName)) Error("DTD.MultipleIdAttributes", "An element has more than one ID attribute.", at);
                if (declaration.DefaultKind is not (DtdDefaultKind.Implied or DtdDefaultKind.Required))
                    Error("DTD.IdDefault", "ID attributes require IMPLIED or REQUIRED.", at);
            }
            if (declaration.Type == DtdAttributeType.Notation)
            {
                if (!notationAttributes.Add(declaration.ElementName)) Error("DTD.MultipleNotationAttributes", "An element has more than one NOTATION attribute.", at);
                var element = model.FindElement(declaration.ElementName, operation); budget.Check();
                if (element?.Content.Kind == DtdParticleKind.Empty) Error("DTD.NotationOnEmpty", "EMPTY elements cannot have NOTATION attributes.", at);
            }
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (string token in declaration.Tokens)
            {
                budget.Retain(1, at);
                if (!DtdLexical.Name(token, declaration.Type == DtdAttributeType.Enumeration, declaration.Type == DtdAttributeType.Notation, operation, at))
                { budget.Check(); Error("DTD.InvalidDeclaration", "Invalid enumeration token.", at); }
                if (!tokens.Add(token)) Error("DTD.DuplicateToken", "Enumeration tokens must be distinct.", at);
                if (declaration.Type == DtdAttributeType.Notation && !HasNotation(token, at))
                    Error("DTD.UndeclaredNotation", "NOTATION token is undeclared.", at);
            }
            bool enumerated = declaration.Type is DtdAttributeType.Enumeration or DtdAttributeType.Notation;
            if (enumerated != (declaration.Tokens.Count != 0)) Error("DTD.InvalidDeclaration", "Invalid attribute token list.", at);
            bool hasDefault = declaration.DefaultKind is DtdDefaultKind.Fixed or DtdDefaultKind.Value;
            if (hasDefault != (declaration.DefaultValue is not null)) Error("DTD.InvalidDeclaration", "Invalid attribute default shape.", at);
            string? normalized = null;
            if (declaration.DefaultValue is { } value)
            {
                budget.Work(value.Length, at);
                // The existing value processor owns normalization/PE-origin semantics.
                var result = DtdValueProcessor.NormalizeAttributeDefault(value, declaration.Type, new BudgetedEntities(entityTable, budget, at),
                    operation.Limits, at, expansion, operation.CancellationToken);
                budget.Check();
                if (result.Diagnostic is { } diagnostic)
                {
                    if (diagnostic.Code == "XML.ResourceLimit") operation.Refuse("VALIDATION.Limit", diagnostic.Message, diagnostic.Location);
                    budget.Check(); throw new XmlParseException([diagnostic]);
                }
                normalized = result.Value!; budget.Retain(normalized.Length, at);
            }
            var attribute = new CompiledDtdAttribute(declaration, normalized);
            // XML 3.3.2 requires default syntax, not unused entity membership.
            if (normalized is not null && !attribute.IsValueValid(normalized, model, operation, at, requireEntityMembership: false))
            { budget.Check(); Error("DTD.InvalidDefault", "Attribute default does not satisfy its declared type.", at); }
            attributes.Add(attribute);
        }
        budget.Check();
        return new(source, elements, attributes);

        void Name(string name, bool ncName, XmlLocation at)
        {
            budget.Work(1, at);
            if (!DtdLexical.Name(name, false, ncName, operation, at)) { budget.Check(); Error("DTD.InvalidDeclaration", "Invalid declaration name.", at); }
        }
        bool HasNotation(string name, XmlLocation at)
        {
            foreach (var notation in source.Notations)
            { if (DtdLexical.Equal(name, notation.Name, operation)) return true; budget.Check(); }
            return false;
        }
    }

    private sealed class Info(bool nullable, HashSet<int> first, HashSet<int> last)
    { internal bool Nullable = nullable; internal readonly HashSet<int> First = first, Last = last; }
    private readonly record struct Visit(DtdParticle Particle, int Depth, bool Finish);
    private static DtdContentModel CompileContent(DtdElementDeclaration declaration, CompilationBudget budget)
    {
        var root = declaration.Content; var at = declaration.Location;
        if (root.Kind is DtdParticleKind.Empty or DtdParticleKind.Any)
        {
            if (root.Name is not null || root.Children.Count != 0 || root.Occurrence != DtdOccurrence.Once)
                Error("DTD.InvalidDeclaration", "Invalid EMPTY/ANY particle.", at);
            budget.Retain(1, at); return new(root.Kind, [], [[]], [true]);
        }
        if (root.Kind == DtdParticleKind.Mixed)
        {
            if (root.Name is not null || (root.Children.Count > 0 ? root.Occurrence != DtdOccurrence.ZeroOrMore : root.Occurrence is not (DtdOccurrence.Once or DtdOccurrence.ZeroOrMore)))
                Error("DTD.InvalidDeclaration", "Invalid mixed content shape.", at);
            var mixedNames = new List<string>(); var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in root.Children)
            {
                budget.Retain(3, at);
                if (child.Kind != DtdParticleKind.Name || child.Occurrence != DtdOccurrence.Once || child.Children.Count != 0 ||
                    child.Name is null || !DtdLexical.Name(child.Name, false, false, budget.Operation, at))
                { budget.Check(); Error("DTD.InvalidDeclaration", "Invalid mixed content name.", at); }
                if (!distinct.Add(child.Name!)) Error("DTD.DuplicateMixedName", "Mixed content names must be distinct.", at);
                mixedNames.Add(child.Name!);
            }
            int[] choices = new int[mixedNames.Count];
            for (int i = 0; i < choices.Length; i++) { budget.Work(1, at); choices[i] = i + 1; }
            return new(root.Kind, mixedNames.ToArray(), [choices], [true]);
        }
        var positions = new List<string>();
        var follow = new List<HashSet<int>> { new() };
        var infos = new Stack<Info>();
        var visits = new Stack<Visit>(); visits.Push(new(root, 1, false));
        while (visits.Count != 0)
        {
            var visit = visits.Pop(); var particle = visit.Particle;
            budget.Work(1, at);
            if (visit.Depth > (budget.Operation.Limits.Depth ?? 4096) || visit.Depth > 4096)
            { budget.Operation.Refuse("VALIDATION.Limit", "Content model depth limit exceeded.", at); budget.Check(); }
            if (!Enum.IsDefined(particle.Occurrence)) Error("DTD.InvalidDeclaration", "Invalid occurrence.", at);
            Info info;
            if (!visit.Finish)
            {
                budget.Retain(3, at);
                if (particle.Kind == DtdParticleKind.Name)
                {
                    if (particle.Name is null || particle.Children.Count != 0 || !DtdLexical.Name(particle.Name, false, false, budget.Operation, at))
                    { budget.Check(); Error("DTD.InvalidDeclaration", "Invalid content name.", at); }
                    positions.Add(particle.Name!); follow.Add([]);
                    info = new(false, [positions.Count], [positions.Count]);
                }
                else
                {
                    if (particle.Kind is not (DtdParticleKind.Sequence or DtdParticleKind.Choice) || particle.Name is not null || particle.Children.Count == 0)
                        Error("DTD.InvalidDeclaration", "Invalid children content model.", at);
                    budget.Retain(particle.Children.Count, at);
                    visits.Push(visit with { Finish = true });
                    for (int i = particle.Children.Count - 1; i >= 0; i--)
                    { budget.Work(1, at); visits.Push(new(particle.Children[i], visit.Depth + 1, false)); }
                    continue;
                }
            }
            else
            {
                var children = new Info[particle.Children.Count];
                for (int i = children.Length - 1; i >= 0; i--) { budget.Work(1, at); children[i] = infos.Pop(); }
                info = children[0];
                for (int i = 1; i < children.Length; i++)
                {
                    budget.Work(1, at); var right = children[i];
                    if (particle.Kind == DtdParticleKind.Choice)
                    { Union(info.First, right.First); Union(info.Last, right.Last); info.Nullable |= right.Nullable; }
                    else
                    {
                        Edges(info.Last, right.First);
                        if (info.Nullable) Union(info.First, right.First);
                        if (right.Nullable) Union(info.Last, right.Last); else info.Last.Clear();
                        if (!right.Nullable) Union(info.Last, right.Last);
                        info.Nullable &= right.Nullable;
                    }
                }
            }
            if (particle.Occurrence is DtdOccurrence.ZeroOrMore or DtdOccurrence.OneOrMore) Edges(info.Last, info.First);
            if (particle.Occurrence is DtdOccurrence.Optional or DtdOccurrence.ZeroOrMore) info.Nullable = true;
            infos.Push(info);
        }
        Info final = infos.Pop(); Union(follow[0], final.First);
        var next = new int[follow.Count][]; var accepting = new bool[follow.Count]; accepting[0] = final.Nullable;
        foreach (int end in final.Last) { budget.Work(1, at); accepting[end] = true; }
        for (int i = 0; i < follow.Count; i++)
        {
            budget.Work(1, at); var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (int target in follow[i])
            { budget.Work(positions[target - 1].Length + 1, at); if (!labels.Add(positions[target - 1])) Error("DTD.AmbiguousContent", "Content model is not deterministic.", at); }
            budget.Retain(follow[i].Count, at); next[i] = follow[i].ToArray(); Array.Sort(next[i]);
        }
        return new(root.Kind, positions.ToArray(), next, accepting);

        void Union(HashSet<int> target, HashSet<int> input)
        {
            foreach (int position in input)
            { budget.Work(1, at); if (target.Add(position)) budget.Retain(1, at); }
        }
        void Edges(HashSet<int> from, HashSet<int> to)
        {
            foreach (int position in from) { budget.Work(1, at); Union(follow[position], to); }
        }
    }
    private static void Error(string code, string message, XmlLocation at) => throw new XmlParseException([new(code, XmlDiagnosticSeverity.Fatal, message, at)]);
    private sealed class CompilationBudget(ValidationOperation operation)
    {
        // Finite implementation capacity even when caller opts out of all quotas.
        private const int MaximumStorageUnits = 1_048_576;
        private int retained;
        internal ValidationOperation Operation => operation;
        internal void Work(long amount, XmlLocation at) { operation.ChargeOperations(amount, at); Check(); }
        internal void Retain(int amount, XmlLocation at)
        {
            Work(1, at);
            if (amount > MaximumStorageUnits - retained) operation.Refuse("VALIDATION.Limit", "Compiled model capacity exceeded.", at);
            else retained += amount;
            Check();
        }
        internal void Check() { if (!operation.IsComplete) throw new XmlParseException(operation.Complete().Diagnostics); }
    }
    // Normalizer enumeration is metered at each probe; a repeated reference
    // cannot bypass aggregate work by repeatedly walking a large entity table.
    private sealed class BudgetedEntities(IReadOnlyDictionary<string, DtdEntityDeclaration> entities,
        CompilationBudget budget, XmlLocation at) : IReadOnlyDictionary<string, DtdEntityDeclaration>
    {
        public int Count => entities.Count;
        public IEnumerable<string> Keys => entities.Keys;
        public IEnumerable<DtdEntityDeclaration> Values => entities.Values;
        public DtdEntityDeclaration this[string key] => entities[key];
        public bool ContainsKey(string key) => entities.ContainsKey(key);
        public bool TryGetValue(string key, out DtdEntityDeclaration value) => entities.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<string, DtdEntityDeclaration>> GetEnumerator()
        {
            foreach (var pair in entities)
            { budget.Work(pair.Key.Length + 1L, at); yield return pair; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class DefaultBudget(ValidationOperation operation) : IDtdBudget
    {
        private long expanded; private int depth;
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation at)
        {
            if (!operation.ChargeOperations(count, at) || count > long.MaxValue - expanded || operation.Limits.ExpandedCharacters is long cap && count > cap - expanded)
                return Refuse(at);
            expanded += count; return null;
        }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation at) => operation.ChargeOperations(name.Length + 1L, at) ? null : Refuse(at);
        public XmlDiagnostic? EnterEntity(XmlLocation at)
        {
            if (!operation.ChargeOperations(1, at) || depth >= 4096 || operation.Limits.EntityDepth is int cap && depth >= cap) return Refuse(at);
            depth++; return null;
        }
        public void ExitEntity() => depth--;
        private XmlDiagnostic Refuse(XmlLocation at)
        { operation.Refuse("VALIDATION.Limit", "DTD default expansion limit exceeded.", at); return operation.Complete().Diagnostics[^1]; }
    }
}

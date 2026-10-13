namespace Xemele.Validation;

internal enum ReferenceKind { ExplicitElementType, InferredSubstitutionHeadType, SubstitutionAffiliation, ListItemType }
internal enum SymbolSpace { Element, Type }
internal enum AvailabilityState { Resolved, Unavailable }
internal enum OwnerKind { ElementDeclaration, SimpleType, TypeDefinition }
internal enum DependencyUse { TypeAssessment, SubstitutionSelection, SchemaValueConstraint, SchemaDerivation }
internal readonly record struct ComponentIdentity(OwnerKind Kind, SchemaName Name, int? DeclarationId);
internal readonly record struct DependencyEdge(ComponentIdentity Consumer, int ReferenceId, DependencyUse Use);
internal sealed class AvailabilityEntry
{
    internal int ReferenceId { get; }
    internal ComponentIdentity Owner { get; }
    internal ReferenceKind Kind { get; }
    internal SymbolSpace Space { get; }
    internal SchemaName AuthoredName { get; }
    internal XmlLocation ReferenceLocation { get; }
    internal AvailabilityState State { get; }
    internal AvailabilityEntry(int id, ComponentIdentity owner, ReferenceKind kind, SymbolSpace space,
        SchemaName name, XmlLocation at, AvailabilityState state)
    { ReferenceId = id; Owner = owner; Kind = kind; Space = space; AuthoredName = name; ReferenceLocation = at; State = state; }
}

// Closure is sealed once at publication. Validation scans borrowed arrays; no
// per-element traversal container, resolver, source handle or mutable cache.
internal sealed class ComponentAvailability
{
    private readonly AvailabilityEntry[] entries;
    private readonly DependencyEdge[] edges;
    internal IReadOnlyList<AvailabilityEntry> Entries { get; }
    internal IReadOnlyList<DependencyEdge> ClosureEdges { get; }
    internal ComponentAvailability(IReadOnlyList<AvailabilityEntry> entries, IReadOnlyList<DependencyEdge> edges, ISchemaOperation op)
    {
        if (entries.Count > int.MaxValue - edges.Count - 5)
        { op.Refuse("SCHEMA.Limit", "Availability snapshot representation exceeded.", default); throw SchemaSnapshots.Failure(op); }
        if (!op.ChargeComponents(entries.Count + edges.Count + 5, default)) throw SchemaSnapshots.Failure(op);
        this.entries = new AvailabilityEntry[entries.Count]; this.edges = new DependencyEdge[edges.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            if (!op.Charge(1, default)) throw SchemaSnapshots.Failure(op);
            if (entries[i].ReferenceId != i) throw new ArgumentException("Availability IDs must be dense.", nameof(entries));
            this.entries[i] = entries[i];
        }
        for (int i = 0; i < edges.Count; i++)
        {
            if (!op.Charge(1, default)) throw SchemaSnapshots.Failure(op);
            if ((uint)edges[i].ReferenceId >= (uint)entries.Count) throw new ArgumentException("Invalid reference ID.", nameof(edges));
            this.edges[i] = edges[i];
        }
        Entries = Array.AsReadOnly(this.entries); ClosureEdges = Array.AsReadOnly(this.edges);
        if (!op.Charge(0, default)) throw SchemaSnapshots.Failure(op);
    }
    internal AvailabilityEntry? Missing(ComponentIdentity owner, DependencyUse use, ISchemaOperation op, XmlLocation at)
    {
        if (!op.Charge(0, at)) throw SchemaSnapshots.Failure(op);
        AvailabilityEntry? missing = null;
        foreach (var edge in edges)
        {
            if (!op.Charge(1, at)) throw SchemaSnapshots.Failure(op);
            if (edge.Consumer != owner || edge.Use != use) continue;
            if (!op.Charge(1, at)) throw SchemaSnapshots.Failure(op);
            var entry = entries[edge.ReferenceId];
            if (entry.State == AvailabilityState.Unavailable) missing ??= entry;
        }
        if (!op.Charge(0, at)) throw SchemaSnapshots.Failure(op);
        return missing;
    }
    // Frozen graph-only lanes cannot consume the concrete partial graph without
    // its availability envelope. Charge every reference inspection.
    internal static void RequireComplete(XsdCompiledSchema schema, ISchemaOperation op)
    {
        if (!op.Charge(0, default)) throw SchemaSnapshots.Failure(op);
        foreach (var element in schema.Elements)
        {
            if (!op.Charge(1, element.Location)) throw SchemaSnapshots.Failure(op);
            if (element.Type is not { } name) throw new ArgumentException("Complete declaration graph required.", nameof(schema));
            bool found = false;
            foreach (var type in schema.Types)
            { if (!op.Charge(1, element.Location)) throw SchemaSnapshots.Failure(op); found |= type.Name == name; }
            if (!found || element.SubstitutionGroup is { } head && !schema.GlobalElements.ContainsKey(head))
                throw new ArgumentException("Complete declaration graph required.", nameof(schema));
        }
        // The compiler already charged the proof against this exact frozen list
        // and its immutable registry. Copies that replace either use the walk.
        if (schema.Datatypes is not XsdDefinitionRegistry registry || !registry.Certifies(schema.SimpleTypes))
        {
            foreach (var definition in schema.SimpleTypes)
            {
                if (!op.Charge(1, definition.Location)) throw SchemaSnapshots.Failure(op);
                if (definition.ItemType is { } item && schema.Datatypes.FindType(item) is null)
                    throw new ArgumentException("Complete datatype graph required.", nameof(schema));
            }
        }
        if (!op.Charge(0, default)) throw SchemaSnapshots.Failure(op);
    }
    internal static ComponentIdentity Element(XsdElementDeclaration element) => new(OwnerKind.ElementDeclaration, element.Name, element.Id);
    internal static ComponentIdentity Simple(SchemaName name) => new(OwnerKind.SimpleType, name, null);
    internal static ComponentIdentity Type(SchemaName name) => new(OwnerKind.TypeDefinition, name, null);
}

// Invocation-local compiler state. Charge before every retained entry, edge,
// closure inspection and traversal growth, including unlimited-budget runs.
internal sealed class AvailabilityBuilder
{
    private readonly ISchemaOperation operation;
    private readonly List<AvailabilityEntry> entries;
    private readonly List<DependencyEdge> edges;
    private readonly List<(ComponentIdentity Consumer, DependencyUse Use, ComponentIdentity Operand, DependencyUse OperandUse)> links;
    private bool hasUnavailableReference;
    internal AvailabilityBuilder(ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation); this.operation = operation;
        if (!operation.Charge(0, default) || !operation.ChargeComponents(4, default)) throw SchemaSnapshots.Failure(operation);
        entries = new(); edges = new(); links = new();
    }
    private void Work(XmlLocation at) { if (!operation.Charge(1, at)) throw SchemaSnapshots.Failure(operation); }
    private void Retain(int count, XmlLocation at) { if (!operation.ChargeComponents(count, at)) throw SchemaSnapshots.Failure(operation); }
    internal void Reference(ComponentIdentity owner, ReferenceKind kind, SymbolSpace space, SchemaName name,
        XmlLocation at, bool resolved, DependencyUse use)
    {
        Work(at); Retain(2, at);
        int id = entries.Count;
        entries.Add(new(id, owner, kind, space, name, at, resolved ? AvailabilityState.Resolved : AvailabilityState.Unavailable));
        edges.Add(new(owner, id, use));
        if (!resolved) hasUnavailableReference = true;
    }
    internal void Link(ComponentIdentity consumer, ComponentIdentity operand, DependencyUse use = DependencyUse.TypeAssessment,
        DependencyUse operandUse = DependencyUse.TypeAssessment)
    { Work(default); Retain(1, default); links.Add((consumer, use, operand, operandUse)); }
    internal void Join(XsdCompiledSchema schema)
    {
        foreach (var element in schema.Elements)
        { Work(element.Location); if (element.Type is { } name) Link(ComponentAvailability.Element(element), ComponentAvailability.Type(name)); }
        foreach (var type in schema.Types)
        {
            Work(type.Location);
            if (type.BaseType is { } basis && basis != type.Name) Link(ComponentAvailability.Type(type.Name), ComponentAvailability.Type(basis));
            if (type.SimpleType is { } simple) Link(ComponentAvailability.Type(type.Name), ComponentAvailability.Simple(simple));
        }
        foreach (var type in schema.SimpleTypes)
        {
            Work(type.Location);
            if (type.BaseType is { } basis && basis != type.Name) Link(ComponentAvailability.Simple(type.Name), ComponentAvailability.Simple(basis));
            if (type.ItemType is { } item) Link(ComponentAvailability.Simple(type.Name), ComponentAvailability.Simple(item));
            foreach (var member in type.MemberTypes) { Work(type.Location); Link(ComponentAvailability.Simple(type.Name), ComponentAvailability.Simple(member)); }
        }
    }
    private List<DependencyEdge> Closure()
    {
        Retain(1, default); var result = new List<DependencyEdge>();
        foreach (var edge in edges) { Work(default); Retain(1, default); result.Add(edge); }
        bool changed;
        do
        {
            changed = false;
            foreach (var link in links)
            {
                Work(default); int count = result.Count;
                for (int i = 0; i < count; i++)
                {
                    Work(default); var edge = result[i];
                    if (edge.Consumer != link.Operand || edge.Use != link.OperandUse) continue;
                    var next = new DependencyEdge(link.Consumer, edge.ReferenceId, link.Use);
                    bool found = false;
                    foreach (var existing in result) { Work(default); if (existing == next) { found = true; break; } }
                    if (!found) { Retain(1, default); result.Add(next); changed = true; }
                }
            }
        } while (changed);
        return result;
    }
    internal bool Unavailable(ComponentIdentity owner, DependencyUse use = DependencyUse.TypeAssessment)
    {
        // Links only propagate registered reference IDs. Without an unavailable
        // entry, no closure path can be unavailable; Seal still publishes it all.
        if (!hasUnavailableReference)
        {
            if (!operation.Charge(0, default)) throw SchemaSnapshots.Failure(operation);
            return false;
        }
        foreach (var edge in Closure())
        { Work(default); if (edge.Consumer == owner && edge.Use == use && entries[edge.ReferenceId].State == AvailabilityState.Unavailable) return true; }
        return false;
    }
    internal ComponentAvailability Seal()
    {
        var closure = Closure();
        return new(entries, closure, operation);
    }
}
internal sealed class CompilationEnvelope(XsdCompiledSchema components, ComponentAvailability availability)
{
    internal XsdCompiledSchema Components { get; } = components;
    internal ComponentAvailability Availability { get; } = availability;
}

using System.Runtime.CompilerServices;
namespace Xemele.Validation;

internal sealed partial class XmlFacetEvaluator : IXmlFacetEvaluator
{
    private readonly IXmlSchemaRegexCompiler compiler;
    private readonly IXmlSchemaRegexMatcher matcher;
    private readonly ConditionalWeakTable<IXmlDatatypeRegistry, ConditionalWeakTable<XmlDatatypeDefinition, FacetPlan>> prepared = new();
    internal XmlFacetEvaluator() { var engine = new XmlSchemaRegexEngine(); compiler = engine; matcher = engine; }
    internal XmlFacetEvaluator(IXmlSchemaRegexCompiler compiler, IXmlSchemaRegexMatcher matcher)
    { ArgumentNullException.ThrowIfNull(compiler); ArgumentNullException.ThrowIfNull(matcher); this.compiler = compiler; this.matcher = matcher; }

    public bool ValidateDefinition(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(operation);
        try
        {
            var op = new FacetOperation(operation);
            // Definition checks always consume the caller's compilation budget.
            // A failed or cancelled attempt never publishes a partial plan.
            var plan = Prepare(type, registry, op);
            if (plan is null) return false;
            Publish(type, registry, plan, op);
            return op.Charge(0, type.Location);
        }
        catch (FacetOperation.Stopped) { return false; }
    }
    // Only concrete XSD publication can defer missing-item value checks. This
    // route never constructs or caches an executable partial FacetPlan.
    internal bool ValidateAvailableDefinition(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry,
        ISchemaOperation operation, AvailabilityBuilder availability)
    {
        if (!availability.Unavailable(ComponentAvailability.Simple(type.Name))) return ValidateDefinition(type, registry, operation);
        try
        {
            var op = new FacetOperation(operation); op.ChargeComponents(1, type.Location);
            return CheckAvailableGraph(type, registry, op, availability, new HashSet<SchemaName>(), 0);
        }
        catch (FacetOperation.Stopped) { return false; }
    }
    internal bool CheckAvailableConstraint(XmlDatatypeDefinition type, ReadOnlySpan<char> lexical,
        IXmlDatatypeRegistry registry, ISchemaOperation operation)
    {
        try { return CheckIndependentConstraint(type, lexical, registry, new FacetOperation(operation)); }
        catch (FacetOperation.Stopped) { return false; }
    }
    public bool TryValidate(XmlDatatypeDefinition type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        IXmlDatatypeRegistry registry, ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(operation);
        value = null; error = null;
        try
        {
            var op = new FacetOperation(operation);
            op.Charge(1, type.Location);
            if (!prepared.TryGetValue(registry, out var cache) || !cache.TryGetValue(type, out var plan))
            {
                plan = Prepare(type, registry, op);
                if (plan is null) { error = InvalidDefinition; return false; }
                Publish(type, registry, plan, op);
            }
            if (!Parse(plan, lexical, context, op, out var result, out error))
            { if (!operation.IsComplete) error = DatatypeErrors.Stopped; return false; }
            op.Charge(0, type.Location); value = result; return true;
        }
        catch (FacetOperation.Stopped)
        { value = null; error = DatatypeErrors.Stopped; return false; }
    }
    // The caller supplies a previously parsed owned value and its normalized
    // lexical form. Used by composition and the primitive allocation oracle;
    // missing preparation is an argument error, never a definitions-only route.
    internal bool CheckPreparedValue(XmlDatatypeDefinition type, ReadOnlySpan<char> normalized, XmlDatatypeValue value,
        IXmlDatatypeRegistry registry, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(operation);
        if (!operation.Charge(1, type.Location)) return false;
        if (!prepared.TryGetValue(registry, out var cache) || !cache.TryGetValue(type, out var plan))
            throw new ArgumentException("Datatype was not prepared for this registry.", nameof(type));
        return Check(plan, normalized, value, operation) && operation.IsComplete;
    }
    private void Publish(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, FacetPlan plan, ISchemaOperation op)
    {
        op.Charge(1, type.Location); op.ChargeComponents(2, type.Location);
        // Weak keys avoid keeping whole schema registries alive. Concurrent
        // duplicate preparations are independent; only complete immutable plans
        // are published, and no caller work occurs under the table's lock.
        if (!prepared.TryGetValue(registry, out var cache))
        {
            var candidate = new ConditionalWeakTable<XmlDatatypeDefinition, FacetPlan>();
            lock (prepared) { if (!prepared.TryGetValue(registry, out cache)) { prepared.Add(registry, candidate); cache = candidate; } }
        }
        cache.AddOrUpdate(type, plan);
    }
    private static readonly XmlDatatypeError InvalidDefinition = new("XSD.Datatype.Definition", "Invalid datatype definition.");
    private static readonly XmlDatatypeError FacetMismatch = new("XSD.Datatype.Facet", "Datatype facet restriction failed.");
}

using System.Numerics;
namespace Xemele.Validation;

internal static class ContentWork
{
    internal static void Tick(ISchemaOperation op, XmlLocation at, long count = 1)
    {
        op.CancellationToken.ThrowIfCancellationRequested();
        if (!op.Charge(count, at)) throw SchemaSnapshots.Failure(op);
    }
    internal static void Retain(ISchemaOperation op, XmlLocation at, int count = 1)
    {
        Tick(op, at, count);
        if (!op.ChargeComponents(count, at)) throw SchemaSnapshots.Failure(op);
    }
    internal static void Integer(ISchemaOperation op, XmlLocation at, BigInteger value) => Tick(op, at, 1 + value.GetByteCount() / 8);
    internal static void Error(ISchemaOperation op, XmlLocation at, string message)
    {
        Tick(op, at);
        var diagnostic = new XmlDiagnostic("XSD.SchemaRestriction", XmlDiagnosticSeverity.Error, message, at);
        if (!op.AddDiagnostic(diagnostic)) throw SchemaSnapshots.Failure(op);
        throw op is SchemaCompileOperation compile ? compile.Exception() : new SchemaCompileException([diagnostic]);
    }
}

// Finite sets and their complements represent every namespace constraint in the
// frozen graph, including the absent namespace (the empty string).
internal static class ContentNamespaces
{
    internal static bool Allows(XsdWildcard wildcard, string ns, ISchemaOperation op, XmlLocation at)
    {
        bool found = false;
        foreach (string item in wildcard.Namespaces)
        { ContentWork.Tick(op, at); if (StringComparer.Ordinal.Equals(item, ns)) { found = true; break; } }
        return wildcard.Negated != found;
    }
    internal static bool SetSubset(XsdWildcard a, XsdWildcard b, ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Tick(op, at);
        if (!a.Negated)
        {
            foreach (string ns in a.Namespaces) if (!Allows(b, ns, op, at)) return false;
            return true;
        }
        if (!b.Negated) return false; // A cofinite set cannot fit a finite set.
        foreach (string ns in b.Namespaces) if (Allows(a, ns, op, at)) return false;
        return true;
    }
    // XSD 1.0 intensional subset is stricter than mathematical containment:
    // two negations must carry the same namespace test, unless super is any.
    internal static bool Subset(XsdWildcard a, XsdWildcard b, ISchemaOperation op, XmlLocation at)
    {
        Validate(a, op, at); Validate(b, op, at);
        if (b.Negated && b.Namespaces.Count == 0) return true;
        if (!a.Negated) return SetSubset(a, b, op, at);
        if (!b.Negated || a.Namespaces.Count == 0) return false;
        return StringComparer.Ordinal.Equals(NegatedValue(a, op, at), NegatedValue(b, op, at));
    }
    private static string NegatedValue(XsdWildcard wildcard, ISchemaOperation op, XmlLocation at)
    {
        foreach (string ns in wildcard.Namespaces) { ContentWork.Tick(op, at); if (ns.Length != 0) return ns; }
        return "";
    }
    internal static void Validate(XsdWildcard wildcard, ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Tick(op, at);
        if (!wildcard.Negated || wildcard.Namespaces.Count == 0) return;
        bool absent = false; string? value = null;
        foreach (string ns in wildcard.Namespaces)
        {
            ContentWork.Tick(op, at);
            if (ns.Length == 0) absent = true;
            else if (value is null) value = ns;
            else if (!StringComparer.Ordinal.Equals(value, ns)) ContentWork.Error(op, at, "Wildcard namespace constraint is not expressible in XSD 1.0.");
        }
        if (!absent) ContentWork.Error(op, at, "A negated XSD 1.0 namespace constraint must also exclude the absent namespace.");
    }
    internal static bool Overlaps(XsdWildcard a, XsdWildcard b, ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Tick(op, at);
        if (a.Negated && b.Negated) return true;
        var finite = a.Negated ? b : a; var other = a.Negated ? a : b;
        foreach (string ns in finite.Namespaces) if (Allows(other, ns, op, at)) return true;
        return false;
    }
}

internal sealed class ContentNode
{
    internal readonly XsdParticle Particle;
    private readonly ContentNode[] children;
    internal ReadOnlySpan<ContentNode> Children => children;
    private readonly (SchemaName Name, int Id)[] names;
    internal ReadOnlySpan<(SchemaName Name, int Id)> Names => names;
    internal readonly int Id;
    internal readonly bool BodyNullable, Nullable;
    private readonly int[] first, tail;
    internal ReadOnlySpan<int> First => first;
    internal ReadOnlySpan<int> Tail => tail;
    internal ContentNode(XsdParticle particle, ContentNode[] children, (SchemaName, int)[] names,
        int id, bool bodyNullable, int[] first, int[] tail)
    {
        Particle = particle; this.children = children; this.names = names; Id = id;
        BodyNullable = bodyNullable; Nullable = particle.MinOccurs.IsZero || particle.MaxOccurs == BigInteger.Zero || bodyNullable;
        this.first = first; this.tail = tail;
    }
    internal bool Matches(SchemaName name, ISchemaOperation op, out int? declaration, out XsdProcessContents policy)
    {
        declaration = null; policy = XsdProcessContents.Strict;
        if (Particle.Term is XsdWildcardTerm w)
        { policy = w.Wildcard.ProcessContents; return ContentNamespaces.Allows(w.Wildcard, name.NamespaceUri, op, Particle.Location); }
        foreach (var item in Names)
        { ContentWork.Tick(op, Particle.Location); if (item.Name == name) { declaration = item.Id; return true; } }
        return false;
    }
}

internal sealed class CompiledContent(ContentNode? root, ContentNode[] nodes, XmlLocation location) : XsdContentProgram
{
    internal ContentNode? Root { get; } = root;
    private readonly ContentNode[] nodes = nodes;
    internal ReadOnlySpan<ContentNode> Nodes => nodes;
    internal XmlLocation Location { get; } = location;
}

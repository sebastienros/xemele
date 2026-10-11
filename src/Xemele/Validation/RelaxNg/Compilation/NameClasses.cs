namespace Xemele.Validation;

internal static class RngNameClasses
{
    // Borrowed comparisons consume names synchronously. No temporary expanded name
    // or normalized string is allocated by the matcher.
    internal static bool Contains(RngNameClass name, ReadOnlySpan<char> ns, ReadOnlySpan<char> local, ISchemaOperation operation, XmlLocation at)
        => ContainsCore(name, ns, local, operation, at, 0);
    private static bool ContainsCore(RngNameClass name, ReadOnlySpan<char> ns, ReadOnlySpan<char> local, ISchemaOperation operation, XmlLocation at, int depth)
    {
        if (!operation.Charge(1, at)) throw SchemaSnapshots.Failure(operation);
        if (depth == 1024)
        { operation.Refuse("SCHEMA.Limit", "Name-class representation depth exceeded.", at); throw SchemaSnapshots.Failure(operation); }
        return name switch
        {
            RngName n => ns.SequenceEqual(n.Name.NamespaceUri) && local.SequenceEqual(n.Name.LocalName),
            RngAnyName a => a.Except is null || !ContainsCore(a.Except, ns, local, operation, at, depth + 1),
            RngNsName n => ns.SequenceEqual(n.NamespaceUri) && (n.Except is null || !ContainsCore(n.Except, ns, local, operation, at, depth + 1)),
            RngNameChoice c => ContainsCore(c.Left, ns, local, operation, at, depth + 1) || ContainsCore(c.Right, ns, local, operation, at, depth + 1),
            _ => throw new ArgumentException("Unknown name class.", nameof(name))
        };
    }

    // Boolean combinations of namespace and expanded-name equality are constant
    // on this finite partition. One fresh namespace/local representative covers
    // the remaining infinite cells, including arbitrarily nested exclusions.
    internal static bool Overlaps(RngNameClass left, RngNameClass right, ISchemaOperation operation, XmlLocation at)
    {
        if (left is RngName a) return Contains(right, a.Name.NamespaceUri, a.Name.LocalName, operation, at);
        if (right is RngName b) return Contains(left, b.Name.NamespaceUri, b.Name.LocalName, operation, at);
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var locals = new HashSet<string>(StringComparer.Ordinal);
        Collect(left, 0); Collect(right, 0);
        namespaces.Add(Fresh(namespaces)); locals.Add(Fresh(locals));
        foreach (string ns in namespaces)
            foreach (string local in locals)
                if (Contains(left, ns, local, operation, at) && Contains(right, ns, local, operation, at)) return true;
        return false;

        void Collect(RngNameClass name, int depth)
        {
            if (!operation.Charge(1, at)) throw SchemaSnapshots.Failure(operation);
            if (depth == 1024)
            { operation.Refuse("SCHEMA.Limit", "Name-class representation depth exceeded.", at); throw SchemaSnapshots.Failure(operation); }
            switch (name)
            {
                case RngName n: namespaces.Add(n.Name.NamespaceUri); locals.Add(n.Name.LocalName); break;
                case RngNsName n: namespaces.Add(n.NamespaceUri); if (n.Except is not null) Collect(n.Except, depth + 1); break;
                case RngAnyName n: if (n.Except is not null) Collect(n.Except, depth + 1); break;
                case RngNameChoice c: Collect(c.Left, depth + 1); Collect(c.Right, depth + 1); break;
            }
        }
    }
    private static string Fresh(HashSet<string> values)
    { string candidate = "_"; while (values.Contains(candidate)) candidate += "_"; return candidate; }
}

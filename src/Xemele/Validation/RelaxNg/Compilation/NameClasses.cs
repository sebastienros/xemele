namespace Xemele.Validation;

internal static class RngNameClasses
{
    // Borrowed comparisons consume names synchronously. No temporary expanded name
    // or normalized string is allocated by the matcher.
    internal static bool Contains(RngNameClass name, ReadOnlySpan<char> ns, ReadOnlySpan<char> local, ISchemaOperation operation, XmlLocation at)
    {
        if (!operation.Charge(1, at)) throw SchemaSnapshots.Failure(operation);
        return name switch
        {
            RngName n => ns.SequenceEqual(n.Name.NamespaceUri) && local.SequenceEqual(n.Name.LocalName),
            RngAnyName a => a.Except is null || !Contains(a.Except, ns, local, operation, at),
            RngNsName n => ns.SequenceEqual(n.NamespaceUri) && (n.Except is null || !Contains(n.Except, ns, local, operation, at)),
            RngNameChoice c => Contains(c.Left, ns, local, operation, at) || Contains(c.Right, ns, local, operation, at),
            _ => throw new ArgumentException("Unknown name class.", nameof(name))
        };
    }

    // Boolean combinations of namespace and expanded-name equality are constant
    // on this finite partition. One fresh namespace/local representative covers
    // the remaining infinite cells, including arbitrarily nested exclusions.
    internal static bool Overlaps(RngNameClass left, RngNameClass right, ISchemaOperation operation, XmlLocation at)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var locals = new HashSet<string>(StringComparer.Ordinal);
        Collect(left); Collect(right);
        namespaces.Add(Fresh(namespaces)); locals.Add(Fresh(locals));
        foreach (string ns in namespaces)
            foreach (string local in locals)
                if (Contains(left, ns, local, operation, at) && Contains(right, ns, local, operation, at)) return true;
        return false;

        void Collect(RngNameClass name)
        {
            if (!operation.Charge(1, at)) throw SchemaSnapshots.Failure(operation);
            switch (name)
            {
                case RngName n: namespaces.Add(n.Name.NamespaceUri); locals.Add(n.Name.LocalName); break;
                case RngNsName n: namespaces.Add(n.NamespaceUri); if (n.Except is not null) Collect(n.Except); break;
                case RngAnyName n: if (n.Except is not null) Collect(n.Except); break;
                case RngNameChoice c: Collect(c.Left); Collect(c.Right); break;
            }
        }
    }
    private static string Fresh(HashSet<string> values)
    { string candidate = "_"; while (values.Contains(candidate)) candidate += "_"; return candidate; }
}

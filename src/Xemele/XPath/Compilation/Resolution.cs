namespace Xemele.XPath.Internal;

// Static resolution never uses generated string hashing/equality on caller-sized
// names. Check cancellation during each actual <=256-unit scan/comparison.
internal static class XPathResolution
{
    internal static int FindColon(ReadOnlySpan<char> name, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        for (int i = 0; i < name.Length;)
        {
            cancellation.ThrowIfCancellationRequested(); int count = Math.Min(256, name.Length - i);
            int found = name.Slice(i, count).IndexOf(':');
            if (found >= 0) { cancellation.ThrowIfCancellationRequested(); return i + found; }
            i += count;
        }
        cancellation.ThrowIfCancellationRequested(); return -1;
    }
    internal static bool Equal(ReadOnlySpan<char> left, ReadOnlySpan<char> right, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length;)
        {
            cancellation.ThrowIfCancellationRequested(); int count = Math.Min(256, left.Length - i);
            if (!left.Slice(i, count).SequenceEqual(right.Slice(i, count))) return false;
            i += count;
        }
        cancellation.ThrowIfCancellationRequested(); return true;
    }
    internal static XPathFunctionSignature? FindFunction(IReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature> functions, XPathExpandedName name, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        foreach (var registration in functions)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Equal(name.NamespaceUri, registration.Key.NamespaceUri, cancellation) && Equal(name.LocalName, registration.Key.LocalName, cancellation)) return registration.Value;
        }
        cancellation.ThrowIfCancellationRequested(); return null;
    }
}

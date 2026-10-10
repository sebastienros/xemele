using Xemele.ReferenceProtocol;

internal static class AttributeNormalization
{
    internal static AttributeValue[] Sort(IEnumerable<AttributeValue> attributes) =>
        attributes.OrderBy(a => a.NamespaceUri, StringComparer.Ordinal)
            .ThenBy(a => a.LocalName, StringComparer.Ordinal).ToArray();
}

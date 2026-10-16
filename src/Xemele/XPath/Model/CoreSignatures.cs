using System.Collections.ObjectModel;
namespace Xemele.XPath.Internal;

internal static class XPathCoreSignatures
{
    internal static IReadOnlyList<XPathFunctionSignature> All
    {
        get;
    } = Array.AsReadOnly<XPathFunctionSignature>([
        new(new("", "last"), 0, 0),
        new(new("", "position"), 0, 0),
        new(new("", "count"), 1, 1),
        new(new("", "id"), 1, 1),
        new(new("", "local-name"), 0, 1),
        new(new("", "namespace-uri"), 0, 1),
        new(new("", "name"), 0, 1),
        new(new("", "string"), 0, 1),
        new(new("", "concat"), 2, null),
        new(new("", "starts-with"), 2, 2),
        new(new("", "contains"), 2, 2),
        new(new("", "substring-before"), 2, 2),
        new(new("", "substring-after"), 2, 2),
        new(new("", "substring"), 2, 3),
        new(new("", "string-length"), 0, 1),
        new(new("", "normalize-space"), 0, 1),
        new(new("", "translate"), 3, 3),
        new(new("", "boolean"), 1, 1),
        new(new("", "not"), 1, 1),
        new(new("", "true"), 0, 0),
        new(new("", "false"), 0, 0),
        new(new("", "lang"), 1, 1),
        new(new("", "number"), 0, 1),
        new(new("", "sum"), 1, 1),
        new(new("", "floor"), 1, 1),
        new(new("", "ceiling"), 1, 1),
        new(new("", "round"), 1, 1),
    ]);
    internal static IReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature> ByName { get; } = Index();
    private static IReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature> Index()
    {
        var owned = new Dictionary<XPathExpandedName, XPathFunctionSignature>();
        foreach (var signature in All)
        {
            owned.Add(signature.Name, signature);
        }

        return new ReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature>(owned);
    }
}

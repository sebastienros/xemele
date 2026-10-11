using System.Text;
namespace Xemele.XPath;

// Identity contains live stable handles; the metadata object contains only immutable strings.
internal readonly record struct ItemIdentity(XPathItemKind Kind, XmlNode Node, XmlAttribute Attribute, string? NamespacePrefix);
public readonly struct XPathItem : IEquatable<XPathItem>
{
    private sealed record Metadata(string StringValue, string LocalName, string NamespaceUri, string Prefix, string Name);
    private readonly Metadata? metadata;
    internal readonly ItemIdentity Identity;
    // Navigator uses captured, budget-checked fields. This path never rereads live topology/values.
    internal XPathItem(ItemIdentity identity, string value, string local = "", string uri = "", string prefix = "")
    {
        ModelGuard.Enum(identity.Kind); ArgumentNullException.ThrowIfNull(value); ArgumentNullException.ThrowIfNull(local); ArgumentNullException.ThrowIfNull(uri); ArgumentNullException.ThrowIfNull(prefix);
        if (identity.Node.IsDefault || (identity.Kind == XPathItemKind.Attribute && (identity.Attribute.IsDefault || !ReferenceEquals(identity.Attribute.Document, identity.Node.Document))) ||
            (identity.Kind != XPathItemKind.Attribute && !identity.Attribute.IsDefault) ||
            (identity.Kind == XPathItemKind.Namespace ? identity.NamespacePrefix is null : identity.NamespacePrefix is not null)) throw new ArgumentException("Invalid captured item identity.", nameof(identity));
        Identity = identity; metadata = new(value, local, uri, prefix, prefix.Length == 0 ? local : string.Concat(prefix, ":", local));
    }
    private Metadata Data => metadata ?? throw new InvalidOperationException("Default XPath item.");
    public bool IsDefault => metadata is null;
    public XPathItemKind Kind { get { _ = Data; return Identity.Kind; } }
    public string StringValue => Data.StringValue;
    public string LocalName => Data.LocalName;
    public string NamespaceUri => Data.NamespaceUri;
    public string Prefix => Data.Prefix;
    public string Name => Data.Name;
    public XmlNode? Node { get { _ = Data; return Identity.Kind is XPathItemKind.Attribute or XPathItemKind.Namespace ? null : Identity.Node; } }
    public XmlAttribute? Attribute { get { _ = Data; return Identity.Kind == XPathItemKind.Attribute ? Identity.Attribute : null; } }
    public XmlNode? OwnerElement { get { _ = Data; return Identity.Kind is XPathItemKind.Attribute or XPathItemKind.Namespace ? Identity.Node : null; } }
    internal XmlDocument Document => Identity.Node.OwnerDocument;
    internal static void RequireConnected(XmlNode node)
    {
        if (node.IsDefault || node.Kind == XmlNodeKind.Fragment) throw new ArgumentException("Require a document-connected node.", nameof(node));
        XmlNode top = node; while (!top.Parent.IsDefault) top = top.Parent;
        if (top.Kind != XmlNodeKind.Document) throw new ArgumentException("Detached node.", nameof(node));
    }
    internal static bool IsText(XmlNode node) => !node.IsDefault && node.Kind is XmlNodeKind.Text or XmlNodeKind.CData;
    public static XPathItem FromNode(XmlNode node)
    {
        RequireConnected(node);
        if (IsText(node))
        {
            while (IsText(node.PreviousSibling)) node = node.PreviousSibling;
            var value = new StringBuilder();
            for (var member = node; IsText(member); member = member.NextSibling) value.Append(member.Value);
            if (value.Length == 0) throw new ArgumentException("Empty logical text run.", nameof(node));
            return new(new(XPathItemKind.Text, node, default, null), value.ToString());
        }
        var kind = node.Kind switch { XmlNodeKind.Document => XPathItemKind.Root, XmlNodeKind.Element => XPathItemKind.Element, XmlNodeKind.Comment => XPathItemKind.Comment, XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction, _ => throw new ArgumentException("Unsupported node.", nameof(node)) };
        bool element = kind == XPathItemKind.Element, pi = kind == XPathItemKind.ProcessingInstruction;
        return new(new(kind, node, default, null), element || kind == XPathItemKind.Root ? node.GetTextContent() : node.Value,
            element || pi ? node.LocalName : "", element ? node.NamespaceUri : "", element ? node.Prefix : "");
    }
    public static XPathItem FromAttribute(XmlAttribute attribute)
    {
        if (attribute.IsDefault || !attribute.IsAttached) throw new ArgumentException("Require attached attribute.", nameof(attribute));
        var owner = attribute.OwnerElement; RequireConnected(owner);
        return new(new(XPathItemKind.Attribute, owner, attribute, null), attribute.Value, attribute.LocalName, attribute.NamespaceUri, attribute.Prefix);
    }
    public static XPathItem FromNamespace(XmlNode ownerElement, string prefix)
    {
        RequireConnected(ownerElement); ArgumentNullException.ThrowIfNull(prefix);
        if (ownerElement.Kind != XmlNodeKind.Element) throw new ArgumentException("Namespace owner must be element.", nameof(ownerElement));
        if (prefix.Length != 0) TreeValidation.Name(prefix);
        string? uri = NamespaceUriFor(ownerElement, prefix);
        if (uri is null || uri.Length == 0) throw new ArgumentException("Namespace is not in scope.", nameof(prefix));
        return new(new(XPathItemKind.Namespace, ownerElement, default, prefix), uri, prefix);
    }
    internal static string? NamespaceUriFor(XmlNode element, string prefix)
    {
        if (prefix == "xml") return TreeValidation.XmlUri;
        if (prefix == "xmlns") return null;
        for (var node = element; !node.IsDefault; node = node.Parent)
            foreach (var declaration in node.NamespaceDeclarations) if (declaration.Prefix == prefix) return declaration.Value;
        return null;
    }
    public bool Equals(XPathItem other) => IsDefault ? other.IsDefault : !other.IsDefault && Identity.Equals(other.Identity);
    public override bool Equals(object? other) => other is XPathItem item && Equals(item);
    public override int GetHashCode() => IsDefault ? 0 : Identity.GetHashCode();
    public static bool operator ==(XPathItem left, XPathItem right) => left.Equals(right);
    public static bool operator !=(XPathItem left, XPathItem right) => !left.Equals(right);
}
internal static class ItemOrder
{
    internal static Dictionary<ItemIdentity, int> Capture(XmlDocument document)
    {
        var result = new Dictionary<ItemIdentity, int>();
        XmlNode root = document.DocumentNode, current = root;
        while (true)
        {
            if (XPathItem.IsText(current))
            {
                // Only nonempty maximal logical runs are members of the XPath data model.
                if (!XPathItem.IsText(current.PreviousSibling))
                {
                    bool nonempty = false;
                    for (var member = current; XPathItem.IsText(member); member = member.NextSibling) nonempty |= member.Value.Length != 0;
                    if (nonempty) Add(new(XPathItemKind.Text, current, default, null));
                }
            }
            else
            {
                var kind = current.Kind switch { XmlNodeKind.Document => XPathItemKind.Root, XmlNodeKind.Element => XPathItemKind.Element, XmlNodeKind.Comment => XPathItemKind.Comment, XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction, _ => throw new ArgumentException("Unsupported tree item.") };
                Add(new(kind, current, default, null));
                if (kind == XPathItemKind.Element)
                {
                    var bindings = new Dictionary<string, string>(StringComparer.Ordinal) { ["xml"] = TreeValidation.XmlUri };
                    for (var owner = current; !owner.IsDefault; owner = owner.Parent)
                        foreach (var declaration in owner.NamespaceDeclarations) bindings.TryAdd(declaration.Prefix, declaration.Value);
                    var prefixes = new List<string>(); foreach (var binding in bindings) if (binding.Key != "xmlns" && binding.Value.Length != 0) prefixes.Add(binding.Key);
                    prefixes.Sort(StringComparer.Ordinal);
                    foreach (var prefix in prefixes) Add(new(XPathItemKind.Namespace, current, default, prefix));
                    var attributes = new List<XmlAttribute>(); foreach (var attribute in current.Attributes) attributes.Add(attribute);
                    attributes.Sort((a, b) => { int c = StringComparer.Ordinal.Compare(a.NamespaceUri, b.NamespaceUri); return c != 0 ? c : StringComparer.Ordinal.Compare(a.LocalName, b.LocalName); });
                    foreach (var attribute in attributes) Add(new(XPathItemKind.Attribute, current, attribute, null));
                }
            }
            if (!current.FirstChild.IsDefault) { current = current.FirstChild; continue; }
            while (current != root && current.NextSibling.IsDefault) current = current.Parent;
            if (current == root) break;
            current = current.NextSibling;
        }
        return result;
        void Add(ItemIdentity identity) => result.Add(identity, result.Count);
    }
}

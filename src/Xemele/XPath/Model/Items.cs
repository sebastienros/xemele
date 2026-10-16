using System.Text;
using Xemele.Internal;
namespace Xemele.XPath;

// Identity contains live stable handles; the metadata object contains only immutable strings.
internal readonly record struct ItemIdentity(XPathItemKind Kind, XmlNode Node, XmlAttribute Attribute, string? NamespacePrefix);
/// <summary>Represents a logical XPath node with captured metadata and stable identity in an XML document.</summary>
/// <remarks>Names and string values are snapshots; returned XML handles still refer to the live document.</remarks>
public readonly struct XPathItem : IEquatable<XPathItem>
{
    private sealed record Metadata(string StringValue, string LocalName, string NamespaceUri, string Prefix, string Name, long ContentRevision);
    private readonly Metadata? metadata;
    internal readonly ItemIdentity Identity;
    // Navigator uses captured, budget-checked fields. This path never rereads live topology/values.
    internal XPathItem(ItemIdentity identity, string value, string local = "", string uri = "", string prefix = "")
    {
        ModelGuard.Enum(identity.Kind);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(prefix);
        if (identity.Node.IsDefault || (identity.Kind == XPathItemKind.Attribute && (identity.Attribute.IsDefault || !ReferenceEquals(identity.Attribute.Document, identity.Node.Document))) ||
            (identity.Kind != XPathItemKind.Attribute && !identity.Attribute.IsDefault) ||
            (identity.Kind == XPathItemKind.Namespace ? identity.NamespacePrefix is null : identity.NamespacePrefix is not null))
        {
            throw new ArgumentException("Invalid captured item identity.", nameof(identity));
        }

        Identity = identity;
        metadata = new(value, local, uri, prefix, prefix.Length == 0 ? local : string.Concat(prefix, ":", local), identity.Node.OwnerDocument.ContentRevision);
    }
    private Metadata Data => metadata ?? throw new InvalidOperationException("Default XPath item.");
    /// <summary>Gets whether this is an uninitialized item with no captured node identity.</summary>
    public bool IsDefault => metadata is null;
    /// <summary>Gets the XPath data-model kind of this item.</summary>
    /// <exception cref="InvalidOperationException">The item is default.</exception>
    public XPathItemKind Kind
    {
        get
        {
            _ = Data;
            return Identity.Kind;
        }
    }
    /// <summary>Gets the string value captured when the item was created.</summary>
    public string StringValue => Data.StringValue;
    /// <summary>Gets the captured local name, or an empty string for unnamed node kinds.</summary>
    public string LocalName => Data.LocalName;
    /// <summary>Gets the captured namespace URI, or an empty string for unnamespaced node kinds.</summary>
    public string NamespaceUri => Data.NamespaceUri;
    /// <summary>Gets the captured name prefix, or an empty string when no prefix applies.</summary>
    public string Prefix => Data.Prefix;
    /// <summary>Gets the captured qualified name formed from the prefix and local name.</summary>
    public string Name => Data.Name;
    /// <summary>Gets the live XML node handle, or null for attribute and namespace items.</summary>
    /// <remarks>A logical text item returns the first node in its captured adjacent text/CDATA run.</remarks>
    public XmlNode? Node
    {
        get
        {
            _ = Data;
            return Identity.Kind is XPathItemKind.Attribute or XPathItemKind.Namespace ? null : Identity.Node;
        }
    }
    /// <summary>Gets the live attribute handle for an attribute item, or null for other kinds.</summary>
    public XmlAttribute? Attribute
    {
        get
        {
            _ = Data;
            return Identity.Kind == XPathItemKind.Attribute ? Identity.Attribute : null;
        }
    }
    /// <summary>Gets the owning element for an attribute or namespace item, or null for other kinds.</summary>
    public XmlNode? OwnerElement
    {
        get
        {
            _ = Data;
            return Identity.Kind is XPathItemKind.Attribute or XPathItemKind.Namespace ? Identity.Node : null;
        }
    }
    internal XmlDocument Document => Identity.Node.OwnerDocument;
    internal long CapturedContentRevision => Data.ContentRevision;
    internal static void RequireConnected(XmlNode node)
    {
        if (node.IsDefault || node.Kind == XmlNodeKind.Fragment)
        {
            throw new ArgumentException("Require a document-connected node.", nameof(node));
        }

        var top = node;
        while (!top.Parent.IsDefault)
        {
            top = top.Parent;
        }

        if (top.Kind != XmlNodeKind.Document)
        {
            throw new ArgumentException("Detached node.", nameof(node));
        }
    }
    internal static bool IsText(XmlNode node) => !node.IsDefault && node.Kind is XmlNodeKind.Text or XmlNodeKind.CData;
    /// <summary>Captures an XPath item from a document-connected XML node.</summary>
    /// <param name="node">The XML node to represent.</param>
    /// <returns>An item with owned metadata; adjacent text and CDATA nodes are represented as one logical text run.</returns>
    /// <exception cref="ArgumentException">The node is default, detached, unsupported, or belongs to an empty logical text run.</exception>
    public static XPathItem FromNode(XmlNode node)
    {
        RequireConnected(node);
        if (IsText(node))
        {
            while (IsText(node.PreviousSibling))
            {
                node = node.PreviousSibling;
            }

            using var value = new ValueStringBuilder();
            for (var member = node; IsText(member); member = member.NextSibling)
            {
                value.Append(member.Value);
            }

            if (value.Length == 0)
            {
                throw new ArgumentException("Empty logical text run.", nameof(node));
            }

            return new(new(XPathItemKind.Text, node, default, null), value.ToString());
        }
        var kind = node.Kind switch
        {
            XmlNodeKind.Document => XPathItemKind.Root,
            XmlNodeKind.Element => XPathItemKind.Element,
            XmlNodeKind.Comment => XPathItemKind.Comment,
            XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction,
            _ => throw new ArgumentException("Unsupported node.", nameof(node))
        };
        bool element = kind == XPathItemKind.Element, pi = kind == XPathItemKind.ProcessingInstruction;
        return new(new(kind, node, default, null), element || kind == XPathItemKind.Root ? node.GetTextContent() : node.Value,
            element || pi ? node.LocalName : "", element ? node.NamespaceUri : "", element ? node.Prefix : "");
    }
    /// <summary>Captures an XPath attribute item from an attached attribute.</summary>
    /// <param name="attribute">The attribute on a document-connected element.</param>
    /// <returns>An item with the attribute's current name and string value.</returns>
    /// <exception cref="ArgumentException">The attribute is default, detached, or owned by a detached element.</exception>
    public static XPathItem FromAttribute(XmlAttribute attribute)
    {
        if (attribute.IsDefault || !attribute.IsAttached)
        {
            throw new ArgumentException("Require attached attribute.", nameof(attribute));
        }

        var owner = attribute.OwnerElement;
        RequireConnected(owner);
        return new(new(XPathItemKind.Attribute, owner, attribute, null), attribute.Value, attribute.LocalName, attribute.NamespaceUri, attribute.Prefix);
    }
    /// <summary>Captures an in-scope XPath namespace item for an element.</summary>
    /// <param name="ownerElement">The document-connected element owning the namespace item.</param>
    /// <param name="prefix">The namespace prefix, or an empty string for the default namespace.</param>
    /// <returns>An item whose string value is the captured namespace URI.</returns>
    /// <exception cref="ArgumentNullException">The prefix is null.</exception>
    /// <exception cref="ArgumentException">The owner is invalid, the prefix is invalid, or no nonempty namespace is in scope.</exception>
    public static XPathItem FromNamespace(XmlNode ownerElement, string prefix)
    {
        RequireConnected(ownerElement);
        ArgumentNullException.ThrowIfNull(prefix);
        if (ownerElement.Kind != XmlNodeKind.Element)
        {
            throw new ArgumentException("Namespace owner must be element.", nameof(ownerElement));
        }

        if (prefix.Length != 0)
        {
            TreeValidation.Name(prefix);
        }

        var uri = NamespaceUriFor(ownerElement, prefix);
        if (uri is null || uri.Length == 0)
        {
            throw new ArgumentException("Namespace is not in scope.", nameof(prefix));
        }

        return new(new(XPathItemKind.Namespace, ownerElement, default, prefix), uri, prefix);
    }
    internal static string? NamespaceUriFor(XmlNode element, string prefix)
    {
        if (prefix == "xml")
        {
            return TreeValidation.XmlUri;
        }

        if (prefix == "xmlns")
        {
            return null;
        }

        for (var node = element; !node.IsDefault; node = node.Parent)
        {
            foreach (var declaration in node.NamespaceDeclarations)
            {
                if (declaration.Prefix == prefix)
                {
                    return declaration.Value;
                }
            }
        }

        return null;
    }
    /// <summary>Compares stable node identities rather than captured metadata values.</summary>
    /// <param name="other">The item to compare.</param>
    /// <returns>Whether the identities match; two default items are equal.</returns>
    public bool Equals(XPathItem other) => IsDefault ? other.IsDefault : !other.IsDefault && Identity.Equals(other.Identity);
    /// <inheritdoc/>
    public override bool Equals(object? other) => other is XPathItem item && Equals(item);
    /// <inheritdoc/>
    public override int GetHashCode() => IsDefault ? 0 : Identity.GetHashCode();
    /// <summary>Tests whether two items have the same stable node identity.</summary>
    /// <param name="left">The first item.</param>
    /// <param name="right">The second item.</param>
    /// <returns>Whether the items are equal.</returns>
    public static bool operator ==(XPathItem left, XPathItem right) => left.Equals(right);
    /// <summary>Tests whether two items have different stable node identities.</summary>
    /// <param name="left">The first item.</param>
    /// <param name="right">The second item.</param>
    /// <returns>Whether the items are unequal.</returns>
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
                    var nonempty = false;
                    for (var member = current; XPathItem.IsText(member); member = member.NextSibling)
                    {
                        nonempty |= member.Value.Length != 0;
                    }

                    if (nonempty)
                    {
                        Add(new(XPathItemKind.Text, current, default, null));
                    }
                }
            }
            else
            {
                var kind = current.Kind switch
                {
                    XmlNodeKind.Document => XPathItemKind.Root,
                    XmlNodeKind.Element => XPathItemKind.Element,
                    XmlNodeKind.Comment => XPathItemKind.Comment,
                    XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction,
                    _ => throw new ArgumentException("Unsupported tree item.")
                };
                Add(new(kind, current, default, null));
                if (kind == XPathItemKind.Element)
                {
                    var bindings = new Dictionary<string, string>(StringComparer.Ordinal) { ["xml"] = TreeValidation.XmlUri };
                    for (var owner = current; !owner.IsDefault; owner = owner.Parent)
                    {
                        foreach (var declaration in owner.NamespaceDeclarations)
                        {
                            bindings.TryAdd(declaration.Prefix, declaration.Value);
                        }
                    }

                    var prefixes = new List<string>();
                    foreach (var binding in bindings)
                    {
                        if (binding.Key != "xmlns" && binding.Value.Length != 0)
                        {
                            prefixes.Add(binding.Key);
                        }
                    }

                    prefixes.Sort(StringComparer.Ordinal);
                    foreach (var prefix in prefixes)
                    {
                        Add(new(XPathItemKind.Namespace, current, default, prefix));
                    }

                    var attributes = new List<XmlAttribute>();
                    foreach (var attribute in current.Attributes)
                    {
                        attributes.Add(attribute);
                    }

                    attributes.Sort((a, b) => { var c = StringComparer.Ordinal.Compare(a.NamespaceUri, b.NamespaceUri); return c != 0 ? c : StringComparer.Ordinal.Compare(a.LocalName, b.LocalName); });
                    foreach (var attribute in attributes)
                    {
                        Add(new(XPathItemKind.Attribute, current, attribute, null));
                    }
                }
            }
            if (!current.FirstChild.IsDefault)
            {
                current = current.FirstChild;
                continue;
            }
            while (current != root && current.NextSibling.IsDefault)
            {
                current = current.Parent;
            }

            if (current == root)
            {
                break;
            }

            current = current.NextSibling;
        }
        return result;
        void Add(ItemIdentity identity) => result.Add(identity, result.Count);
    }
}

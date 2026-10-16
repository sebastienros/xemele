using System.Xml;
using System.Xml.XPath;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xemele.Parsing.Dtd;
using BclNavigator = System.Xml.XPath.XPathNavigator;

namespace Xemele.Interop;

/// <summary>Read-only XPath cursor over a captured immutable data model. Clones share the capture.</summary>
public sealed class XemeleXPathNavigatorAdapter : BclNavigator
{
    private const string XmlUri = "http://www.w3.org/XML/1998/namespace";
    private sealed class Row(XPathNodeType kind, string local, string prefix, string uri, string value, int parent, string baseUri)
    {
        internal readonly XPathNodeType Kind = kind;
        internal readonly string Local = local, Prefix = prefix, Uri = uri, Value = value, BaseUri = baseUri;
        internal string Name = prefix.Length == 0 ? local : string.Concat(prefix, ":", local);
        internal readonly int Parent = parent;
        internal int FirstChild = -1, Next = -1, Previous = -1, FirstAttribute = -1, FirstNamespace = -1;
        internal bool LocalNamespace;
    }
    private sealed class Snapshot
    {
        internal readonly Row[] Rows;
        internal readonly NameTable Names = new();
        internal readonly Dictionary<string, int> Ids = new(StringComparer.Ordinal);
        internal Snapshot(XmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            if (document.UnresolvedReferences.Count != 0 || document.HasIncompleteAttributes || !document.SourceInputComplete)
            {
                throw new ArgumentException("Navigator requires complete input.", nameof(document));
            }
            // Reuse M4's owned/coalesced XPath model; BCL evaluation imposes its own
            // semantics, so no Xemele query budget is silently applied here.
            var budget = new XPathBudget(new()
            {
                MaxOperations = null,
                MaxInputItems = null,
                MaxInputCharacters = null,
                MaxNodeSetItems = null,
                MaxStringCharacters = null
            }, default, default);
            var capture = new Xemele.XPath.Internal.XPathNavigator(document, budget);
            var items = capture.SelectAxis(capture.Root, XPathAxis.DescendantOrSelf, budget);
            var rows = new List<Row>();
            var ordinary = new Dictionary<XmlNode, int>();
            var lastChildren = new Dictionary<int, int>();
            foreach (var item in items)
            {
                var node = item.Node!.Value;
                var parent = node.Parent.IsDefault ? -1 : ordinary[node.Parent];
                var baseUri = parent < 0 ? "" : rows[parent].BaseUri;
                if (item.Kind == XPathItemKind.Element && node.GetAttribute("base", XmlUri) is string b)
                {
                    baseUri = AdapterBaseUri.Resolve(baseUri, b);
                }

                var row = new Row(item.Kind switch
                {
                    XPathItemKind.Root => XPathNodeType.Root,
                    XPathItemKind.Element => XPathNodeType.Element,
                    XPathItemKind.Text => XPathNodeType.Text,
                    XPathItemKind.Comment => XPathNodeType.Comment,
                    XPathItemKind.ProcessingInstruction => XPathNodeType.ProcessingInstruction,
                    _ => throw new InvalidOperationException()
                }, Intern(item.LocalName), Intern(item.Prefix), Intern(item.NamespaceUri), item.StringValue, parent, baseUri);
                var index = rows.Count;
                rows.Add(row);
                ordinary.Add(node, index);
                if (parent >= 0)
                {
                    if (lastChildren.TryGetValue(parent, out var prior))
                    {
                        rows[prior].Next = index;
                        row.Previous = prior;
                    }
                    else
                    {
                        rows[parent].FirstChild = index;
                    }

                    lastChildren[parent] = index;
                }
                if (item.Kind != XPathItemKind.Element)
                {
                    continue;
                }

                var previous = -1;
                foreach (var a in node.Attributes)
                {
                    var at = rows.Count;
                    rows.Add(new(XPathNodeType.Attribute, Intern(a.LocalName), Intern(a.Prefix), Intern(a.NamespaceUri), a.Value, index, baseUri));
                    if (previous < 0)
                    {
                        row.FirstAttribute = at;
                    }
                    else
                    {
                        rows[previous].Next = at;
                    }

                    rows[at].Previous = previous;
                    previous = at;
                    var id = a.NamespaceUri == XmlUri && a.LocalName == "id";
                    if (document.Dtd is { } dtd)
                    {
                        foreach (var declaration in dtd.Attributes)
                        {
                            if (declaration.Type == DtdAttributeType.Id && declaration.ElementName == item.Name && declaration.Name == rows[at].Name)
                            {
                                id = true;
                            }
                        }
                    }

                    if (id)
                    {
                        var key = a.Value.Trim(' ', '\t', '\r', '\n');
                        if (!Ids.TryAdd(key, index) && Ids[key] != index)
                        {
                            Ids[key] = -1;
                        }
                    }
                }
                var bindings = new SortedDictionary<string, (string Uri, bool Local)>(StringComparer.Ordinal);
                for (var owner = node; !owner.IsDefault; owner = owner.Parent)
                {
                    foreach (var declaration in owner.NamespaceDeclarations)
                    {
                        bindings.TryAdd(declaration.Prefix, (declaration.Value, owner == node));
                    }
                }

                bindings.TryAdd("xml", (XmlUri, false));
                previous = -1;
                foreach (var binding in bindings)
                {
                    if (binding.Key == "xmlns" || binding.Value.Uri.Length == 0)
                    {
                        continue;
                    }

                    var at = rows.Count;
                    rows.Add(new(XPathNodeType.Namespace, Intern(binding.Key), "", "", binding.Value.Uri, index, baseUri)
                    {
                        LocalNamespace = binding.Value.Local
                    });
                    if (previous < 0)
                    {
                        row.FirstNamespace = at;
                    }
                    else
                    {
                        rows[previous].Next = at;
                    }

                    rows[at].Previous = previous;
                    previous = at;
                }
            }
            foreach (var row in rows)
            {
                row.Name = Names.Add(row.Name);
            }

            Rows = [.. rows];
        }
        private string Intern(string name) => Names.Add(name);
    }
    private readonly Snapshot snapshot;
    private int position;
    private Row Current => snapshot.Rows[position];
    /// <summary>Captures a complete document as a read-only XPath data model, initially positioned at the root.</summary>
    /// <param name="document">The document to capture. Subsequent document changes do not update the capture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentException">The document has incomplete input, unresolved references, or incomplete attribute values.</exception>
    public XemeleXPathNavigatorAdapter(XmlDocument document) => snapshot = new(document);
    private XemeleXPathNavigatorAdapter(Snapshot snapshot, int position)
    {
        this.snapshot = snapshot;
        this.position = position;
    }
    /// <inheritdoc/>
    public override string BaseURI => Current.BaseUri;
    /// <inheritdoc/>
    public override bool IsEmptyElement => Current.Kind == XPathNodeType.Element && Current.FirstChild < 0;
    /// <inheritdoc/>
    public override string LocalName => Current.Local;
    /// <inheritdoc/>
    public override string Name => Current.Name;
    /// <inheritdoc/>
    public override string NamespaceURI => Current.Uri;
    /// <inheritdoc/>
    public override XmlNameTable NameTable => snapshot.Names;
    /// <inheritdoc/>
    public override XPathNodeType NodeType => Current.Kind;
    /// <inheritdoc/>
    public override string Prefix => Current.Prefix;
    /// <inheritdoc/>
    public override string Value => Current.Value;
    /// <inheritdoc/>
    public override bool CanEdit => false;
    /// <inheritdoc/>
    /// <remarks>The clone shares this navigator's immutable capture.</remarks>
    public override BclNavigator Clone() => new XemeleXPathNavigatorAdapter(snapshot, position);
    /// <inheritdoc/>
    /// <remarks>Only navigators sharing the same capture can identify the same position.</remarks>
    public override bool IsSamePosition(BclNavigator other) => other is XemeleXPathNavigatorAdapter adapter && ReferenceEquals(snapshot, adapter.snapshot) && position == adapter.position;
    /// <inheritdoc/>
    /// <remarks>Movement succeeds only for another adapter sharing this capture.</remarks>
    public override bool MoveTo(BclNavigator other)
    {
        if (other is not XemeleXPathNavigatorAdapter adapter || !ReferenceEquals(snapshot, adapter.snapshot))
        {
            return false;
        }

        position = adapter.position;
        return true;
    }
    private bool Move(int destination)
    {
        if (destination < 0)
        {
            return false;
        }

        position = destination;
        return true;
    }
    /// <inheritdoc/>
    public override bool MoveToFirstAttribute() => Move(Current.FirstAttribute);
    /// <inheritdoc/>
    public override bool MoveToNextAttribute() => Current.Kind == XPathNodeType.Attribute && Move(Current.Next);
    /// <inheritdoc/>
    public override bool MoveToFirstChild() => Move(Current.FirstChild);
    /// <inheritdoc/>
    public override bool MoveToNext() => Current.Kind is not (XPathNodeType.Attribute or XPathNodeType.Namespace) && Move(Current.Next);
    /// <inheritdoc/>
    public override bool MoveToPrevious() => Current.Kind is not (XPathNodeType.Attribute or XPathNodeType.Namespace) && Move(Current.Previous);
    /// <inheritdoc/>
    public override bool MoveToParent() => Move(Current.Parent);
    /// <inheritdoc/>
    public override bool MoveToId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return snapshot.Ids.TryGetValue(id, out var destination) && Move(destination);
    }
    private bool MoveNamespace(int first, XPathNamespaceScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        for (var i = first; i >= 0; i = snapshot.Rows[i].Next)
        {
            var row = snapshot.Rows[i];
            if (scope == XPathNamespaceScope.Local ? row.LocalNamespace : scope != XPathNamespaceScope.ExcludeXml || row.Local != "xml")
            {
                return Move(i);
            }
        }
        return false;
    }
    /// <inheritdoc/>
    public override bool MoveToFirstNamespace(XPathNamespaceScope namespaceScope) => MoveNamespace(Current.FirstNamespace, namespaceScope);
    /// <inheritdoc/>
    public override bool MoveToNextNamespace(XPathNamespaceScope namespaceScope)
    {
        if (!Enum.IsDefined(namespaceScope))
        {
            throw new ArgumentOutOfRangeException(nameof(namespaceScope));
        }

        return Current.Kind == XPathNodeType.Namespace && MoveNamespace(Current.Next, namespaceScope);
    }
    /// <inheritdoc/>
    public override void MoveToRoot() => position = 0;
}

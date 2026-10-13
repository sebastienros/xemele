using Xemele.Parsing.Dtd;

namespace Xemele.XPath.Internal;

// A query owns this captured data. Live handles are used only as identity keys
// after construction. The caller must exclude mutation while capture runs.
internal sealed class XPathNavigator : IXPathNavigator
{
    private sealed class Entry(XPathItem item, int parent)
    {
        internal readonly XPathItem Item = item;
        internal readonly int Parent = parent;
        internal int End;
        internal bool IsId;
        internal string? Language;
    }
    private readonly List<Entry> entries = [];
    private readonly Dictionary<ItemIdentity, int> order = [];
    private readonly XPathSourceSpan span;
    private readonly XPathBudget inputBudget;
    private long characters;
    internal int InputItems => entries.Count;
    internal long InputCharacters => characters;
    public XPathItem Root => entries[0].Item;

    internal XPathNavigator(XmlDocument document, XPathBudget budget, XPathSourceSpan span = default)
        : this(document, budget, budget, span) { }

    // Separate size accounting and work charging supports hosts that forward
    // the shared budget through a metered adapter. Normal evaluation uses the
    // same XPathBudget for both roles.
    internal XPathNavigator(XmlDocument document, XPathBudget inputBudget, IXPathBudget budget, XPathSourceSpan span = default)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(inputBudget); ArgumentNullException.ThrowIfNull(budget);
        this.inputBudget = inputBudget;
        this.span = ModelGuard.Span(span); budget.ChargeOperations(0);
        if (document.UnresolvedReferences.Count != 0 || document.HasIncompleteAttributes)
            throw Error("XPATH-INCOMPLETE", "Document has unresolved references or incomplete attributes.");
        XmlNode root = document.DocumentNode, node = root;
        do
        {
            budget.ChargeOperations(1);
            int parent = node.Parent.IsDefault ? -1 : LookupNode(node.Parent, budget);
            if (XPathItem.IsText(node))
            {
                // Visit every physical member, including empty omitted runs.
                XmlNode first = node, after = node;
                long length = 0;
                do
                {
                    if (after != first) budget.ChargeOperations(1);
                    length = AddLength(length, after.Value.Length, budget);
                    after = after.NextSibling;
                } while (XPathItem.IsText(after));
                if (length != 0)
                {
                    Preflight((int)length, "", "", "", budget);
                    string text = CopyText(first, (int)length, true, budget);
                    Add(new(XPathItemKind.Text, first, default, null), text, "", "", "", parent, budget);
                }
                node = after.IsDefault ? NextAfter(first.Parent, root, budget) : after;
                continue;
            }
            var kind = node.Kind switch
            {
                XmlNodeKind.Document => XPathItemKind.Root, XmlNodeKind.Element => XPathItemKind.Element,
                XmlNodeKind.Comment => XPathItemKind.Comment, XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction,
                _ => throw new ArgumentException("Unsupported input node.", nameof(document))
            };
            string local = kind is XPathItemKind.Element or XPathItemKind.ProcessingInstruction ? node.LocalName : "";
            string uri = kind == XPathItemKind.Element ? node.NamespaceUri : "";
            string prefix = kind == XPathItemKind.Element ? node.Prefix : "";
            bool aggregate = kind is XPathItemKind.Root or XPathItemKind.Element;
            int valueLength = aggregate ? TextLength(node, budget) : node.Value.Length;
            Preflight(valueLength, local, uri, prefix, budget);
            string value = aggregate ? CopyText(node, valueLength, false, budget) : node.Value;
            int index = Add(new(kind, node, default, null), value, local, uri, prefix, parent, budget);
            if (kind == XPathItemKind.Element)
            {
                CaptureNamespaces(node, index, budget);
                int start = entries.Count;
                foreach (var attribute in node.Attributes)
                {
                    budget.ChargeOperations(1);
                    Preflight(attribute.Value.Length, attribute.LocalName, attribute.NamespaceUri, attribute.Prefix, budget);
                    int at = Add(new(XPathItemKind.Attribute, node, attribute, null), attribute.Value,
                        attribute.LocalName, attribute.NamespaceUri, attribute.Prefix, index, budget);
                    if (Equal(attribute.NamespaceUri, TreeValidation.XmlUri, budget) && Equal(attribute.LocalName, "lang", budget))
                        entries[index].Language = attribute.Value;
                    if (document.Dtd is { } dtd)
                        foreach (var declaration in dtd.Attributes)
                        {
                            budget.ChargeOperations(1);
                            if (declaration.Type == DtdAttributeType.Id && Equal(declaration.ElementName, entries[index].Item.Name, budget) &&
                                Equal(declaration.Name, entries[at].Item.Name, budget)) entries[at].IsId = true;
                        }
                }
                SortSection(start, budget, false);
            }
            node = !node.FirstChild.IsDefault ? node.FirstChild : NextAfter(node, root, budget);
        } while (!node.IsDefault);
        // Rebuild indexes after deterministic namespace/attribute sorting; these
        // leaf sections never move a captured parent's index.
        order.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            ChargeIdentity(entries[i].Item.Identity, budget);
            if (entries[i].Item.Kind != XPathItemKind.Namespace) order.Add(entries[i].Item.Identity, i);
            entries[i].End = i;
        }
        for (int i = entries.Count - 1; i > 0; i--)
        {
            budget.ChargeOperations(1);
            int p = entries[i].Parent; entries[p].End = Math.Max(entries[p].End, entries[i].End);
        }
        budget.ChargeOperations(0);
    }

    private static XmlNode NextAfter(XmlNode node, XmlNode root, IXPathBudget budget)
    {
        while (node != root && node.NextSibling.IsDefault) { budget.ChargeOperations(1); node = node.Parent; }
        budget.ChargeOperations(1); return node == root ? default : node.NextSibling;
    }
    private long AddLength(long length, int extra, IXPathBudget budget)
    {
        if (length > int.MaxValue - extra) { budget.CheckStringSize(int.MaxValue); throw Limit(); }
        length += extra; budget.CheckStringSize((int)length); return length;
    }
    private XPathEvaluationException Limit() => new(new("XPATH-EVALUATION-LIMIT", XPathDiagnosticCategory.EvaluationLimit, "XPath representational capacity exceeded.", span));
    private int TextLength(XmlNode owner, IXPathBudget budget)
    {
        long length = 0;
        for (var node = owner.FirstChild; !node.IsDefault; node = !node.FirstChild.IsDefault ? node.FirstChild : NextAfter(node, owner, budget))
        { budget.ChargeOperations(1); if (XPathItem.IsText(node)) length = AddLength(length, node.Value.Length, budget); }
        return (int)length;
    }
    private static string CopyText(XmlNode node, int length, bool run, IXPathBudget budget)
    {
        if (length == 0) return "";
        return string.Create(length, (node, run, budget), static (destination, state) =>
        {
            int offset = 0;
            var current = state.run ? state.node : state.node.FirstChild;
            while (!current.IsDefault && (!state.run || XPathItem.IsText(current)))
            {
                state.budget.ChargeOperations(1);
                if (XPathItem.IsText(current))
                {
                    var text = current.Value.AsSpan();
                    while (!text.IsEmpty)
                    {
                        int count = Math.Min(256, text.Length); state.budget.ChargeOperations(count);
                        text[..count].CopyTo(destination[offset..]); offset += count; text = text[count..];
                    }
                }
                current = state.run ? current.NextSibling : !current.FirstChild.IsDefault ? current.FirstChild : NextAfter(current, state.node, state.budget);
            }
        });
    }
    private void Preflight(int value, string local, string uri, string prefix, IXPathBudget budget)
    {
        long name = local.Length + (long)prefix.Length + (prefix.Length == 0 ? 0 : 1);
        long extra = value + (long)local.Length + uri.Length + prefix.Length + name;
        if (name > int.MaxValue || characters > long.MaxValue - extra || entries.Count >= Array.MaxLength) throw Limit();
        budget.CheckStringSize(value); budget.CheckStringSize(local.Length); budget.CheckStringSize(uri.Length);
        budget.CheckStringSize(prefix.Length); budget.CheckStringSize((int)name);
        inputBudget.CheckInputSize(entries.Count + 1, characters + extra);
        // Charge all metadata scans before creating the item/name. Aggregated
        // values are additionally charged while copied in bounded chunks.
        Poll(value, budget); ContextScan.Poll(local, budget.CancellationToken, budget);
        ContextScan.Poll(uri, budget.CancellationToken, budget); ContextScan.Poll(prefix, budget.CancellationToken, budget);
        Poll((int)name, budget); characters += extra;
    }
    private static void Poll(int length, IXPathBudget budget)
    { for (int i = 0; i < length;) { int count = Math.Min(256, length - i); budget.ChargeOperations(count); i += count; } }
    private int Add(ItemIdentity identity, string value, string local, string uri, string prefix, int parent, IXPathBudget budget)
    {
        ChargeIdentity(identity, budget);
        int index = entries.Count;
        entries.Add(new(new(identity, value, local, uri, prefix), parent));
        if (identity.Kind != XPathItemKind.Namespace) order.Add(identity, index);
        return index;
    }
    private int LookupNode(XmlNode node, IXPathBudget budget)
    { budget.ChargeOperations(1); return order[new(node.Kind == XmlNodeKind.Document ? XPathItemKind.Root : XPathItemKind.Element, node, default, null)]; }
    private void CaptureNamespaces(XmlNode element, int parent, IXPathBudget budget)
    {
        int start = entries.Count;
        Preflight(TreeValidation.XmlUri.Length, "xml", "", "", budget);
        Add(new(XPathItemKind.Namespace, element, default, "xml"), TreeValidation.XmlUri, "xml", "", "", parent, budget);
        for (var owner = element; !owner.IsDefault; owner = owner.Parent)
        {
            budget.ChargeOperations(1);
            foreach (var declaration in owner.NamespaceDeclarations)
            {
                budget.ChargeOperations(1);
                if (Equal(declaration.Prefix, "xml", budget) || Equal(declaration.Prefix, "xmlns", budget) || declaration.Value.Length == 0) continue;
                bool shadowed = false;
                for (var nearer = element; nearer != owner && !shadowed; nearer = nearer.Parent)
                {
                    budget.ChargeOperations(1);
                    foreach (var prior in nearer.NamespaceDeclarations)
                    { budget.ChargeOperations(1); if (Equal(prior.Prefix, declaration.Prefix, budget)) { shadowed = true; break; } }
                }
                if (shadowed) continue;
                Preflight(declaration.Value.Length, declaration.Prefix, "", "", budget);
                Add(new(XPathItemKind.Namespace, element, default, declaration.Prefix), declaration.Value, declaration.Prefix, "", "", parent, budget);
            }
        }
        SortSection(start, budget, true);
    }
    private void SortSection(int start, IXPathBudget budget, bool namespaces)
    {
        // Insertion sort avoids a comparer that can wrap cancellation/limit
        // exceptions; every comparison and movement is charged explicitly.
        for (int i = start + 1; i < entries.Count; i++)
        {
            var entry = entries[i]; int j = i;
            while (j > start)
            {
                budget.ChargeOperations(1);
                int cmp = namespaces ? Compare(entries[j - 1].Item.LocalName, entry.Item.LocalName, budget) : Compare(entries[j - 1].Item.NamespaceUri, entry.Item.NamespaceUri, budget);
                if (cmp == 0 && !namespaces) cmp = Compare(entries[j - 1].Item.LocalName, entry.Item.LocalName, budget);
                if (cmp <= 0) break;
                budget.ChargeOperations(1); entries[j] = entries[j - 1]; j--;
            }
            entries[j] = entry;
        }
    }
    private static int Compare(ReadOnlySpan<char> left, ReadOnlySpan<char> right, IXPathBudget budget)
    {
        int length = Math.Min(left.Length, right.Length);
        for (int i = 0; i < length; i++) { budget.ChargeOperations(2); if (left[i] != right[i]) return left[i].CompareTo(right[i]); }
        return left.Length.CompareTo(right.Length);
    }
    private static bool Equal(string left, string right, IXPathBudget budget) => Compare(left, right, budget) == 0;
    private static void ChargeIdentity(ItemIdentity identity, IXPathBudget budget)
    { budget.ChargeOperations(1); if (identity.NamespacePrefix is { } prefix) ContextScan.Poll(prefix, budget.CancellationToken, budget); }
    private int Index(XPathItem item, IXPathBudget budget, string code = "XPATH-NODESET")
    {
        budget.ChargeOperations(0);
        if (item.IsDefault) throw new ArgumentException("Default XPath item.", nameof(item));
        ChargeIdentity(item.Identity, budget);
        if (item.Identity.Kind == XPathItemKind.Namespace)
        {
            budget.ChargeOperations(1);
            if (order.TryGetValue(new(XPathItemKind.Element, item.Identity.Node, default, null), out int owner))
                for (int i = owner + 1; i < entries.Count && entries[i].Item.Kind == XPathItemKind.Namespace; i++)
                {
                    budget.ChargeOperations(1);
                    if (Equal(entries[i].Item.LocalName, item.Identity.NamespacePrefix!, budget)) return i;
                }
        }
        else if (order.TryGetValue(item.Identity, out int index)) return index;
        throw Error(code, "Item identity is absent from the entry snapshot.");
    }
    private XPathEvaluationException Error(string code, string message) => new(new(code, XPathDiagnosticCategory.Evaluation, message, span));
    internal XPathItem Rebind(XPathItem item, IXPathBudget budget, bool context = false) => entries[Index(item, budget, context ? "XPATH-CONTEXT" : "XPATH-NODESET")].Item;
    internal int OrderIndex(XPathItem item, IXPathBudget budget) => Index(item, budget);
    public int CompareDocumentOrder(XPathItem left, XPathItem right, IXPathBudget budget)
    { ArgumentNullException.ThrowIfNull(budget); int a = Index(left, budget), b = Index(right, budget); budget.ChargeOperations(1); return a.CompareTo(b); }

    public IReadOnlyList<XPathItem> SelectAxis(XPathItem item, XPathAxis axis, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget); ModelGuard.Enum(axis);
        int at = Index(item, budget), parent = entries[at].Parent;
        var result = new List<XPathItem>();
        bool member = entries[at].Item.Kind is XPathItemKind.Attribute or XPathItemKind.Namespace;
        // These axes need only their captured interval, never the whole input.
        // End includes member leaves; child traversal skips each child's subtree.
        if (axis is XPathAxis.Self or XPathAxis.Parent)
        {
            int selected = axis == XPathAxis.Self ? at : parent;
            if (selected >= 0) Append(result, entries[selected].Item, budget);
            budget.ChargeOperations(0); return result.AsReadOnly();
        }
        if (axis is XPathAxis.Child or XPathAxis.Attribute or XPathAxis.Namespace or XPathAxis.Descendant or XPathAxis.DescendantOrSelf)
        {
            if (axis == XPathAxis.DescendantOrSelf) Append(result, entries[at].Item, budget);
            if (!member)
                for (int i = at + 1; i <= entries[at].End;)
                {
                    budget.ChargeOperations(1);
                    var kind = entries[i].Item.Kind;
                    bool ordinary = kind is not (XPathItemKind.Attribute or XPathItemKind.Namespace);
                    if (axis is XPathAxis.Attribute or XPathAxis.Namespace)
                    {
                        if (ordinary) break;
                        if (entries[i].Parent == at && kind == (axis == XPathAxis.Attribute ? XPathItemKind.Attribute : XPathItemKind.Namespace))
                            Append(result, entries[i].Item, budget);
                        i++;
                    }
                    else
                    {
                        if (ordinary) Append(result, entries[i].Item, budget);
                        i = axis == XPathAxis.Child ? entries[i].End + 1 : i + 1;
                    }
                }
            budget.ChargeOperations(0); return result.AsReadOnly();
        }
        bool reverse = axis is XPathAxis.Ancestor or XPathAxis.AncestorOrSelf or XPathAxis.Preceding or XPathAxis.PrecedingSibling;
        // Structural intervals include namespace/attribute leaves, which are
        // explicitly excluded from ordinary descendant/following/preceding axes.
        for (int k = 0; k < entries.Count; k++)
        {
            int i = reverse ? entries.Count - 1 - k : k;
            budget.ChargeOperations(1);
            bool ordinary = entries[i].Item.Kind is not (XPathItemKind.Attribute or XPathItemKind.Namespace);
            bool ancestor = ordinary && i < at && entries[i].End >= at;
            bool include = axis switch
            {
                XPathAxis.Self => i == at,
                XPathAxis.Parent => i == parent,
                XPathAxis.Child => !member && ordinary && entries[i].Parent == at,
                XPathAxis.Attribute => entries[i].Item.Kind == XPathItemKind.Attribute && entries[i].Parent == at,
                XPathAxis.Namespace => entries[i].Item.Kind == XPathItemKind.Namespace && entries[i].Parent == at,
                XPathAxis.Descendant => !member && ordinary && i > at && i <= entries[at].End,
                XPathAxis.DescendantOrSelf => i == at || (!member && ordinary && i > at && i <= entries[at].End),
                XPathAxis.Ancestor => ancestor,
                XPathAxis.AncestorOrSelf => i == at || ancestor,
                XPathAxis.FollowingSibling => !member && ordinary && entries[i].Parent == parent && i > at,
                XPathAxis.PrecedingSibling => !member && ordinary && entries[i].Parent == parent && i < at,
                XPathAxis.Following => ordinary && i > (member ? at : entries[at].End),
                XPathAxis.Preceding => ordinary && i < at && !ancestor,
                _ => false
            };
            if (include) Append(result, entries[i].Item, budget);
        }
        budget.ChargeOperations(0); return result.AsReadOnly();
    }
    private void Append(List<XPathItem> result, XPathItem item, IXPathBudget budget)
    {
        if (result.Count >= Array.MaxLength) throw Limit();
        budget.CheckNodeSetSize(result.Count + 1); budget.ChargeOperations(1); result.Add(item);
    }
    internal IReadOnlyList<XPathItem> Normalize(IReadOnlyList<XPathItem> input, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(budget);
        budget.CheckNodeSetSize(input.Count);
        // Exact-sized owned scratch; sorted input is linear. Reverse/unordered
        // input uses metered iterative merge sort, avoiding quadratic insertion
        // and comparison callbacks whose exceptions a framework sort can wrap.
        var indexes = new int[input.Count]; bool sorted = true;
        for (int k = 0; k < input.Count; k++)
        {
            budget.ChargeOperations(1); indexes[k] = Index(input[k], budget);
            if (k > 0) { budget.ChargeOperations(1); if (indexes[k - 1] > indexes[k]) sorted = false; }
        }
        if (!sorted)
        {
            var scratch = new int[indexes.Length];
            for (long width = 1; width < indexes.Length; width *= 2)
            {
                for (long start = 0; start < indexes.Length; start += width * 2)
                {
                    int left = (int)start, middle = (int)Math.Min(start + width, indexes.Length);
                    int right = middle, end = (int)Math.Min(start + width * 2, indexes.Length);
                    for (int output = left; output < end; output++)
                    {
                        budget.ChargeOperations(3);
                        scratch[output] = left < middle && (right == end || indexes[left] <= indexes[right])
                            ? indexes[left++] : indexes[right++];
                    }
                }
                (indexes, scratch) = (scratch, indexes);
            }
        }
        var result = new List<XPathItem>();
        int last = -1;
        foreach (int index in indexes)
        {
            budget.ChargeOperations(1);
            if (index == last) continue;
            Append(result, entries[index].Item, budget); last = index;
        }
        budget.ChargeOperations(0); return result.AsReadOnly();
    }
    public IReadOnlyList<XPathItem> SelectId(string id, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(id); ArgumentNullException.ThrowIfNull(budget);
        budget.ChargeOperations(0); budget.CheckStringSize(id.Length);
        var result = new List<XPathItem>(); int last = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            budget.ChargeOperations(1); if (!entries[i].IsId) continue;
            bool match = false;
            for (int start = 0; start < id.Length;)
            {
                budget.ChargeOperations(1); if (XPathConversions.IsSpace(id[start])) { start++; continue; }
                int end = start;
                while (end < id.Length) { budget.ChargeOperations(1); if (XPathConversions.IsSpace(id[end])) break; end++; }
                if (Compare(id.AsSpan(start, end - start), entries[i].Item.StringValue, budget) == 0) match = true;
                start = end;
            }
            int parent = entries[i].Parent;
            if (match && parent != last) { Append(result, entries[parent].Item, budget); last = parent; }
        }
        budget.ChargeOperations(0); return result.AsReadOnly();
    }
    public string? FindLanguage(XPathItem item, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget); int at = Index(item, budget);
        if (entries[at].Item.Kind != XPathItemKind.Element) at = entries[at].Parent;
        while (at >= 0) { budget.ChargeOperations(1); if (entries[at].Language is { } language) return language; at = entries[at].Parent; }
        return null;
    }
}

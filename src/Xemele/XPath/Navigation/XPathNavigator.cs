using System.Numerics;
using Xemele.Parsing.Dtd;

namespace Xemele.XPath.Internal;

// A query owns this captured data. Live handles are used only as identity keys
// after construction. The caller must exclude mutation while capture runs.
internal sealed class XPathNavigator : IXPathNavigator
{
    private sealed class Entry(XPathItem item, int parent)
    {
        internal readonly XPathItem Item = item;
        internal readonly XPathItemKind Kind = item.Kind;
        internal readonly string Local = item.LocalName;
        internal readonly string Uri = item.NamespaceUri;
        internal readonly int Parent = parent;
        internal int End;
        internal bool IsId;
        internal string? Language;
    }
    // Immutable once published; shared by every evaluation of the same document revision.
    private sealed class Captured(Entry[] entries, Dictionary<ItemIdentity, int> order, long characters, long operations, int maxString, long revision, DtdModel? dtd)
    {
        internal readonly Entry[] Entries = entries;
        internal readonly Dictionary<ItemIdentity, int> Order = order;
        internal readonly long Characters = characters;
        internal readonly long Operations = operations;
        internal readonly int MaxString = maxString;
        internal readonly long Revision = revision;
        internal readonly DtdModel? Dtd = dtd;
    }
    private readonly Entry[] entries;
    private List<Entry> building = null!;
    private readonly Dictionary<ItemIdentity, int> order;
    private int maxString;
    private readonly XPathSourceSpan span;
    private readonly XPathBudget inputBudget;
    private long characters;
    internal int InputItems => entries.Length;
    internal long InputCharacters => characters;
    public XPathItem Root => entries[0].Item;

    internal XPathNavigator(XmlDocument document, XPathBudget budget, XPathSourceSpan span = default)
        : this(document, budget, budget, span) { }

    // Separate size accounting and work charging supports hosts that forward
    // the shared budget through a metered adapter. Normal evaluation uses the
    // same XPathBudget for both roles.
    internal XPathNavigator(XmlDocument document, XPathBudget inputBudget, IXPathBudget budget, XPathSourceSpan span = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(inputBudget);
        ArgumentNullException.ThrowIfNull(budget);
        this.inputBudget = inputBudget;
        this.span = ModelGuard.Span(span);
        budget.ChargeOperations(0);
        if (document.UnresolvedReferences.Count != 0 || document.HasIncompleteAttributes)
        {
            throw Error("XPATH-INCOMPLETE", "Document has unresolved references or incomplete attributes.");
        }

        var cacheable = ReferenceEquals(inputBudget, budget) && document.ContentRevision >= 0;
        var revision = document.ContentRevision;
        var startOperations = inputBudget.Operations;
        if (cacheable && document.XPathSnapshot is Captured cached && cached.Revision == revision && ReferenceEquals(cached.Dtd, document.Dtd) &&
            inputBudget.TryReplayCapture(cached.Operations, cached.Entries.Length, cached.Characters, cached.MaxString))
        {
            entries = cached.Entries;
            order = cached.Order;
            characters = cached.Characters;
            return;
        }

        building = [];
        order = [];
        XmlNode root = document.DocumentNode, node = root;
        do
        {
            budget.ChargeOperations(1);
            var parent = node.Parent.IsDefault ? -1 : LookupNode(node.Parent, budget);
            if (XPathItem.IsText(node))
            {
                // Visit every physical member, including empty omitted runs.
                XmlNode first = node, after = node;
                long length = 0;
                do
                {
                    if (after != first)
                    {
                        budget.ChargeOperations(1);
                    }

                    length = AddLength(length, after.Value.Length, budget);
                    after = after.NextSibling;
                } while (XPathItem.IsText(after));
                if (length != 0)
                {
                    Preflight((int)length, "", "", "", budget);
                    var text = CopyText(first, (int)length, true, budget);
                    Add(new(XPathItemKind.Text, first, default, null), text, "", "", "", parent, budget);
                }
                node = after.IsDefault ? NextAfter(first.Parent, root, budget) : after;
                continue;
            }
            var kind = node.Kind switch
            {
                XmlNodeKind.Document => XPathItemKind.Root,
                XmlNodeKind.Element => XPathItemKind.Element,
                XmlNodeKind.Comment => XPathItemKind.Comment,
                XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction,
                _ => throw new ArgumentException("Unsupported input node.", nameof(document))
            };
            var local = kind is XPathItemKind.Element or XPathItemKind.ProcessingInstruction ? node.LocalName : "";
            var uri = kind == XPathItemKind.Element ? node.NamespaceUri : "";
            var prefix = kind == XPathItemKind.Element ? node.Prefix : "";
            var aggregate = kind is XPathItemKind.Root or XPathItemKind.Element;
            var valueLength = aggregate ? TextLength(node, budget) : node.Value.Length;
            Preflight(valueLength, local, uri, prefix, budget);
            var value = aggregate ? CopyText(node, valueLength, false, budget) : node.Value;
            var index = Add(new(kind, node, default, null), value, local, uri, prefix, parent, budget);
            if (kind == XPathItemKind.Element)
            {
                CaptureNamespaces(node, index, budget);
                var start = building.Count;
                foreach (var attribute in node.Attributes)
                {
                    budget.ChargeOperations(1);
                    Preflight(attribute.Value.Length, attribute.LocalName, attribute.NamespaceUri, attribute.Prefix, budget);
                    var at = Add(new(XPathItemKind.Attribute, node, attribute, null), attribute.Value,
                        attribute.LocalName, attribute.NamespaceUri, attribute.Prefix, index, budget);
                    if (Equal(attribute.NamespaceUri, TreeValidation.XmlUri, budget) && Equal(attribute.LocalName, "lang", budget))
                    {
                        building[index].Language = attribute.Value;
                    }

                    if (document.Dtd is { } dtd)
                    {
                        foreach (var declaration in dtd.Attributes)
                        {
                            budget.ChargeOperations(1);
                            if (declaration.Type == DtdAttributeType.Id && Equal(declaration.ElementName, building[index].Item.Name, budget) &&
                                Equal(declaration.Name, building[at].Item.Name, budget))
                            {
                                building[at].IsId = true;
                            }
                        }
                    }
                }
                SortSection(start, budget, false);
            }
            node = !node.FirstChild.IsDefault ? node.FirstChild : NextAfter(node, root, budget);
        } while (!node.IsDefault);
        // Rebuild indexes after deterministic namespace/attribute sorting; these
        // leaf sections never move a captured parent's index.
        order.Clear();
        for (var i = 0; i < building.Count; i++)
        {
            ChargeIdentity(building[i].Item.Identity, budget);
            if (building[i].Item.Kind != XPathItemKind.Namespace)
            {
                order.Add(building[i].Item.Identity, i);
            }

            building[i].End = i;
        }
        for (var i = building.Count - 1; i > 0; i--)
        {
            budget.ChargeOperations(1);
            var p = building[i].Parent;
            building[p].End = Math.Max(building[p].End, building[i].End);
        }
        budget.ChargeOperations(0);
        entries = building.ToArray();
        building = null!;
        if (cacheable && document.ContentRevision == revision)
        {
            document.StoreXPathSnapshot(new Captured(entries, order, characters, inputBudget.Operations - startOperations, maxString, revision, document.Dtd));
        }
    }

    private static XmlNode NextAfter(XmlNode node, XmlNode root, IXPathBudget budget)
    {
        while (node != root && node.NextSibling.IsDefault)
        {
            budget.ChargeOperations(1);
            node = node.Parent;
        }
        budget.ChargeOperations(1);
        return node == root ? default : node.NextSibling;
    }
    private long AddLength(long length, int extra, IXPathBudget budget)
    {
        if (length > int.MaxValue - extra)
        {
            budget.CheckStringSize(int.MaxValue);
            throw Limit();
        }
        length += extra;
        maxString = Math.Max(maxString, (int)length);
        budget.CheckStringSize((int)length);
        return length;
    }
    private XPathEvaluationException Limit() => new(new("XPATH-EVALUATION-LIMIT", XPathDiagnosticCategory.EvaluationLimit, "XPath representational capacity exceeded.", span));
    private int TextLength(XmlNode owner, IXPathBudget budget)
    {
        long length = 0;
        for (var node = owner.FirstChild; !node.IsDefault; node = !node.FirstChild.IsDefault ? node.FirstChild : NextAfter(node, owner, budget))
        {
            budget.ChargeOperations(1);
            if (XPathItem.IsText(node))
            {
                length = AddLength(length, node.Value.Length, budget);
            }
        }
        return (int)length;
    }
    private static string CopyText(XmlNode node, int length, bool run, IXPathBudget budget)
    {
        if (length == 0)
        {
            return "";
        }

        return string.Create(length, (node, run, budget), static (destination, state) =>
        {
            var offset = 0;
            var current = state.run ? state.node : state.node.FirstChild;
            while (!current.IsDefault && (!state.run || XPathItem.IsText(current)))
            {
                state.budget.ChargeOperations(1);
                if (XPathItem.IsText(current))
                {
                    var text = current.Value.AsSpan();
                    while (!text.IsEmpty)
                    {
                        var count = Math.Min(256, text.Length);
                        state.budget.ChargeOperations(count);
                        text[..count].CopyTo(destination[offset..]);
                        offset += count;
                        text = text[count..];
                    }
                }
                current = state.run ? current.NextSibling : !current.FirstChild.IsDefault ? current.FirstChild : NextAfter(current, state.node, state.budget);
            }
        });
    }
    private void Preflight(int value, string local, string uri, string prefix, IXPathBudget budget)
    {
        var name = local.Length + (long)prefix.Length + (prefix.Length == 0 ? 0 : 1);
        var extra = value + (long)local.Length + uri.Length + prefix.Length + name;
        if (name > int.MaxValue || characters > long.MaxValue - extra || building.Count >= Array.MaxLength)
        {
            throw Limit();
        }

        maxString = Math.Max(maxString, Math.Max(value, Math.Max(local.Length, Math.Max(uri.Length, Math.Max(prefix.Length, (int)name)))));
        budget.CheckStringSize(value);
        budget.CheckStringSize(local.Length);
        budget.CheckStringSize(uri.Length);
        budget.CheckStringSize(prefix.Length);
        budget.CheckStringSize((int)name);
        inputBudget.CheckInputSize(building.Count + 1, characters + extra);
        // Charge all metadata scans before creating the item/name. Aggregated
        // values are additionally charged while copied in bounded chunks.
        Poll(value, budget);
        ContextScan.Poll(local, budget.CancellationToken, budget);
        ContextScan.Poll(uri, budget.CancellationToken, budget);
        ContextScan.Poll(prefix, budget.CancellationToken, budget);
        Poll((int)name, budget);
        characters += extra;
    }
    private static void Poll(int length, IXPathBudget budget)
    {
        for (var i = 0; i < length;)
        {
            var count = Math.Min(256, length - i);
            budget.ChargeOperations(count);
            i += count;
        }
    }
    private int Add(ItemIdentity identity, string value, string local, string uri, string prefix, int parent, IXPathBudget budget)
    {
        ChargeIdentity(identity, budget);
        var index = building.Count;
        building.Add(new(new(identity, value, local, uri, prefix), parent));
        if (identity.Kind != XPathItemKind.Namespace)
        {
            order.Add(identity, index);
        }

        return index;
    }
    private int LookupNode(XmlNode node, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        return order[new(node.Kind == XmlNodeKind.Document ? XPathItemKind.Root : XPathItemKind.Element, node, default, null)];
    }
    private void CaptureNamespaces(XmlNode element, int parent, IXPathBudget budget)
    {
        var start = building.Count;
        Preflight(TreeValidation.XmlUri.Length, "xml", "", "", budget);
        Add(new(XPathItemKind.Namespace, element, default, "xml"), TreeValidation.XmlUri, "xml", "", "", parent, budget);
        for (var owner = element; !owner.IsDefault; owner = owner.Parent)
        {
            budget.ChargeOperations(1);
            foreach (var declaration in owner.NamespaceDeclarations)
            {
                budget.ChargeOperations(1);
                if (Equal(declaration.Prefix, "xml", budget) || Equal(declaration.Prefix, "xmlns", budget) || declaration.Value.Length == 0)
                {
                    continue;
                }

                var shadowed = false;
                for (var nearer = element; nearer != owner && !shadowed; nearer = nearer.Parent)
                {
                    budget.ChargeOperations(1);
                    foreach (var prior in nearer.NamespaceDeclarations)
                    {
                        budget.ChargeOperations(1);
                        if (Equal(prior.Prefix, declaration.Prefix, budget))
                        {
                            shadowed = true;
                            break;
                        }
                    }
                }
                if (shadowed)
                {
                    continue;
                }

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
        for (var i = start + 1; i < building.Count; i++)
        {
            var entry = building[i];
            var j = i;
            while (j > start)
            {
                budget.ChargeOperations(1);
                var cmp = namespaces ? Compare(building[j - 1].Item.LocalName, entry.Item.LocalName, budget) : Compare(building[j - 1].Item.NamespaceUri, entry.Item.NamespaceUri, budget);
                if (cmp == 0 && !namespaces)
                {
                    cmp = Compare(building[j - 1].Item.LocalName, entry.Item.LocalName, budget);
                }

                if (cmp <= 0)
                {
                    break;
                }

                budget.ChargeOperations(1);
                building[j] = building[j - 1];
                j--;
            }
            building[j] = entry;
        }
    }
    private static int Compare(ReadOnlySpan<char> left, ReadOnlySpan<char> right, IXPathBudget budget)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            budget.ChargeOperations(2);
            if (left[i] != right[i])
            {
                return left[i].CompareTo(right[i]);
            }
        }
        return left.Length.CompareTo(right.Length);
    }
    private static bool Equal(string left, string right, IXPathBudget budget) => Compare(left, right, budget) == 0;
    private static void ChargeIdentity(ItemIdentity identity, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        if (identity.NamespacePrefix is { } prefix)
        {
            ContextScan.Poll(prefix, budget.CancellationToken, budget);
        }
    }
    private int Index(XPathItem item, IXPathBudget budget, string code = "XPATH-NODESET")
    {
        budget.ChargeOperations(0);
        if (item.IsDefault)
        {
            throw new ArgumentException("Default XPath item.", nameof(item));
        }

        ChargeIdentity(item.Identity, budget);
        if (item.Identity.Kind == XPathItemKind.Namespace)
        {
            budget.ChargeOperations(1);
            if (order.TryGetValue(new(XPathItemKind.Element, item.Identity.Node, default, null), out var owner))
            {
                for (var i = owner + 1; i < entries.Length && entries[i].Item.Kind == XPathItemKind.Namespace; i++)
                {
                    budget.ChargeOperations(1);
                    if (Equal(entries[i].Item.LocalName, item.Identity.NamespacePrefix!, budget))
                    {
                        return i;
                    }
                }
            }
        }
        else if (order.TryGetValue(item.Identity, out var index))
        {
            return index;
        }

        throw Error(code, "Item identity is absent from the entry snapshot.");
    }
    private XPathEvaluationException Error(string code, string message) => new(new(code, XPathDiagnosticCategory.Evaluation, message, span));
    internal XPathItem Rebind(XPathItem item, IXPathBudget budget, bool context = false) => entries[Index(item, budget, context ? "XPATH-CONTEXT" : "XPATH-NODESET")].Item;
    internal int OrderIndex(XPathItem item, IXPathBudget budget) => Index(item, budget);
    public int CompareDocumentOrder(XPathItem left, XPathItem right, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        int a = Index(left, budget), b = Index(right, budget);
        budget.ChargeOperations(1);
        return a.CompareTo(b);
    }

    internal XPathItem ItemAt(int index) => entries[index].Item;
    // Charge of Index() for an already-resolved entry. Ordinary entries cost one
    // operation, returned for bulk posting; namespace entries replay the full lookup.
    internal long LookupCharge(int index, IXPathBudget budget)
    {
        if (entries[index].Kind != XPathItemKind.Namespace)
        {
            return 1;
        }

        Index(entries[index].Item, budget);
        return 0;
    }
    internal int IndexOfUnmetered(XPathItem item) => Index(item, UnmeteredBudget.Instance);
    private sealed class UnmeteredBudget : IXPathBudget
    {
        internal static readonly UnmeteredBudget Instance = new();
        public CancellationToken CancellationToken => default;
        public void ChargeOperations(long amount) { }
        public void CheckNodeSetSize(int count) { }
        public void CheckStringSize(int utf16Characters) { }
    }
    private interface IAxisSink
    {
        void Accept(int index);
    }
    private struct ListSink(List<XPathItem> result, Entry[] nodes, IXPathBudget budget) : IAxisSink
    {
        public readonly void Accept(int index)
        {
            budget.CheckNodeSetSize(result.Count + 1);
            result.Add(nodes[index].Item);
        }
    }
    // Applies a step's node test to each axis member as it is visited and adds
    // matches to the selection, counting the per-candidate charges.
    private struct StepSink(Entry[] nodes, XPathNodeSelection selected, XPathNodeTest test, XPathItemKind principal) : IAxisSink
    {
        internal int Count;
        internal long Pending;
        public void Accept(int index)
        {
            Count++;
            Pending++;
            var entry = nodes[index];
            bool match;
            switch (test.Kind)
            {
                case XPathNodeTestKind.Node:
                    match = true;
                    break;
                case XPathNodeTestKind.Text:
                    match = entry.Kind == XPathItemKind.Text;
                    break;
                case XPathNodeTestKind.Comment:
                    match = entry.Kind == XPathItemKind.Comment;
                    break;
                case XPathNodeTestKind.ProcessingInstruction:
                    match = entry.Kind == XPathItemKind.ProcessingInstruction;
                    if (match && test.ProcessingInstructionTarget is { } target)
                    {
                        match = EqualCharge(entry.Local, target, ref Pending);
                    }
                    break;
                case XPathNodeTestKind.AnyPrincipal:
                    match = entry.Kind == principal;
                    break;
                case XPathNodeTestKind.NamespaceWildcard:
                    match = entry.Kind == principal && EqualCharge(entry.Uri, test.Name.NamespaceUri, ref Pending);
                    break;
                case XPathNodeTestKind.Name:
                    match = entry.Kind == principal && EqualCharge(entry.Uri, test.Name.NamespaceUri, ref Pending) && EqualCharge(entry.Local, test.Name.LocalName, ref Pending);
                    break;
                default:
                    throw new InvalidOperationException("Unknown node test.");
            }
            if (match)
            {
                selected.AddDeferred(index);
            }
        }
    }
    // Node test plus direct merge for predicate-free steps. Each match is charged
    // as it was when it passed through a per-context selection and then a merge.
    private struct MergeSink(Entry[] nodes, XPathNodeSelection merged, XPathNodeTest test, XPathItemKind principal, XPathNavigator navigator, XPathBudget budget) : IAxisSink
    {
        internal int Count;
        internal int Matches;
        internal long Pending;
        public void Accept(int index)
        {
            Count++;
            Pending++;
            var entry = nodes[index];
            bool match;
            switch (test.Kind)
            {
                case XPathNodeTestKind.Node:
                    match = true;
                    break;
                case XPathNodeTestKind.Text:
                    match = entry.Kind == XPathItemKind.Text;
                    break;
                case XPathNodeTestKind.Comment:
                    match = entry.Kind == XPathItemKind.Comment;
                    break;
                case XPathNodeTestKind.ProcessingInstruction:
                    match = entry.Kind == XPathItemKind.ProcessingInstruction;
                    if (match && test.ProcessingInstructionTarget is { } target)
                    {
                        match = EqualCharge(entry.Local, target, ref Pending);
                    }
                    break;
                case XPathNodeTestKind.AnyPrincipal:
                    match = entry.Kind == principal;
                    break;
                case XPathNodeTestKind.NamespaceWildcard:
                    match = entry.Kind == principal && EqualCharge(entry.Uri, test.Name.NamespaceUri, ref Pending);
                    break;
                case XPathNodeTestKind.Name:
                    match = entry.Kind == principal && EqualCharge(entry.Uri, test.Name.NamespaceUri, ref Pending) && EqualCharge(entry.Local, test.Name.LocalName, ref Pending);
                    break;
                default:
                    throw new InvalidOperationException("Unknown node test.");
            }
            if (match)
            {
                Matches++;
                merged.AddUnique(index, navigator, budget);
            }
            if (Pending >= 4096 || merged.HasLargePending)
            {
                budget.CheckNodeSetSize(Count);
                budget.ChargeOperations(Pending);
                Pending = 0;
                merged.Flush(budget);
            }
        }
    }
    // Streams axis members to the sink in axis order and returns the operations
    // to charge: one per candidate visited plus one per member accepted.
    // With a meter every unit of work is charged where it occurs; otherwise it is
    // returned in bulk for the caller to post.
    private static void Tick(ref long pending, IXPathBudget? meter)
    {
        if (meter is null)
        {
            pending++;
        }
        else
        {
            meter.ChargeOperations(1);
        }
    }
    private long Candidates<TSink>(int at, XPathAxis axis, ref TSink sink, IXPathBudget? meter = null) where TSink : struct, IAxisSink
    {
        var nodes = entries;
        long pending = 0;
        int parent = nodes[at].Parent;
        var member = nodes[at].Kind is XPathItemKind.Attribute or XPathItemKind.Namespace;
        // These axes need only their captured interval, never the whole input.
        // End includes member leaves; child traversal skips each child's subtree.
        if (axis is XPathAxis.Self or XPathAxis.Parent)
        {
            var selected = axis == XPathAxis.Self ? at : parent;
            if (selected >= 0)
            {
                sink.Accept(selected);
                Tick(ref pending, meter);
            }

            return pending;
        }
        if (axis is XPathAxis.Child or XPathAxis.Attribute or XPathAxis.Namespace or XPathAxis.Descendant or XPathAxis.DescendantOrSelf)
        {
            if (axis == XPathAxis.DescendantOrSelf)
            {
                sink.Accept(at);
                Tick(ref pending, meter);
            }

            if (!member)
            {
                var end = nodes[at].End;
                for (var i = at + 1; i <= end;)
                {
                    Tick(ref pending, meter);
                    var kind = nodes[i].Kind;
                    var ordinary = kind is not (XPathItemKind.Attribute or XPathItemKind.Namespace);
                    if (axis is XPathAxis.Attribute or XPathAxis.Namespace)
                    {
                        if (ordinary)
                        {
                            break;
                        }

                        if (nodes[i].Parent == at && kind == (axis == XPathAxis.Attribute ? XPathItemKind.Attribute : XPathItemKind.Namespace))
                        {
                            sink.Accept(i);
                            Tick(ref pending, meter);
                        }

                        i++;
                    }
                    else
                    {
                        if (ordinary)
                        {
                            sink.Accept(i);
                            Tick(ref pending, meter);
                        }

                        i = axis == XPathAxis.Child ? nodes[i].End + 1 : i + 1;
                    }
                }
            }

            return pending;
        }
        var reverse = axis is XPathAxis.Ancestor or XPathAxis.AncestorOrSelf or XPathAxis.Preceding or XPathAxis.PrecedingSibling;
        // Structural intervals include namespace/attribute leaves, which are
        // explicitly excluded from ordinary descendant/following/preceding axes.
        for (var k = 0; k < nodes.Length; k++)
        {
            var i = reverse ? nodes.Length - 1 - k : k;
            Tick(ref pending, meter);
            var ordinary = nodes[i].Kind is not (XPathItemKind.Attribute or XPathItemKind.Namespace);
            var ancestor = ordinary && i < at && nodes[i].End >= at;
            var include = axis switch
            {
                XPathAxis.Child => !member && ordinary && nodes[i].Parent == at,
                XPathAxis.Attribute => nodes[i].Kind == XPathItemKind.Attribute && nodes[i].Parent == at,
                XPathAxis.Namespace => nodes[i].Kind == XPathItemKind.Namespace && nodes[i].Parent == at,
                XPathAxis.Descendant => !member && ordinary && i > at && i <= nodes[at].End,
                XPathAxis.Ancestor => ancestor,
                XPathAxis.AncestorOrSelf => i == at || ancestor,
                XPathAxis.FollowingSibling => !member && ordinary && nodes[i].Parent == parent && i > at,
                XPathAxis.PrecedingSibling => !member && ordinary && nodes[i].Parent == parent && i < at,
                XPathAxis.Following => ordinary && i > (member ? at : nodes[at].End),
                XPathAxis.Preceding => ordinary && i < at && !ancestor,
                _ => false
            };
            if (include)
            {
                sink.Accept(i);
                Tick(ref pending, meter);
            }
        }
        return pending;
    }
    public IReadOnlyList<XPathItem> SelectAxis(XPathItem item, XPathAxis axis, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ModelGuard.Enum(axis);
        var at = Index(item, budget);
        var result = new List<XPathItem>();
        var sink = new ListSink(result, entries, budget);
        Candidates(at, axis, ref sink, budget);
        budget.ChargeOperations(0);
        return result.AsReadOnly();
    }
    // One step from one context entry: SelectAxis followed by the node test, adding
    // matches to selected. Charges equal the per-candidate item-based sequence.
    internal void SelectStep(int at, XPathStep step, XPathBudget budget, XPathNodeSelection selected)
    {
        var principal = step.Axis switch
        {
            XPathAxis.Attribute => XPathItemKind.Attribute,
            XPathAxis.Namespace => XPathItemKind.Namespace,
            _ => XPathItemKind.Element
        };
        var sink = new StepSink(entries, selected, step.Test, principal);
        // One for the context entry and its identity lookup, then the axis walk.
        var pending = 1 + LookupCharge(at, budget) + Candidates(at, step.Axis, ref sink);
        budget.CheckNodeSetSize(sink.Count);
        budget.ChargeOperations(pending + sink.Pending);
    }
    // Growth charges of a node-set filled one member at a time from empty: capacity
    // starts at four and doubles, each growth copying the existing members twice.
    private static long SelectionCharges(int members) =>
        members <= 4 ? members : members + 4L * (BitOperations.RoundUpToPowerOf2((uint)members) / 2) - 8;
    // A predicate-free step over every context entry, then document-order normalization.
    internal XPathNodeList EvaluateStep(XPathNodeList current, XPathStep step, XPathBudget budget)
    {
        budget.ChargeOperations(1);
        var merged = new XPathNodeSelection();
        merged.Presize(current.Count);
        var principal = step.Axis switch
        {
            XPathAxis.Attribute => XPathItemKind.Attribute,
            XPathAxis.Namespace => XPathItemKind.Namespace,
            _ => XPathItemKind.Element
        };
        var sink = new MergeSink(entries, merged, step.Test, principal, this, budget);
        for (var j = 0; j < current.Count; j++)
        {
            var at = current.IndexAt(j);
            sink.Count = 0;
            var matchesBefore = sink.Matches;
            var walk = 1 + LookupCharge(at, budget) + Candidates(at, step.Axis, ref sink);
            budget.CheckNodeSetSize(sink.Count);
            merged.AddPending(SelectionCharges(sink.Matches - matchesBefore));
            budget.ChargeOperations(walk + sink.Pending);
            sink.Pending = 0;
            if (merged.HasLargePending)
            {
                merged.Flush(budget);
            }
        }
        merged.Flush(budget);
        var normalized = NormalizeSelection(merged, budget);
        budget.CheckNodeSetSize(normalized.Count);
        budget.ChargeOperations(2L * normalized.Count);
        return normalized;
    }
    // Equal() charges one operation, plus two per code unit compared when lengths agree.
    private static bool EqualCharge(string left, string right, ref long pending)
    {
        pending++;
        if (left.Length != right.Length)
        {
            return false;
        }

        var common = left.AsSpan().CommonPrefixLength(right);
        var equal = common == left.Length;
        pending += 2L * (equal ? common : common + 1);
        return equal;
    }
    private XPathNodeList Seal(int[] indexes, int count, bool sorted, IXPathBudget budget)
    {
        if (!sorted)
        {
            var temporary = new int[count];
            var from = indexes;
            var to = temporary;
            for (long width = 1; width < count; width *= 2)
            {
                budget.ChargeOperations(3L * count);
                for (long start = 0; start < count; start += width * 2)
                {
                    int left = (int)start, middle = (int)Math.Min(start + width, count);
                    int right = middle, end = (int)Math.Min(start + width * 2, count);
                    for (var output = left; output < end; output++)
                    {
                        to[output] = left < middle && (right == end || from[left] <= from[right])
                            ? from[left++] : from[right++];
                    }
                }
                (from, to) = (to, from);
            }
            if (!ReferenceEquals(from, indexes))
            {
                Array.Copy(from, indexes, count);
            }
        }
        var unique = 0;
        for (var i = 0; i < count; i++)
        {
            if (unique == 0 || indexes[unique - 1] != indexes[i])
            {
                indexes[unique++] = indexes[i];
            }
        }
        // One operation per input index, one more per kept member.
        budget.ChargeOperations((long)count + unique);
        budget.CheckNodeSetSize(unique);
        return new(this, indexes, unique);
    }
    // Takes ownership of the selection's storage. Charges equal Normalize over its items.
    internal XPathNodeList NormalizeSelection(XPathNodeSelection selection, IXPathBudget budget)
    {
        var count = selection.Count;
        budget.CheckNodeSetSize(count);
        var indexes = selection.Items;
        var sorted = true;
        long pending = 0;
        for (var k = 0; k < count; k++)
        {
            pending += 1 + LookupCharge(indexes[k], budget);
            if (k > 0)
            {
                pending++;
                if (indexes[k - 1] > indexes[k])
                {
                    sorted = false;
                }
            }
        }
        budget.ChargeOperations(pending);
        return Seal(indexes, count, sorted, budget);
    }
    internal XPathNodeList Normalize(IReadOnlyList<XPathItem> input, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(budget);
        budget.CheckNodeSetSize(input.Count);
        if (input is XPathNodeList { } list && ReferenceEquals(list.Navigator, this))
        {
            // Already ordered and unique: only the charges remain.
            long pending = 0;
            for (var k = 0; k < list.Count; k++)
            {
                pending += 1 + LookupCharge(list.IndexAt(k), budget) + (k > 0 ? 1 : 0);
            }

            budget.ChargeOperations(pending + 2L * list.Count);
            budget.CheckNodeSetSize(list.Count);
            return list;
        }
        // Exact-sized owned scratch; sorted input is linear. Reverse/unordered
        // input uses metered iterative merge sort, avoiding quadratic insertion
        // and comparison callbacks whose exceptions a framework sort can wrap.
        var indexes = new int[input.Count];
        var sorted = true;
        for (var k = 0; k < input.Count; k++)
        {
            budget.ChargeOperations(1);
            indexes[k] = Index(input[k], budget);
            if (k > 0)
            {
                budget.ChargeOperations(1);
                if (indexes[k - 1] > indexes[k])
                {
                    sorted = false;
                }
            }
        }
        return Seal(indexes, indexes.Length, sorted, budget);
    }
    private void Append(List<XPathItem> result, XPathItem item, IXPathBudget budget)
    {
        if (result.Count >= Array.MaxLength)
        {
            throw Limit();
        }

        budget.CheckNodeSetSize(result.Count + 1);
        budget.ChargeOperations(1);
        result.Add(item);
    }
    public IReadOnlyList<XPathItem> SelectId(string id, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(budget);
        budget.ChargeOperations(0);
        budget.CheckStringSize(id.Length);
        var result = new List<XPathItem>();
        var last = -1;
        for (var i = 0; i < entries.Length; i++)
        {
            budget.ChargeOperations(1);
            if (!entries[i].IsId)
            {
                continue;
            }

            var match = false;
            for (var start = 0; start < id.Length;)
            {
                budget.ChargeOperations(1);
                if (XPathConversions.IsSpace(id[start]))
                {
                    start++;
                    continue;
                }
                var end = start;
                while (end < id.Length)
                {
                    budget.ChargeOperations(1);
                    if (XPathConversions.IsSpace(id[end]))
                    {
                        break;
                    }

                    end++;
                }
                if (Compare(id.AsSpan(start, end - start), entries[i].Item.StringValue, budget) == 0)
                {
                    match = true;
                }

                start = end;
            }
            var parent = entries[i].Parent;
            if (match && parent != last)
            {
                Append(result, entries[parent].Item, budget);
                last = parent;
            }
        }
        budget.ChargeOperations(0);
        return result.AsReadOnly();
    }
    public string? FindLanguage(XPathItem item, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        var at = Index(item, budget);
        if (entries[at].Item.Kind != XPathItemKind.Element)
        {
            at = entries[at].Parent;
        }

        while (at >= 0)
        {
            budget.ChargeOperations(1);
            if (entries[at].Language is { } language)
            {
                return language;
            }

            at = entries[at].Parent;
        }
        return null;
    }
}

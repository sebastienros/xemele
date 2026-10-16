using System.Text;
using Xemele.Internal;
using Xemele.Internal.Operations;
namespace Xemele;

/// <summary>Identifies the kind of a node in an XML tree.</summary>
public enum XmlNodeKind
{
    /// <summary>The document container.</summary>
    Document,
    /// <summary>An element with a name, attributes, and child nodes.</summary>
    Element,
    /// <summary>Character data outside a CDATA section.</summary>
    Text,
    /// <summary>Character data represented as a CDATA section.</summary>
    CData,
    /// <summary>An XML comment.</summary>
    Comment,
    /// <summary>A processing instruction with a target and data.</summary>
    ProcessingInstruction,
    /// <summary>A detached container whose children can be transferred together.</summary>
    Fragment
}
internal enum XmlTreeMutationKind
{
    Insert, Remove, AttributeInsert, AttributeRemove, NamespaceInsert, NamespaceRemove, Value, Name
}
internal readonly record struct TreeCopyWork(long NodeVisits, long MemberVisits, long Links);
internal readonly record struct XmlTreeMutation(XmlTreeMutationKind Kind, XmlNode Node, XmlNode OldParent, XmlNode NewParent, XmlAttribute Attribute, XmlNamespaceDeclaration NamespaceDeclaration);
/// <summary>Owns a mutable XML tree and the storage backing its node, attribute, and namespace handles.</summary>
/// <remarks>Handles retain their owning document. Removing a node or member does not invalidate its handle or reuse its identity.</remarks>
public sealed partial class XmlDocument
{
    // Stable, never-reused slots. Handle retention deliberately retains the whole owner.
    // Immutable document-owned name triples share one reference per slot.
    // Unnamed nodes do not allocate a triple or retain a borrowed input view.
    internal sealed class NameData(string local, string uri, string prefix)
    {
        internal readonly string Local = local, Uri = uri, Prefix = prefix;
    }
    internal struct NodeData(XmlNodeKind kind, NameData? name, string value)
    {
        // Internal malformed-tree fixtures use the original constructor. Normal
        // persistence goes through GetName and the shared-name constructor.
        internal NodeData(XmlNodeKind kind, string local, string uri, string prefix, string value)
            : this(kind, new NameData(local, uri, prefix), value) { }
        internal readonly XmlNodeKind Kind = kind;
        internal readonly NameData? Name = name;
        internal readonly string Local => Name is null ? "" : Name.Local;
        internal readonly string Uri => Name is null ? "" : Name.Uri;
        internal readonly string Prefix => Name is null ? "" : Name.Prefix;
        internal string Value = value;
        internal int Parent = -1, First = -1, Last = -1, Previous = -1, Next = -1, Count;
        internal int FirstAttribute = -1, LastAttribute = -1, AttributeCount;
        internal int FirstNamespace = -1, LastNamespace = -1, NamespaceCount;
    }
    internal struct MemberData(int element, NameData? name, string value)
    {
        internal MemberData(int element, string local, string uri, string prefix, string value)
            : this(element, new NameData(local, uri, prefix), value) { }
        internal readonly int Element = element;
        internal NameData? Name = name;
        internal readonly string Local => Name is null ? "" : Name.Local;
        internal readonly string Uri => Name is null ? "" : Name.Uri;
        internal string Prefix
        {
            readonly get => Name is null ? "" : Name.Prefix;
            // Internal adversarial fixtures can replace a prefix directly;
            // public mutations instead use the document's quota-checked path.
            set => Name = new(Local, Uri, value);
        }
        internal string Value = value;
        internal int Previous = -1, Next = -1;
        internal bool Attached = true;
    }
    internal readonly SlotArena<NodeData> Nodes = new();
    internal readonly SlotArena<MemberData> AttributeSlots = new(), NamespaceSlots = new();
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private Dictionary<(string Local, string Uri, string Prefix), NameData>? nameTriples;
    private NameData?[]? recentNames;
    private int recentNameCursor;
    private NameData? FindNameTriple(string local, string uri, string prefix)
    {
        // A bounded front cache avoids hashing repeated long namespace strings.
        // Misses keep the ordinal randomized dictionary and its existing bounds.
        if (recentNames is not null)
        {
            foreach (var name in recentNames)
            {
                if (name is not null && ReferenceEquals(name.Local, local) && ReferenceEquals(name.Uri, uri) && ReferenceEquals(name.Prefix, prefix))
                {
                    return name;
                }
            }
        }

        if (nameTriples is not null && nameTriples.TryGetValue((local, uri, prefix), out var found))
        {
            RememberName(found);
            return found;
        }
        return null;
    }
    private void RememberName(NameData name)
    {
        (recentNames ??= new NameData?[8])[recentNameCursor] = name;
        recentNameCursor = (recentNameCursor + 1) & 7;
    }
    internal bool HasNameTriple(string local, string uri, string prefix) => FindNameTriple(local, uri, prefix) is not null;
    private string InternOwned(string value)
    {
        if (value.Length == 0)
        {
            return "";
        }

        if (names.TryGetValue(value, out var found))
        {
            return found;
        }
        // The immutable string is already owned by the caller, never a view.
        names.Add(value, value);
        return value;
    }
    private readonly OperationBudget budget;
    private readonly XmlResourceLimits limits;
    internal long MutationVersion
    {
        get; private set;
    }
    internal TreeCopyWork LastCopyWork
    {
        get; private set;
    }
    partial void OnMutationCommitted(in XmlTreeMutation mutation);
    internal void NotifyMutation(in XmlTreeMutation mutation)
    {
        AdvanceContentRevision();
        OnMutationCommitted(in mutation);
    }
    /// <summary>Creates an empty document using the default resource limits.</summary>
    public XmlDocument() : this(new XmlResourceLimits()) { }
    /// <summary>Creates an empty document using a snapshot of the supplied resource limits.</summary>
    /// <param name="limits">The positive or disabled resource budgets for the document.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enabled limit is not positive.</exception>
    public XmlDocument(XmlResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        budget = new(new()
        {
            Limits = limits
        }, default);
        this.limits = budget.Options.Limits;
        Nodes.Add(new(XmlNodeKind.Document, null, ""));
    }
    /// <summary>Gets the document container node.</summary>
    public XmlNode DocumentNode => new(this, 0);
    /// <summary>Gets the document's root element, or a default handle when no root element is attached.</summary>
    public XmlNode RootElement
    {
        get
        {
            for (var i = Nodes[0].First; i >= 0; i = Nodes[i].Next)
            {
                if (Nodes[i].Kind == XmlNodeKind.Element)
                {
                    return Handle(i);
                }
            }

            return default;
        }
    }
    internal XmlNode Handle(int slot) => slot < 0 ? default : new(this, slot);
    private void PreflightNames(string a, string b = "", string c = "")
    {
        long missing = 0;
        if (a.Length != 0 && !names.ContainsKey(a))
        {
            missing++;
        }

        if (b.Length != 0 && b != a && !names.ContainsKey(b))
        {
            missing++;
        }

        if (c.Length != 0 && c != a && c != b && !names.ContainsKey(c))
        {
            missing++;
        }

        if (!budget.TryCharge(Quota.InternedNames, missing))
        {
            throw new InvalidOperationException("Tree interned-name quota exceeded.");
        }
    }
    internal string Intern(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return "";
        }

        if (FindInternedName(value) is string found)
        {
            return found;
        }

        var owned = value.ToString();
        names.Add(owned, owned);
        return owned;
    }
    private NameData? GetName(string local, string uri, string prefix, bool cache = true)
    {
        if (local.Length == 0 && uri.Length == 0 && prefix.Length == 0)
        {
            return null;
        }
        // Probe before persistence: the tuple is a value and no temporary lookup
        // strings are allocated. Entries are bounded by created named slots.
        if (FindNameTriple(local, uri, prefix) is { } found)
        {
            return found;
        }

        var name = new NameData(InternOwned(local), InternOwned(uri), InternOwned(prefix));
        if (cache)
        {
            (nameTriples ??= []).Add((name.Local, name.Uri, name.Prefix), name);
            RememberName(name);
        }
        return name;
    }
    internal sealed partial class DomBuilder
    {
        // Syntax owns immutable validated fields. Creation and attachment are
        // one exclusive operation, so the freshly-created leaf cannot be
        // replaced, moved or acquire members between these two steps.
        internal XmlNode AppendParsedNode(XmlNode parent, XmlNodeKind kind, string local, string uri, string prefix, string value, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            CheckState();
            var p = Container(parent, false);
            ArgumentNullException.ThrowIfNull(local);
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(prefix);
            ArgumentNullException.ThrowIfNull(value);
            var shape = kind switch
            {
                XmlNodeKind.Element => local.Length != 0 && value.Length == 0,
                XmlNodeKind.ProcessingInstruction => local.Length != 0 && uri.Length == 0 && prefix.Length == 0,
                XmlNodeKind.Text or XmlNodeKind.CData or XmlNodeKind.Comment => local.Length == 0 && uri.Length == 0 && prefix.Length == 0,
                _ => false
            };
            if (!shape)
            {
                throw new ArgumentException("Unsupported syntax node shape.", nameof(kind));
            }

            var isRoot = p == 0 && kind == XmlNodeKind.Element;
            if (p == 0 && (kind is not (XmlNodeKind.Element or XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction) || isRoot && root))
            {
                throw new ArgumentException("Illegal document child or second root.", nameof(kind));
            }

            var child = document.Create(kind, local, uri, prefix, value);
            document.Link(p, child.Slot, -1);
            ChildChecks++;
            nodes++;
            version = document.MutationVersion;
            root |= isRoot;
            return child;
        }
    }
    private static void CheckCapacity<T>(SlotArena<T> slots) where T : struct
    {
        if (slots.Count >= Array.MaxLength)
        {
            throw new InvalidOperationException("Tree slot capacity exceeded.");
        }
    }
    private static void Capacity<T>(SlotArena<T> slots) where T : struct => slots.EnsureCapacity(checked(slots.Count + 1));
    private XmlNode Create(XmlNodeKind kind, string local, string uri, string prefix, string value)
    {
        CheckCapacity(Nodes);
        NameData? name = null;
        if (local.Length != 0 || uri.Length != 0 || prefix.Length != 0)
        {
            name = FindNameTriple(local, uri, prefix);
            if (name is null)
            {
                PreflightNames(local, uri, prefix);
                name = GetName(local, uri, prefix);
            }
        }
        Capacity(Nodes);
        var data = new NodeData(kind, name, value);
        var slot = Nodes.Count;
        Nodes.Add(data);
        return new(this, slot);
    }
    /// <summary>Creates a detached element owned by this document.</summary>
    /// <param name="localName">The XML local name.</param>
    /// <param name="namespaceUri">The namespace URI, or <see langword="null"/> for no namespace.</param>
    /// <param name="prefix">The prefix, or <see langword="null"/> for no prefix.</param>
    /// <returns>The new element handle.</returns>
    /// <exception cref="ArgumentException">The name or namespace binding is invalid.</exception>
    /// <exception cref="InvalidOperationException">The document's storage or interned-name budget is exhausted.</exception>
    public XmlNode CreateElement(string localName, string? namespaceUri = null, string? prefix = null)
    {
        namespaceUri ??= "";
        prefix ??= "";
        TreeValidation.Expanded(localName, namespaceUri, prefix);
        return Create(XmlNodeKind.Element, localName, namespaceUri, prefix, "");
    }
    /// <summary>Creates a detached text node owned by this document.</summary>
    /// <param name="value">Text containing only legal XML characters.</param>
    /// <returns>The new text node.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> contains illegal XML characters.</exception>
    public XmlNode CreateText(string value)
    {
        TreeValidation.Value(XmlNodeKind.Text, value);
        return Create(XmlNodeKind.Text, "", "", "", value);
    }
    /// <summary>Creates a detached comment owned by this document.</summary>
    /// <param name="value">The comment text, without a double hyphen or trailing hyphen.</param>
    /// <returns>The new comment node.</returns>
    /// <exception cref="ArgumentException">The text contains illegal XML characters or comment delimiters.</exception>
    public XmlNode CreateComment(string value)
    {
        TreeValidation.Value(XmlNodeKind.Comment, value);
        return Create(XmlNodeKind.Comment, "", "", "", value);
    }
    /// <summary>Creates a detached CDATA node owned by this document.</summary>
    /// <param name="value">The character data, without the CDATA closing delimiter.</param>
    /// <returns>The new CDATA node.</returns>
    /// <exception cref="ArgumentException">The text contains illegal XML characters or the CDATA closing delimiter.</exception>
    public XmlNode CreateCData(string value)
    {
        TreeValidation.Value(XmlNodeKind.CData, value);
        return Create(XmlNodeKind.CData, "", "", "", value);
    }
    /// <summary>Creates a detached processing instruction owned by this document.</summary>
    /// <param name="target">The XML target name, other than any case variant of <c>xml</c>.</param>
    /// <param name="data">The instruction data, without the processing-instruction closing delimiter.</param>
    /// <returns>The new processing-instruction node.</returns>
    /// <exception cref="ArgumentException">The target or data is invalid XML.</exception>
    public XmlNode CreateProcessingInstruction(string target, string data)
    {
        TreeValidation.Name(target);
        TreeValidation.Value(XmlNodeKind.ProcessingInstruction, data);
        if (target.Equals("xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Reserved PI target.", nameof(target));
        }

        return Create(XmlNodeKind.ProcessingInstruction, target, "", "", data);
    }
    /// <summary>Creates a detached fragment container owned by this document.</summary>
    /// <returns>An empty fragment whose children can be inserted together into another container.</returns>
    public XmlNode CreateFragment() => Create(XmlNodeKind.Fragment, "", "", "", "");
    internal void EnsureVersions(long amount)
    {
        if (amount > long.MaxValue - MutationVersion)
        {
            throw new InvalidOperationException("Tree mutation version exhausted.");
        }
    }
    private void AdvanceVersion()
    {
        EnsureVersions(1);
        MutationVersion++;
    }
    internal void SetValue(int slot, string value)
    {
        ref var n = ref Nodes[slot];
        if (n.Kind is XmlNodeKind.Element or XmlNodeKind.Document or XmlNodeKind.Fragment)
        {
            throw new InvalidOperationException("Container value is read-only.");
        }

        TreeValidation.Value(n.Kind, value);
        n.Value = value;
        NotifyMutation(new(XmlTreeMutationKind.Value, Handle(slot), default, default, default, default));
    }
    internal void Remove(int slot)
    {
        ref var n = ref Nodes[slot];
        if (n.Kind == XmlNodeKind.Document)
        {
            throw new InvalidOperationException("Document cannot detach.");
        }

        if (n.Parent < 0)
        {
            return;
        }

        AdvanceVersion();
        var parent = n.Parent;
        ref var p = ref Nodes[parent];
        if (n.Previous >= 0)
        {
            Nodes[n.Previous].Next = n.Next;
        }
        else
        {
            p.First = n.Next;
        }

        if (n.Next >= 0)
        {
            Nodes[n.Next].Previous = n.Previous;
        }
        else
        {
            p.Last = n.Previous;
        }

        p.Count--;
        n.Parent = n.Previous = n.Next = -1;
        NotifyMutation(new(XmlTreeMutationKind.Remove, Handle(slot), Handle(parent), default, default, default));
    }
    private void Link(int parent, int child, int reference)
    {
        AdvanceVersion();
        ref var p = ref Nodes[parent];
        ref var n = ref Nodes[child];
        var previous = reference < 0 ? p.Last : Nodes[reference].Previous;
        n.Parent = parent;
        n.Previous = previous;
        n.Next = reference;
        if (previous >= 0)
        {
            Nodes[previous].Next = child;
        }
        else
        {
            p.First = child;
        }

        if (reference >= 0)
        {
            Nodes[reference].Previous = child;
        }
        else
        {
            p.Last = child;
        }

        p.Count++;
        NotifyMutation(new(XmlTreeMutationKind.Insert, Handle(child), default, Handle(parent), default, default));
    }
    internal void Insert(int parent, XmlNode child, XmlNode reference, bool before)
    {
        ref var p = ref Nodes[parent];
        if (p.Kind is not (XmlNodeKind.Element or XmlNodeKind.Fragment or XmlNodeKind.Document))
        {
            throw new InvalidOperationException("Node cannot have children.");
        }

        child.Require();
        if (!ReferenceEquals(child.Document, this))
        {
            throw new ArgumentException("Foreign child.", nameof(child));
        }

        var r = -1;
        if (before)
        {
            reference.Require();
            if (!ReferenceEquals(reference.Document, this) || Nodes[reference.Slot].Parent != parent)
            {
                throw new ArgumentException("Reference is not a direct child.", nameof(reference));
            }

            r = reference.Slot;
        }
        ref var n = ref Nodes[child.Slot];
        if (n.Kind == XmlNodeKind.Document)
        {
            throw new ArgumentException("Document cannot be inserted.", nameof(child));
        }

        for (var a = parent; a >= 0; a = Nodes[a].Parent)
        {
            if (a == child.Slot)
            {
                throw new ArgumentException("Cyclic insertion.", nameof(child));
            }
        }

        if (child.Slot == r)
        {
            return;
        }

        var fragment = n.Kind == XmlNodeKind.Fragment;
        long versions = 0;
        var roots = RootCount(parent);
        for (var i = fragment ? n.First : child.Slot; i >= 0; i = fragment ? Nodes[i].Next : -1)
        {
            var item = Nodes[i];
            for (var a = parent; a >= 0; a = Nodes[a].Parent)
            {
                if (a == i)
                {
                    throw new ArgumentException("Cyclic fragment transfer.", nameof(child));
                }
            }

            if (p.Kind == XmlNodeKind.Document)
            {
                if (item.Kind is not (XmlNodeKind.Element or XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction))
                {
                    throw new ArgumentException("Illegal document child.", nameof(child));
                }

                if (item.Kind == XmlNodeKind.Element && item.Parent != parent)
                {
                    roots++;
                }
            }
            versions += item.Parent >= 0 ? 2 : 1;
        }
        if (roots > 1)
        {
            throw new ArgumentException("Document already has a root.", nameof(child));
        }

        EnsureVersions(versions);
        if (fragment)
        {
            while (n.First >= 0)
            {
                var i = n.First;
                Remove(i);
                Link(parent, i, r);
            }
        }
        else
        {
            Remove(child.Slot);
            Link(parent, child.Slot, r);
        }
    }
    private int RootCount(int parent)
    {
        if (Nodes[parent].Kind != XmlNodeKind.Document)
        {
            return 0;
        }

        var count = 0;
        for (var i = Nodes[parent].First; i >= 0; i = Nodes[i].Next)
        {
            if (Nodes[i].Kind == XmlNodeKind.Element)
            {
                count++;
            }
        }

        return count;
    }
    internal string TextContent(int slot)
    {
        RefuseUnresolvedReferences();
        var root = Nodes[slot];
        if (root.Kind is not (XmlNodeKind.Document or XmlNodeKind.Element or XmlNodeKind.Fragment))
        {
            return root.Value;
        }

        using var result = new ValueStringBuilder();
        for (var i = root.First; i >= 0;)
        {
            var n = Nodes[i];
            if (n.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
            {
                result.Append(n.Value);
            }

            if (n.First >= 0)
            {
                i = n.First;
                continue;
            }
            while (i != slot && Nodes[i].Next < 0)
            {
                i = Nodes[i].Parent;
            }

            i = i == slot ? -1 : Nodes[i].Next;
        }
        return result.ToString();
    }
    internal void SetText(int slot, string value)
    {
        ref var n = ref Nodes[slot];
        if (n.Kind == XmlNodeKind.Document)
        {
            throw new InvalidOperationException("Document text replacement is forbidden.");
        }

        if (n.Kind is not (XmlNodeKind.Element or XmlNodeKind.Fragment))
        {
            SetValue(slot, value);
            return;
        }
        TreeValidation.Text(value);
        EnsureVersions((long)n.Count + (value.Length == 0 ? 0 : 1));
        var text = value.Length == 0 ? default : CreateText(value);
        while (Nodes[slot].First >= 0)
        {
            Remove(Nodes[slot].First);
        }

        if (!text.IsDefault)
        {
            Link(slot, text.Slot, -1);
        }
    }
    internal void Element(int slot)
    {
        if (Nodes[slot].Kind != XmlNodeKind.Element)
        {
            throw new InvalidOperationException("Only elements have attributes or declarations.");
        }
    }
    internal int Find(int element, string local, string uri, bool ns)
    {
        var e = Nodes[element];
        var slots = ns ? NamespaceSlots : AttributeSlots;
        for (var i = ns ? e.FirstNamespace : e.FirstAttribute; i >= 0; i = slots[i].Next)
        {
            if (ns ? slots[i].Prefix == local : slots[i].Local == local && slots[i].Uri == uri)
            {
                return i;
            }
        }

        return -1;
    }
    internal int SetMember(int element, string local, string value, string uri, string prefix, bool ns)
    {
        Element(element);
        if (ns)
        {
            TreeValidation.Binding(prefix, value);
        }
        else
        {
            TreeValidation.Expanded(local, uri, prefix, attribute: true);
            TreeValidation.Text(value);
        }
        var found = Find(element, ns ? prefix : local, uri, ns);
        var slots = ns ? NamespaceSlots : AttributeSlots;
        if (found >= 0)
        {
            ref var existing = ref slots[found];
            PreflightNames(ns ? value : prefix);
            var changedPrefix = !ns && prefix != existing.Prefix;
            if (changedPrefix)
            {
                existing.Name = GetName(existing.Local, existing.Uri, prefix, cache: false);
            }

            existing.Value = ns ? InternOwned(value) : value;
            if (changedPrefix)
            {
                MemberNotify(element, found, false, XmlTreeMutationKind.Name);
            }

            MemberNotify(element, found, ns, XmlTreeMutationKind.Value);
            return found;
        }
        return AppendMember(element, local, value, uri, prefix, ns);
    }
    // Caller has validated bindings and uniqueness, or is copying a valid source
    // member into a fresh element after full-copy preflight. No key scan is needed.
    private int AppendMember(int element, string local, string value, string uri, string prefix, bool ns)
    {
        var slots = ns ? NamespaceSlots : AttributeSlots;
        EnsureVersions(1);
        CheckCapacity(slots);
        var name = FindNameTriple(local, uri, prefix);
        if (ns)
        {
            PreflightNames(prefix, value);
        }
        else if (name is null)
        {
            PreflightNames(local, uri, prefix);
        }

        Capacity(slots);
        name ??= GetName(local, uri, prefix);
        var member = new MemberData(element, name, ns ? InternOwned(value) : value);
        var slot = slots.Count;
        ref var e = ref Nodes[element];
        var last = ns ? e.LastNamespace : e.LastAttribute;
        member.Previous = last;
        slots.Add(member);
        AdvanceVersion();
        if (last >= 0)
        {
            slots[last].Next = slot;
        }

        if (ns)
        {
            if (e.FirstNamespace < 0)
            {
                e.FirstNamespace = slot;
            }

            e.LastNamespace = slot;
            e.NamespaceCount++;
        }
        else
        {
            if (e.FirstAttribute < 0)
            {
                e.FirstAttribute = slot;
            }

            e.LastAttribute = slot;
            e.AttributeCount++;
        }
        MemberNotify(element, slot, ns, ns ? XmlTreeMutationKind.NamespaceInsert : XmlTreeMutationKind.AttributeInsert);
        return slot;
    }
    internal void MemberValue(int slot, string value, bool ns)
    {
        ref var m = ref (ns ? NamespaceSlots : AttributeSlots)[slot];
        if (ns)
        {
            TreeValidation.Binding(m.Prefix, value);
            PreflightNames(value);
            value = Intern(value);
        }
        else
        {
            TreeValidation.Text(value);
        }

        m.Value = value;
        MemberNotify(m.Element, slot, ns, XmlTreeMutationKind.Value);
    }
    internal void RemoveMember(int slot, bool ns)
    {
        var slots = ns ? NamespaceSlots : AttributeSlots;
        ref var m = ref slots[slot];
        if (!m.Attached)
        {
            return;
        }

        AdvanceVersion();
        ref var e = ref Nodes[m.Element];
        if (m.Previous >= 0)
        {
            slots[m.Previous].Next = m.Next;
        }
        else if (ns)
        {
            e.FirstNamespace = m.Next;
        }
        else
        {
            e.FirstAttribute = m.Next;
        }

        if (m.Next >= 0)
        {
            slots[m.Next].Previous = m.Previous;
        }
        else if (ns)
        {
            e.LastNamespace = m.Previous;
        }
        else
        {
            e.LastAttribute = m.Previous;
        }

        if (ns)
        {
            e.NamespaceCount--;
        }
        else
        {
            e.AttributeCount--;
        }

        m.Attached = false;
        m.Previous = m.Next = -1;
        MemberNotify(m.Element, slot, ns, ns ? XmlTreeMutationKind.NamespaceRemove : XmlTreeMutationKind.AttributeRemove);
    }
    private void MemberNotify(int element, int slot, bool ns, XmlTreeMutationKind kind) =>
        NotifyMutation(new(kind, Handle(element), default, default, ns ? default : new(this, slot), ns ? new(this, slot) : default));
    /// <summary>Copies a node into this document without attaching the copy.</summary>
    /// <param name="source">The node to copy, possibly from another document; document nodes cannot be imported.</param>
    /// <param name="deep">Whether to copy descendants in addition to the node's attributes and namespace declarations.</param>
    /// <returns>A detached copy owned by this document.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> is a document node.</exception>
    /// <exception cref="InvalidOperationException">The source is default, its document has unresolved content, or a storage budget is exceeded.</exception>
    public XmlNode ImportNode(XmlNode source, bool deep = true)
    {
        source.Require();
        if (source.Kind == XmlNodeKind.Document)
        {
            throw new ArgumentException("Import document children instead.", nameof(source));
        }

        source.OwnerDocument.RefuseUnresolvedReferences();
        return Copy(source, deep);
    }
    internal XmlNode Clone(XmlNode source, bool deep)
    {
        source.OwnerDocument.RefuseUnresolvedReferences();
        if (source.Kind != XmlNodeKind.Document)
        {
            return Copy(source, deep);
        }

        var copy = new XmlDocument(limits);
        if (!SourceInputComplete)
        {
            copy.MarkSourceIncomplete();
        }

        if (deep)
        {
            foreach (var child in source.Children)
            {
                // A valid document source already has legal ordered children and at
                // most one root; every imported copy is detached and newly owned.
                var imported = copy.ImportNode(child);
                copy.Link(0, imported.Slot, -1);
            }
        }

        return copy.DocumentNode;
    }
    private XmlNode Copy(XmlNode source, bool deep)
    {
        // Preflight all distinct names and structural versions before a quota rejection.
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var remainingNames = (limits.InternedNames ?? int.MaxValue) - names.Count;
        long nodeCount = 0, members = 0, edges = 0, attributeCount = 0, namespaceCount = 0;
        long nodeVisits = 0, memberVisits = 0, links = 0;
        var current = source;
        while (true)
        {
            nodeCount++;
            nodeVisits++;
            if (current != source)
            {
                edges++;
            }

            Collect(current.LocalName);
            Collect(current.NamespaceUri);
            Collect(current.Prefix);
            foreach (var a in current.Attributes)
            {
                members++;
                attributeCount++;
                memberVisits++;
                Collect(a.LocalName);
                Collect(a.NamespaceUri);
                Collect(a.Prefix);
            }
            foreach (var n in current.NamespaceDeclarations)
            {
                members++;
                namespaceCount++;
                memberVisits++;
                Collect(n.Prefix);
                Collect(n.Value);
            }
            if (!deep || !Advance(ref current, source))
            {
                break;
            }
        }
        if (nodeCount > Array.MaxLength - Nodes.Count || attributeCount > Array.MaxLength - AttributeSlots.Count || namespaceCount > Array.MaxLength - NamespaceSlots.Count)
        {
            throw new InvalidOperationException("Tree capacity exceeded.");
        }

        EnsureVersions(edges + members);
        if (missing.Count > (limits.InternedNames ?? int.MaxValue) - names.Count)
        {
            throw new InvalidOperationException("Tree interned-name quota exceeded.");
        }

        XmlNode root = Shallow(source, ref nodeVisits, ref memberVisits), target = root;
        current = source;
        if (!deep)
        {
            LastCopyWork = new(nodeVisits, memberVisits, links);
            return root;
        }
        while (true)
        {
            if (!current.FirstChild.IsDefault)
            {
                current = current.FirstChild;
                var child = Shallow(current, ref nodeVisits, ref memberVisits);
                Link(target.Slot, child.Slot, -1);
                links++;
                target = child;
                continue;
            }
            while (current != source && current.NextSibling.IsDefault)
            {
                current = current.Parent;
                target = target.Parent;
            }
            if (current == source)
            {
                break;
            }

            current = current.NextSibling;
            var sibling = Shallow(current, ref nodeVisits, ref memberVisits);
            var parent = target.Parent;
            Link(parent.Slot, sibling.Slot, -1);
            links++;
            target = sibling;
        }
        LastCopyWork = new(nodeVisits, memberVisits, links);
        return root;
        void Collect(string s)
        {
            if (s.Length == 0 || names.ContainsKey(s) || missing.Contains(s))
            {
                return;
            }
            // Reject the one-over candidate before growing even the scratch set.
            if (missing.Count == remainingNames)
            {
                throw new InvalidOperationException("Tree interned-name quota exceeded.");
            }

            missing.Add(s);
        }
    }
    private static bool Advance(ref XmlNode current, XmlNode root)
    {
        if (!current.FirstChild.IsDefault)
        {
            current = current.FirstChild;
            return true;
        }
        while (current != root && current.NextSibling.IsDefault)
        {
            current = current.Parent;
        }

        if (current == root)
        {
            return false;
        }

        current = current.NextSibling;
        return true;
    }
    private XmlNode Shallow(XmlNode source, ref long nodeVisits, ref long memberVisits)
    {
        // Every target node is freshly detached. Preflight validated the complete
        // source, destination quota/capacity and version budget; preserving the
        // source topology cannot create a cycle or duplicate an expanded-name key.
        var result = Create(source.Kind, source.LocalName, source.NamespaceUri, source.Prefix, source.Value);
        nodeVisits++;
        var owner = source.OwnerDocument;
        for (var i = owner.Nodes[source.Slot].FirstAttribute; i >= 0;)
        {
            var member = owner.AttributeSlots[i]; // snapshot before destination arena growth
            AppendMember(result.Slot, member.Local, member.Value, member.Uri, member.Prefix, false);
            memberVisits++;
            i = member.Next;
        }
        for (var i = owner.Nodes[source.Slot].FirstNamespace; i >= 0;)
        {
            var member = owner.NamespaceSlots[i];
            AppendMember(result.Slot, member.Local, member.Value, member.Uri, member.Prefix, true);
            memberVisits++;
            i = member.Next;
        }
        return result;
    }
}

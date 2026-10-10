using System.Text;
using Xemele.Internal.Operations;
namespace Xemele;
public enum XmlNodeKind { Document, Element, Text, CData, Comment, ProcessingInstruction, Fragment }
internal enum XmlTreeMutationKind { Insert, Remove, AttributeInsert, AttributeRemove, NamespaceInsert, NamespaceRemove, Value, Name }
internal readonly record struct TreeCopyWork(long NodeVisits, long MemberVisits, long Links);
internal readonly record struct XmlTreeMutation(XmlTreeMutationKind Kind, XmlNode Node, XmlNode OldParent, XmlNode NewParent, XmlAttribute Attribute, XmlNamespaceDeclaration NamespaceDeclaration);
public sealed partial class XmlDocument
{
    // Stable, never-reused slots. Handle retention deliberately retains the whole owner.
    internal struct NodeData(XmlNodeKind kind, string local, string uri, string prefix, string value)
    {
        internal readonly XmlNodeKind Kind = kind;
        internal readonly string Local = local, Uri = uri, Prefix = prefix;
        internal string Value = value;
        internal int Parent = -1, First = -1, Last = -1, Previous = -1, Next = -1, Count;
        internal int FirstAttribute = -1, LastAttribute = -1, AttributeCount;
        internal int FirstNamespace = -1, LastNamespace = -1, NamespaceCount;
    }
    internal struct MemberData(int element, string local, string uri, string prefix, string value)
    {
        internal readonly int Element = element;
        internal readonly string Local = local, Uri = uri;
        internal string Prefix = prefix, Value = value;
        internal int Previous = -1, Next = -1;
        internal bool Attached = true;
    }
    internal readonly SlotArena<NodeData> Nodes = new();
    internal readonly SlotArena<MemberData> AttributeSlots = new(), NamespaceSlots = new();
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private readonly OperationBudget budget;
    private readonly XmlResourceLimits limits;
    internal long MutationVersion { get; private set; }
    internal TreeCopyWork LastCopyWork { get; private set; }
    partial void OnMutationCommitted(in XmlTreeMutation mutation);
    internal void NotifyMutation(in XmlTreeMutation mutation) => OnMutationCommitted(in mutation);
    public XmlDocument() : this(new XmlResourceLimits()) { }
    public XmlDocument(XmlResourceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        budget = new(new() { Limits = limits }, default);
        this.limits = budget.Options.Limits;
        Nodes.Add(new(XmlNodeKind.Document, "", "", "", ""));
    }
    public XmlNode DocumentNode => new(this, 0);
    public XmlNode RootElement
    {
        get { for (int i = Nodes[0].First; i >= 0; i = Nodes[i].Next) if (Nodes[i].Kind == XmlNodeKind.Element) return Handle(i); return default; }
    }
    internal XmlNode Handle(int slot) => slot < 0 ? default : new(this, slot);
    private void PreflightNames(string a, string b = "", string c = "")
    {
        long missing = 0;
        if (a.Length != 0 && !names.ContainsKey(a)) missing++;
        if (b.Length != 0 && b != a && !names.ContainsKey(b)) missing++;
        if (c.Length != 0 && c != a && c != b && !names.ContainsKey(c)) missing++;
        if (!budget.TryCharge(Quota.InternedNames, missing)) throw new InvalidOperationException("Tree interned-name quota exceeded.");
    }
    internal string Intern(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty) return "";
        if (FindInternedName(value) is string found) return found;
        string owned = value.ToString(); names.Add(owned, owned); return owned;
    }
    private static void CheckCapacity<T>(SlotArena<T> slots) where T : struct
    {
        if (slots.Count >= Array.MaxLength) throw new InvalidOperationException("Tree slot capacity exceeded.");
    }
    private static void Capacity<T>(SlotArena<T> slots) where T : struct => slots.EnsureCapacity(checked(slots.Count + 1));
    private XmlNode Create(XmlNodeKind kind, string local, string uri, string prefix, string value)
    {
        CheckCapacity(Nodes); PreflightNames(local, uri, prefix); Capacity(Nodes);
        var data = new NodeData(kind, Intern(local), Intern(uri), Intern(prefix), value);
        int slot = Nodes.Count; Nodes.Add(data); return new(this, slot);
    }
    public XmlNode CreateElement(string localName, string? namespaceUri = null, string? prefix = null)
    {
        namespaceUri ??= ""; prefix ??= ""; TreeValidation.Expanded(localName, namespaceUri, prefix);
        return Create(XmlNodeKind.Element, localName, namespaceUri, prefix, "");
    }
    public XmlNode CreateText(string value) { TreeValidation.Value(XmlNodeKind.Text, value); return Create(XmlNodeKind.Text, "", "", "", value); }
    public XmlNode CreateComment(string value) { TreeValidation.Value(XmlNodeKind.Comment, value); return Create(XmlNodeKind.Comment, "", "", "", value); }
    public XmlNode CreateCData(string value) { TreeValidation.Value(XmlNodeKind.CData, value); return Create(XmlNodeKind.CData, "", "", "", value); }
    public XmlNode CreateProcessingInstruction(string target, string data)
    {
        TreeValidation.Name(target); TreeValidation.Value(XmlNodeKind.ProcessingInstruction, data);
        if (target.Equals("xml", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Reserved PI target.", nameof(target));
        return Create(XmlNodeKind.ProcessingInstruction, target, "", "", data);
    }
    public XmlNode CreateFragment() => Create(XmlNodeKind.Fragment, "", "", "", "");
    internal void EnsureVersions(long amount)
    {
        if (amount > long.MaxValue - MutationVersion) throw new InvalidOperationException("Tree mutation version exhausted.");
    }
    private void AdvanceVersion() { EnsureVersions(1); MutationVersion++; }
    internal void SetValue(int slot, string value)
    {
        ref var n = ref Nodes[slot];
        if (n.Kind is XmlNodeKind.Element or XmlNodeKind.Document or XmlNodeKind.Fragment) throw new InvalidOperationException("Container value is read-only.");
        TreeValidation.Value(n.Kind, value); n.Value = value;
        NotifyMutation(new(XmlTreeMutationKind.Value, Handle(slot), default, default, default, default));
    }
    internal void Remove(int slot)
    {
        ref var n = ref Nodes[slot];
        if (n.Kind == XmlNodeKind.Document) throw new InvalidOperationException("Document cannot detach.");
        if (n.Parent < 0) return;
        AdvanceVersion(); int parent = n.Parent; ref var p = ref Nodes[parent];
        if (n.Previous >= 0) Nodes[n.Previous].Next = n.Next; else p.First = n.Next;
        if (n.Next >= 0) Nodes[n.Next].Previous = n.Previous; else p.Last = n.Previous;
        p.Count--; n.Parent = n.Previous = n.Next = -1;
        NotifyMutation(new(XmlTreeMutationKind.Remove, Handle(slot), Handle(parent), default, default, default));
    }
    private void Link(int parent, int child, int reference)
    {
        AdvanceVersion(); ref var p = ref Nodes[parent]; ref var n = ref Nodes[child];
        int previous = reference < 0 ? p.Last : Nodes[reference].Previous;
        n.Parent = parent; n.Previous = previous; n.Next = reference;
        if (previous >= 0) Nodes[previous].Next = child; else p.First = child;
        if (reference >= 0) Nodes[reference].Previous = child; else p.Last = child;
        p.Count++;
        NotifyMutation(new(XmlTreeMutationKind.Insert, Handle(child), default, Handle(parent), default, default));
    }
    internal void Insert(int parent, XmlNode child, XmlNode reference, bool before)
    {
        ref var p = ref Nodes[parent];
        if (p.Kind is not (XmlNodeKind.Element or XmlNodeKind.Fragment or XmlNodeKind.Document)) throw new InvalidOperationException("Node cannot have children.");
        child.Require();
        if (!ReferenceEquals(child.Document, this)) throw new ArgumentException("Foreign child.", nameof(child));
        int r = -1;
        if (before)
        {
            reference.Require();
            if (!ReferenceEquals(reference.Document, this) || Nodes[reference.Slot].Parent != parent) throw new ArgumentException("Reference is not a direct child.", nameof(reference));
            r = reference.Slot;
        }
        ref var n = ref Nodes[child.Slot];
        if (n.Kind == XmlNodeKind.Document) throw new ArgumentException("Document cannot be inserted.", nameof(child));
        for (int a = parent; a >= 0; a = Nodes[a].Parent) if (a == child.Slot) throw new ArgumentException("Cyclic insertion.", nameof(child));
        if (child.Slot == r) return;
        bool fragment = n.Kind == XmlNodeKind.Fragment;
        long versions = 0; int roots = RootCount(parent);
        for (int i = fragment ? n.First : child.Slot; i >= 0; i = fragment ? Nodes[i].Next : -1)
        {
            var item = Nodes[i];
            for (int a = parent; a >= 0; a = Nodes[a].Parent) if (a == i) throw new ArgumentException("Cyclic fragment transfer.", nameof(child));
            if (p.Kind == XmlNodeKind.Document)
            {
                if (item.Kind is not (XmlNodeKind.Element or XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction)) throw new ArgumentException("Illegal document child.", nameof(child));
                if (item.Kind == XmlNodeKind.Element && item.Parent != parent) roots++;
            }
            versions += item.Parent >= 0 ? 2 : 1;
        }
        if (roots > 1) throw new ArgumentException("Document already has a root.", nameof(child));
        EnsureVersions(versions);
        if (fragment)
        {
            while (n.First >= 0) { int i = n.First; Remove(i); Link(parent, i, r); }
        }
        else { Remove(child.Slot); Link(parent, child.Slot, r); }
    }
    private int RootCount(int parent)
    {
        if (Nodes[parent].Kind != XmlNodeKind.Document) return 0;
        int count = 0; for (int i = Nodes[parent].First; i >= 0; i = Nodes[i].Next) if (Nodes[i].Kind == XmlNodeKind.Element) count++;
        return count;
    }
    internal string TextContent(int slot)
    {
        RefuseUnresolvedReferences();
        var root = Nodes[slot];
        if (root.Kind is not (XmlNodeKind.Document or XmlNodeKind.Element or XmlNodeKind.Fragment)) return root.Value;
        var result = new StringBuilder();
        for (int i = root.First; i >= 0;)
        {
            var n = Nodes[i]; if (n.Kind is XmlNodeKind.Text or XmlNodeKind.CData) result.Append(n.Value);
            if (n.First >= 0) { i = n.First; continue; }
            while (i != slot && Nodes[i].Next < 0) i = Nodes[i].Parent;
            i = i == slot ? -1 : Nodes[i].Next;
        }
        return result.ToString();
    }
    internal void SetText(int slot, string value)
    {
        ref var n = ref Nodes[slot];
        if (n.Kind == XmlNodeKind.Document) throw new InvalidOperationException("Document text replacement is forbidden.");
        if (n.Kind is not (XmlNodeKind.Element or XmlNodeKind.Fragment)) { SetValue(slot, value); return; }
        TreeValidation.Text(value); EnsureVersions((long)n.Count + (value.Length == 0 ? 0 : 1));
        XmlNode text = value.Length == 0 ? default : CreateText(value);
        while (Nodes[slot].First >= 0) Remove(Nodes[slot].First);
        if (!text.IsDefault) Link(slot, text.Slot, -1);
    }
    internal void Element(int slot)
    {
        if (Nodes[slot].Kind != XmlNodeKind.Element) throw new InvalidOperationException("Only elements have attributes or declarations.");
    }
    internal int Find(int element, string local, string uri, bool ns)
    {
        var e = Nodes[element]; var slots = ns ? NamespaceSlots : AttributeSlots;
        for (int i = ns ? e.FirstNamespace : e.FirstAttribute; i >= 0; i = slots[i].Next)
            if (ns ? slots[i].Prefix == local : slots[i].Local == local && slots[i].Uri == uri) return i;
        return -1;
    }
    internal int SetMember(int element, string local, string value, string uri, string prefix, bool ns)
    {
        Element(element);
        if (ns) TreeValidation.Binding(prefix, value); else { TreeValidation.Expanded(local, uri, prefix, attribute: true); TreeValidation.Text(value); }
        int found = Find(element, ns ? prefix : local, uri, ns);
        var slots = ns ? NamespaceSlots : AttributeSlots;
        if (found >= 0)
        {
            ref var existing = ref slots[found];
            PreflightNames(ns ? value : prefix);
            bool changedPrefix = !ns && prefix != existing.Prefix;
            existing.Prefix = Intern(prefix); existing.Value = ns ? Intern(value) : value;
            if (changedPrefix) MemberNotify(element, found, false, XmlTreeMutationKind.Name);
            MemberNotify(element, found, ns, XmlTreeMutationKind.Value); return found;
        }
        return AppendMember(element, local, value, uri, prefix, ns);
    }
    // Caller has validated bindings and uniqueness, or is copying a valid source
    // member into a fresh element after full-copy preflight. No key scan is needed.
    private int AppendMember(int element, string local, string value, string uri, string prefix, bool ns)
    {
        var slots = ns ? NamespaceSlots : AttributeSlots;
        EnsureVersions(1); CheckCapacity(slots);
        PreflightNames(ns ? prefix : local, ns ? value : uri, ns ? "" : prefix); Capacity(slots);
        var member = new MemberData(element, Intern(local), Intern(uri), Intern(prefix), ns ? Intern(value) : value);
        int slot = slots.Count; ref var e = ref Nodes[element];
        int last = ns ? e.LastNamespace : e.LastAttribute;
        member.Previous = last; slots.Add(member); AdvanceVersion();
        if (last >= 0) slots[last].Next = slot;
        if (ns) { if (e.FirstNamespace < 0) e.FirstNamespace = slot; e.LastNamespace = slot; e.NamespaceCount++; }
        else { if (e.FirstAttribute < 0) e.FirstAttribute = slot; e.LastAttribute = slot; e.AttributeCount++; }
        MemberNotify(element, slot, ns, ns ? XmlTreeMutationKind.NamespaceInsert : XmlTreeMutationKind.AttributeInsert);
        return slot;
    }
    internal void MemberValue(int slot, string value, bool ns)
    {
        ref var m = ref (ns ? NamespaceSlots : AttributeSlots)[slot];
        if (ns) { TreeValidation.Binding(m.Prefix, value); PreflightNames(value); value = Intern(value); }
        else TreeValidation.Text(value);
        m.Value = value; MemberNotify(m.Element, slot, ns, XmlTreeMutationKind.Value);
    }
    internal void RemoveMember(int slot, bool ns)
    {
        var slots = ns ? NamespaceSlots : AttributeSlots; ref var m = ref slots[slot];
        if (!m.Attached) return;
        AdvanceVersion(); ref var e = ref Nodes[m.Element];
        if (m.Previous >= 0) slots[m.Previous].Next = m.Next;
        else if (ns) e.FirstNamespace = m.Next; else e.FirstAttribute = m.Next;
        if (m.Next >= 0) slots[m.Next].Previous = m.Previous;
        else if (ns) e.LastNamespace = m.Previous; else e.LastAttribute = m.Previous;
        if (ns) e.NamespaceCount--; else e.AttributeCount--;
        m.Attached = false; m.Previous = m.Next = -1;
        MemberNotify(m.Element, slot, ns, ns ? XmlTreeMutationKind.NamespaceRemove : XmlTreeMutationKind.AttributeRemove);
    }
    private void MemberNotify(int element, int slot, bool ns, XmlTreeMutationKind kind) =>
        NotifyMutation(new(kind, Handle(element), default, default, ns ? default : new(this, slot), ns ? new(this, slot) : default));
    public XmlNode ImportNode(XmlNode source, bool deep = true)
    {
        source.Require(); if (source.Kind == XmlNodeKind.Document) throw new ArgumentException("Import document children instead.", nameof(source));
        source.OwnerDocument.RefuseUnresolvedReferences();
        return Copy(source, deep);
    }
    internal XmlNode Clone(XmlNode source, bool deep)
    {
        source.OwnerDocument.RefuseUnresolvedReferences();
        if (source.Kind != XmlNodeKind.Document) return Copy(source, deep);
        var copy = new XmlDocument(limits);
        if (deep) foreach (var child in source.Children)
        {
            // A valid document source already has legal ordered children and at
            // most one root; every imported copy is detached and newly owned.
            var imported = copy.ImportNode(child); copy.Link(0, imported.Slot, -1);
        }
        return copy.DocumentNode;
    }
    private XmlNode Copy(XmlNode source, bool deep)
    {
        // Preflight all distinct names and structural versions before a quota rejection.
        var missing = new HashSet<string>(StringComparer.Ordinal);
        int remainingNames = (limits.InternedNames ?? int.MaxValue) - names.Count;
        long nodeCount = 0, members = 0, edges = 0, attributeCount = 0, namespaceCount = 0;
        long nodeVisits = 0, memberVisits = 0, links = 0;
        XmlNode current = source;
        while (true)
        {
            nodeCount++; nodeVisits++; if (current != source) edges++;
            Collect(current.LocalName); Collect(current.NamespaceUri); Collect(current.Prefix);
            foreach (var a in current.Attributes) { members++; attributeCount++; memberVisits++; Collect(a.LocalName); Collect(a.NamespaceUri); Collect(a.Prefix); }
            foreach (var n in current.NamespaceDeclarations) { members++; namespaceCount++; memberVisits++; Collect(n.Prefix); Collect(n.Value); }
            if (!deep || !Advance(ref current, source)) break;
        }
        if (nodeCount > Array.MaxLength - Nodes.Count || attributeCount > Array.MaxLength - AttributeSlots.Count || namespaceCount > Array.MaxLength - NamespaceSlots.Count)
            throw new InvalidOperationException("Tree capacity exceeded.");
        EnsureVersions(edges + members);
        if (missing.Count > (limits.InternedNames ?? int.MaxValue) - names.Count) throw new InvalidOperationException("Tree interned-name quota exceeded.");
        XmlNode root = Shallow(source, ref nodeVisits, ref memberVisits), target = root; current = source;
        if (!deep) { LastCopyWork = new(nodeVisits, memberVisits, links); return root; }
        while (true)
        {
            if (!current.FirstChild.IsDefault) { current = current.FirstChild; var child = Shallow(current, ref nodeVisits, ref memberVisits); Link(target.Slot, child.Slot, -1); links++; target = child; continue; }
            while (current != source && current.NextSibling.IsDefault) { current = current.Parent; target = target.Parent; }
            if (current == source) break;
            current = current.NextSibling; var sibling = Shallow(current, ref nodeVisits, ref memberVisits); var parent = target.Parent; Link(parent.Slot, sibling.Slot, -1); links++; target = sibling;
        }
        LastCopyWork = new(nodeVisits, memberVisits, links);
        return root;
        void Collect(string s)
        {
            if (s.Length == 0 || names.ContainsKey(s) || missing.Contains(s)) return;
            // Reject the one-over candidate before growing even the scratch set.
            if (missing.Count == remainingNames) throw new InvalidOperationException("Tree interned-name quota exceeded.");
            missing.Add(s);
        }
    }
    private static bool Advance(ref XmlNode current, XmlNode root)
    {
        if (!current.FirstChild.IsDefault) { current = current.FirstChild; return true; }
        while (current != root && current.NextSibling.IsDefault) current = current.Parent;
        if (current == root) return false;
        current = current.NextSibling; return true;
    }
    private XmlNode Shallow(XmlNode source, ref long nodeVisits, ref long memberVisits)
    {
        // Every target node is freshly detached. Preflight validated the complete
        // source, destination quota/capacity and version budget; preserving the
        // source topology cannot create a cycle or duplicate an expanded-name key.
        var result = Create(source.Kind, source.LocalName, source.NamespaceUri, source.Prefix, source.Value);
        nodeVisits++;
        var owner = source.OwnerDocument;
        for (int i = owner.Nodes[source.Slot].FirstAttribute; i >= 0;)
        {
            var member = owner.AttributeSlots[i]; // snapshot before destination arena growth
            AppendMember(result.Slot, member.Local, member.Value, member.Uri, member.Prefix, false);
            memberVisits++; i = member.Next;
        }
        for (int i = owner.Nodes[source.Slot].FirstNamespace; i >= 0;)
        {
            var member = owner.NamespaceSlots[i];
            AppendMember(result.Slot, member.Local, member.Value, member.Uri, member.Prefix, true);
            memberVisits++; i = member.Next;
        }
        return result;
    }
}

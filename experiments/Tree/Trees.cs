namespace TreeSpike;

// Deliberately private experiment APIs, not Xemele's future public contract.
public readonly record struct Handle(ArenaTree Owner, int Id)
{
    public string Name => Owner.Name(Id);
    public void MoveTo(Handle parent)
    {
        if (!ReferenceEquals(Owner, parent.Owner)) throw new ArgumentException("Different owner");
        Owner.Move(Id, parent.Id);
    }
}

public sealed class ArenaTree
{
    private struct Slot
    {
        public string Name, Text;
        public int Parent, First, Last, Next, Previous;
        public Slot(string name, string text)
        {
            Name = name; Text = text;
            Parent = First = Last = Next = Previous = -1;
        }
    }
    private readonly List<Slot> _slots = [];
    public Handle Add(string name, string text, int parent)
    {
        int id = _slots.Count;
        _slots.Add(new Slot(name, text));
        if (parent >= 0) Attach(id, parent);
        return new Handle(this, id);
    }
    public string Name(int id) => _slots[id].Name;
    public void Move(int id, int parent)
    {
        if (id == 0) throw new ArgumentException("Cannot move root");
        for (int p = parent; p >= 0; p = _slots[p].Parent)
            if (p == id) throw new ArgumentException("Cycle");
        Detach(id); Attach(id, parent);
    }
    private void Attach(int id, int parent)
    {
        var p = _slots[parent]; var n = _slots[id];
        n.Parent = parent; n.Previous = p.Last; n.Next = -1;
        if (p.Last >= 0) { var last = _slots[p.Last]; last.Next = id; _slots[p.Last] = last; }
        else p.First = id;
        p.Last = id; _slots[parent] = p; _slots[id] = n;
    }
    private void Detach(int id)
    {
        var n = _slots[id]; var p = _slots[n.Parent];
        if (n.Previous >= 0) { var prev = _slots[n.Previous]; prev.Next = n.Next; _slots[n.Previous] = prev; }
        else p.First = n.Next;
        if (n.Next >= 0) { var next = _slots[n.Next]; next.Previous = n.Previous; _slots[n.Next] = next; }
        else p.Last = n.Previous;
        _slots[n.Parent] = p; n.Parent = n.Previous = n.Next = -1; _slots[id] = n;
    }
    public long Traverse()
    {
        long result = 0; int id = 0;
        while (id >= 0)
        {
            var n = _slots[id]; result += n.Name.Length + n.Text.Length;
            if (n.First >= 0) { id = n.First; continue; }
            while (n.Next < 0 && n.Parent >= 0) { id = n.Parent; n = _slots[id]; }
            id = n.Next;
        }
        return result;
    }
}

public sealed class ObjectTree
{
    public sealed class Node(ObjectTree owner, string name, string text)
    {
        public readonly ObjectTree Owner = owner;
        public readonly string Name = name, Text = text;
        internal Node? Parent, First, Last, Next, Previous;
        public void MoveTo(Node parent)
        {
            if (!ReferenceEquals(Owner, parent.Owner)) throw new ArgumentException("Different owner");
            if (ReferenceEquals(this, Owner.Root)) throw new ArgumentException("Cannot move root");
            for (Node? p = parent; p != null; p = p.Parent)
                if (ReferenceEquals(this, p)) throw new ArgumentException("Cycle");
            var old = Parent!;
            if (Previous != null) Previous.Next = Next; else old.First = Next;
            if (Next != null) Next.Previous = Previous; else old.Last = Previous;
            Parent = Previous = Next = null;
            Attach(parent);
        }
        internal void Attach(Node parent)
        {
            Parent = parent; Previous = parent.Last;
            if (parent.Last != null) parent.Last.Next = this; else parent.First = this;
            parent.Last = this;
        }
    }
    public Node Root { get; private set; } = null!;
    public Node Add(string name, string text, Node? parent)
    {
        var node = new Node(this, name, text);
        if (parent != null) node.Attach(parent); else Root = node;
        return node;
    }
    public long Traverse()
    {
        long result = 0; Node? node = Root;
        while (node != null)
        {
            result += node.Name.Length + node.Text.Length;
            if (node.First != null) { node = node.First; continue; }
            while (node.Next == null && node.Parent != null) node = node.Parent;
            node = node.Next;
        }
        return result;
    }
}

public sealed class Tokens
{
    public readonly string Source;
    public readonly (int Start, int Length)[] Ranges;
    public Tokens(int count, bool unique, int length = 8)
    {
        var builder = new System.Text.StringBuilder();
        Ranges = new (int, int)[count];
        for (int i = 0; i < count; i++)
        {
            string name = ((i % 2 == 0) ? "node" : "節点") + (unique ? i : i % 8).ToString(System.Globalization.CultureInfo.InvariantCulture);
            name = name.PadRight(length, 'x');
            Ranges[i] = (builder.Length, name.Length); builder.Append(name); builder.Append('|');
        }
        Source = builder.ToString();
    }
    public ReadOnlySpan<char> At(int i) => Source.AsSpan(Ranges[i].Start, Ranges[i].Length);
}

public static class Factory
{
    public static string Intern(Dictionary<string, string> table, ReadOnlySpan<char> name)
    {
        var lookup = table.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(name, out string? found)) return found;
        string owned = new(name); table.Add(owned, owned); return owned;
    }
    public static ArenaTree Arena(Tokens tokens)
    {
        var tree = new ArenaTree(); var names = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < tokens.Ranges.Length; i++) tree.Add(Intern(names, tokens.At(i)), "payload", i == 0 ? -1 : (i - 1) / 4);
        return tree;
    }
    public static ObjectTree Objects(Tokens tokens)
    {
        var tree = new ObjectTree(); var names = new Dictionary<string, string>(StringComparer.Ordinal);
        // Temporary construction index is measured, then released; durable nodes remain linked.
        var nodes = new ObjectTree.Node[tokens.Ranges.Length];
        for (int i = 0; i < nodes.Length; i++) nodes[i] = tree.Add(Intern(names, tokens.At(i)), "payload", i == 0 ? null : nodes[(i - 1) / 4]);
        return tree;
    }
}

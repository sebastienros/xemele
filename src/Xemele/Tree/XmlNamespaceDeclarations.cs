namespace Xemele;
public readonly struct XmlNamespaceDeclarations
{
    private readonly XmlNode node;
    internal XmlNamespaceDeclarations(XmlNode node) => this.node = node;
    public int Count => node.OwnerDocument.Nodes[node.Slot].NamespaceCount;
    public XmlNamespaceDeclaration this[int index]
    {
        get
        {
            var document = node.OwnerDocument;
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            int i = document.Nodes[node.Slot].FirstNamespace;
            while (index-- > 0) i = document.NamespaceSlots[i].Next;
            return new(document, i);
        }
    }
    public Enumerator GetEnumerator() => new(node);
    public struct Enumerator
    {
        private readonly XmlDocument document;
        private readonly long version;
        private int next, current;
        internal Enumerator(XmlNode node)
        {
            document = node.OwnerDocument; version = document.MutationVersion;
            next = document.Nodes[node.Slot].FirstNamespace; current = -1;
        }
        private void Check() { if (document is null || version != document.MutationVersion) throw new InvalidOperationException("Invalid enumerator or structural mutation."); }
        public XmlNamespaceDeclaration Current
        {
            get { Check(); if (current < 0) throw new InvalidOperationException("No current item."); return new(document, current); }
        }
        public bool MoveNext()
        {
            Check(); current = next;
            if (current < 0) return false;
            next = document.NamespaceSlots[current].Next; return true;
        }
    }
}

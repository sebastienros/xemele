namespace Xemele;

/// <summary>A live, tree-ordered view of a node's direct children.</summary>
/// <remarks>A default view is unusable. Retaining a view also retains the owning document.</remarks>
public readonly struct XmlNodeChildren
{
    private readonly XmlNode node;
    internal XmlNodeChildren(XmlNode node) => this.node = node;
    /// <summary>Gets the current number of direct children.</summary>
    public int Count => node.OwnerDocument.Nodes[node.Slot].Count;
    /// <summary>Gets the direct child at the specified position.</summary>
    /// <param name="index">The zero-based child index.</param>
    /// <returns>The child at that position in tree order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the view.</exception>
    public XmlNode this[int index]
    {
        get
        {
            var document = node.OwnerDocument;
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var i = document.Nodes[node.Slot].First;
            while (index-- > 0)
            {
                i = document.Nodes[i].Next;
            }

            return new(document, i);
        }
    }
    /// <summary>Creates an enumerator over the direct children in tree order.</summary>
    /// <returns>An enumerator invalidated by structural mutations anywhere in the owning document.</returns>
    public Enumerator GetEnumerator() => new(node);
    /// <summary>Enumerates direct children while checking the document's structural mutation version.</summary>
    public struct Enumerator
    {
        private readonly XmlDocument document;
        private readonly long version;
        private int next, current;
        internal Enumerator(XmlNode node)
        {
            document = node.OwnerDocument;
            version = document.MutationVersion;
            next = document.Nodes[node.Slot].First;
            current = -1;
        }
        private readonly void Check()
        {
            if (document is null || version != document.MutationVersion)
            {
                throw new InvalidOperationException("Invalid enumerator or structural mutation.");
            }
        }
        /// <summary>Gets the child at the current position.</summary>
        /// <exception cref="InvalidOperationException">The enumerator has no current item, is default, or the document was structurally mutated.</exception>
        public readonly XmlNode Current
        {
            get
            {
                Check();
                if (current < 0)
                {
                    throw new InvalidOperationException("No current item.");
                }

                return new(document, current);
            }
        }
        /// <summary>Advances to the next direct child.</summary>
        /// <returns><see langword="true"/> if a child is available; otherwise <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The enumerator is default or the document was structurally mutated.</exception>
        public bool MoveNext()
        {
            Check();
            current = next;
            if (current < 0)
            {
                return false;
            }

            next = document.Nodes[current].Next;
            return true;
        }
    }
}

namespace Xemele;

/// <summary>A live, insertion-ordered view of namespace declarations directly attached to a node.</summary>
/// <remarks>This view does not include bindings inherited from ancestors. A default view is unusable. Retaining a view also retains the owning document.</remarks>
public readonly struct XmlNamespaceDeclarations
{
    private readonly XmlNode node;
    internal XmlNamespaceDeclarations(XmlNode node) => this.node = node;
    /// <summary>Gets the current number of directly attached declarations.</summary>
    public int Count => node.OwnerDocument.Nodes[node.Slot].NamespaceCount;
    /// <summary>Gets the namespace declaration at the specified position.</summary>
    /// <param name="index">The zero-based declaration index.</param>
    /// <returns>The attached declaration at that position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the view.</exception>
    public XmlNamespaceDeclaration this[int index]
    {
        get
        {
            var document = node.OwnerDocument;
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var i = document.Nodes[node.Slot].FirstNamespace;
            while (index-- > 0)
            {
                i = document.NamespaceSlots[i].Next;
            }

            return new(document, i);
        }
    }
    /// <summary>Creates an enumerator over the directly attached declarations.</summary>
    /// <returns>An insertion-order enumerator invalidated by structural mutations anywhere in the document.</returns>
    public Enumerator GetEnumerator() => new(node);
    /// <summary>Enumerates declarations while checking the document's structural mutation version.</summary>
    public struct Enumerator
    {
        private readonly XmlDocument document;
        private readonly long version;
        private int next, current;
        internal Enumerator(XmlNode node)
        {
            document = node.OwnerDocument;
            version = document.MutationVersion;
            next = document.Nodes[node.Slot].FirstNamespace;
            current = -1;
        }
        private readonly void Check()
        {
            if (document is null || version != document.MutationVersion)
            {
                throw new InvalidOperationException("Invalid enumerator or structural mutation.");
            }
        }
        /// <summary>Gets the declaration at the current position.</summary>
        /// <exception cref="InvalidOperationException">The enumerator has no current item, is default, or the document was structurally mutated.</exception>
        public readonly XmlNamespaceDeclaration Current
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
        /// <summary>Advances to the next attached declaration.</summary>
        /// <returns><see langword="true"/> if a declaration is available; otherwise <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The enumerator is default or the document was structurally mutated.</exception>
        public bool MoveNext()
        {
            Check();
            current = next;
            if (current < 0)
            {
                return false;
            }

            next = document.NamespaceSlots[current].Next;
            return true;
        }
    }
}

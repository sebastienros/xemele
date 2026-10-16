namespace Xemele;

/// <summary>A live, insertion-ordered view of an element's attached attributes, excluding namespace declarations.</summary>
/// <remarks>A default view is unusable. Retaining a view also retains the owning document.</remarks>
public readonly struct XmlAttributes
{
    private readonly XmlNode node;
    internal XmlAttributes(XmlNode node) => this.node = node;
    /// <summary>Gets the current number of attached attributes.</summary>
    public int Count => node.OwnerDocument.Nodes[node.Slot].AttributeCount;
    /// <summary>Gets the attribute at the specified position.</summary>
    /// <param name="index">The zero-based attribute index.</param>
    /// <returns>The attached attribute at that position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the view.</exception>
    public XmlAttribute this[int index]
    {
        get
        {
            var document = node.OwnerDocument;
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var i = document.Nodes[node.Slot].FirstAttribute;
            while (index-- > 0)
            {
                i = document.AttributeSlots[i].Next;
            }

            return new(document, i);
        }
    }
    /// <summary>Creates an enumerator over the attached attributes in insertion order.</summary>
    /// <returns>An enumerator invalidated by structural mutations anywhere in the owning document.</returns>
    public Enumerator GetEnumerator() => new(node);
    /// <summary>Enumerates attached attributes while checking the document's structural mutation version.</summary>
    public struct Enumerator
    {
        private readonly XmlDocument document;
        private readonly long version;
        private int next, current;
        internal Enumerator(XmlNode node)
        {
            document = node.OwnerDocument;
            version = document.MutationVersion;
            next = document.Nodes[node.Slot].FirstAttribute;
            current = -1;
        }
        private readonly void Check()
        {
            if (document is null || version != document.MutationVersion)
            {
                throw new InvalidOperationException("Invalid enumerator or structural mutation.");
            }
        }
        /// <summary>Gets the attribute at the current position.</summary>
        /// <exception cref="InvalidOperationException">The enumerator has no current item, is default, or the document was structurally mutated.</exception>
        public readonly XmlAttribute Current
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
        /// <summary>Advances to the next attached attribute.</summary>
        /// <returns><see langword="true"/> if an attribute is available; otherwise <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The enumerator is default or the document was structurally mutated.</exception>
        public bool MoveNext()
        {
            Check();
            current = next;
            if (current < 0)
            {
                return false;
            }

            next = document.AttributeSlots[current].Next;
            return true;
        }
    }
}

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Xemele.DomBuilderSample")]

namespace Xemele;

public sealed partial class XmlDocument
{
    /// <summary>Starts exclusive validated construction on a completely fresh document.</summary>
    /// <remarks>No parser dependency: the caller supplies validated, unique member keys.
    /// General mutation remains available, but structural mutation outside this builder
    /// invalidates it. Detached creation must be followed immediately by AppendChild.</remarks>
    internal DomBuilder BeginDomConstruction()
    {
        if (Nodes.Count != 1 || AttributeSlots.Count != 0 || NamespaceSlots.Count != 0 || MutationVersion != 0)
            throw new InvalidOperationException("Construction requires a fresh document.");
        return new(this);
    }

    internal sealed partial class DomBuilder
    {
        private readonly XmlDocument document;
        private int nodes = 1, attributes, namespaces;
        private long version;
        private bool root;
        internal long ChildChecks { get; private set; }
        internal long MemberChecks { get; private set; }

        internal DomBuilder(XmlDocument document) => this.document = document;

        private void CheckState(bool createdChild = false)
        {
            if (document.MutationVersion != version || document.Nodes.Count != nodes + (createdChild ? 1L : 0L) ||
                document.AttributeSlots.Count != attributes || document.NamespaceSlots.Count != namespaces)
                throw new InvalidOperationException("Construction was changed outside this builder.");
        }

        private int Container(XmlNode node, bool elementOnly)
        {
            node.Require();
            if (!ReferenceEquals(node.Document, document)) throw new ArgumentException("Foreign construction container.", nameof(node));
            if ((uint)node.Slot >= (uint)nodes) throw new ArgumentException("Container was not constructed by this builder.", nameof(node));
            var kind = document.Nodes[node.Slot].Kind;
            if (kind != XmlNodeKind.Element && (elementOnly || kind != XmlNodeKind.Document))
                throw new InvalidOperationException("Invalid construction container.");
            return node.Slot;
        }

        /// <summary>Appends the sole node created since the previous builder operation.</summary>
        /// <remarks>The child must be the newest same-owner detached leaf with no members.
        /// Its creation API has validated names/value. Its freshness makes a cycle impossible;
        /// no ancestor traversal is necessary. Fragments and document children are rejected.
        /// Checked arena capacities bound parent counts. Link performs version preflight,
        /// ordered attachment and the existing postcommit mutation notification.</remarks>
        internal void AppendChild(XmlNode parent, XmlNode child)
        {
            child.Require();
            if (!ReferenceEquals(child.Document, document)) throw new ArgumentException("Foreign construction child.", nameof(child));
            CheckState(createdChild: true);
            int p = Container(parent, false);
            if (child.Slot != nodes) throw new ArgumentException("Child is not newly created.", nameof(child));
            ref var n = ref document.Nodes[child.Slot];
            if (n.Parent != -1 || n.Previous != -1 || n.Next != -1 || n.First != -1 || n.Last != -1 || n.Count != 0 ||
                n.AttributeCount != 0 || n.NamespaceCount != 0 || n.Kind is XmlNodeKind.Document or XmlNodeKind.Fragment)
                throw new ArgumentException("Child must be a fresh detached leaf.", nameof(child));
            bool isRoot = p == 0 && n.Kind == XmlNodeKind.Element;
            if (p == 0 && (n.Kind is not (XmlNodeKind.Element or XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction) || (isRoot && root)))
                throw new ArgumentException("Illegal document child or second root.", nameof(child));
            ChildChecks++;
            document.Link(p, child.Slot, -1);
            nodes++; version = document.MutationVersion; root |= isRoot;
        }

        /// <summary>Appends an attribute whose expanded key is known unique by validated syntax.</summary>
        /// <remarks>Caller must never submit a duplicate (namespaceUri, localName) to this
        /// element. Names, binding and scalar validity are still checked here. No borrowed
        /// input is retained: strings are durable values, names are document-interned.</remarks>
        internal XmlAttribute AppendAttribute(XmlNode element, string localName, string value, string? namespaceUri = null, string? prefix = null)
        {
            CheckState(); int e = Container(element, true);
            namespaceUri ??= ""; prefix ??= "";
            TreeValidation.Expanded(localName, namespaceUri, prefix); TreeValidation.Text(value);
            MemberChecks++;
            int slot = document.AppendMember(e, localName, value, namespaceUri, prefix, false);
            attributes++; version = document.MutationVersion;
            return new(document, slot);
        }

        /// <summary>Appends a declaration whose prefix is known unique by validated syntax.</summary>
        /// <remarks>Caller must never submit a duplicate prefix to this element.
        /// Reserved/empty bindings remain validated; no member-key list scan occurs.</remarks>
        internal XmlNamespaceDeclaration AppendNamespaceDeclaration(XmlNode element, string prefix, string namespaceUri)
        {
            CheckState(); int e = Container(element, true);
            TreeValidation.Binding(prefix, namespaceUri);
            MemberChecks++;
            int slot = document.AppendMember(e, "", namespaceUri, "", prefix, true);
            namespaces++; version = document.MutationVersion;
            return new(document, slot);
        }
    }
}

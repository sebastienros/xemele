using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Xemele.DomPersistenceSample")]

namespace Xemele;

public sealed partial class XmlDocument
{
    internal sealed partial class DomBuilder
    {
        /// <summary>Persists exact owned strings already fully validated by syntax.</summary>
        /// <remarks>Trusted callers must prove XML names/scalars, reserved bindings,
        /// comment/CDATA/PI delimiter legality and non-reserved PI target for these
        /// exact immutable strings. Only verified syntax sinks or independently
        /// validated model replay may call this boundary. It does not validate XML.
        /// It retains values and interns names; no borrowed input may be retained.
        /// Create must be followed immediately by AppendSyntaxValidatedChild.
        /// Cancellation is checked at boundaries, not inside platform string hashing,
        /// copying or arena allocation. Cancellation abandons the private document;
        /// no allocation rollback is promised.</remarks>
        internal XmlNode CreateSyntaxValidatedNode(XmlNodeKind kind, string localName,
            string namespaceUri, string prefix, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckState();
            ArgumentNullException.ThrowIfNull(localName);
            ArgumentNullException.ThrowIfNull(namespaceUri);
            ArgumentNullException.ThrowIfNull(prefix);
            ArgumentNullException.ThrowIfNull(value);
            bool shape = kind switch
            {
                XmlNodeKind.Element => localName.Length != 0 && value.Length == 0,
                XmlNodeKind.ProcessingInstruction => localName.Length != 0 && namespaceUri.Length == 0 && prefix.Length == 0,
                XmlNodeKind.Text or XmlNodeKind.CData or XmlNodeKind.Comment =>
                    localName.Length == 0 && namespaceUri.Length == 0 && prefix.Length == 0,
                _ => false
            };
            if (!shape) throw new ArgumentException("Unsupported syntax node kind or noncanonical fields.", nameof(kind));
            return document.Create(kind, localName, namespaceUri, prefix, value);
        }

        /// <summary>Attaches the newest detached leaf with all original structural checks.</summary>
        internal void AppendSyntaxValidatedChild(XmlNode parent, XmlNode child, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendChild(parent, child);
        }

        /// <summary>Persists a fully syntax-validated attribute with a unique expanded key.</summary>
        /// <remarks>Caller proves all exact strings' names/scalars/reserved bindings and
        /// uniqueness of (namespaceUri, localName) on this element. No key or scalar
        /// scan is performed. Values are owned immutable strings, not borrowed views.</remarks>
        internal XmlAttribute AppendSyntaxValidatedAttribute(XmlNode element, string localName,
            string value, string namespaceUri, string prefix, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckState(); int e = Container(element, true);
            ArgumentNullException.ThrowIfNull(localName);
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(namespaceUri);
            ArgumentNullException.ThrowIfNull(prefix);
            if (localName.Length == 0) throw new ArgumentException("Empty attribute local name.", nameof(localName));
            MemberChecks++;
            int slot = document.AppendMember(e, localName, value, namespaceUri, prefix, false);
            attributes++; version = document.MutationVersion;
            return new(document, slot);
        }

        /// <summary>Persists a fully syntax-validated declaration with a unique prefix.</summary>
        /// <remarks>Caller proves exact strings' names/scalars/reserved and empty binding
        /// legality and prefix uniqueness on this element. URI is document-interned.</remarks>
        internal XmlNamespaceDeclaration AppendSyntaxValidatedNamespaceDeclaration(XmlNode element,
            string prefix, string namespaceUri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckState(); int e = Container(element, true);
            ArgumentNullException.ThrowIfNull(prefix);
            ArgumentNullException.ThrowIfNull(namespaceUri);
            MemberChecks++;
            int slot = document.AppendMember(e, "", namespaceUri, "", prefix, true);
            namespaces++; version = document.MutationVersion;
            return new(document, slot);
        }
    }
}

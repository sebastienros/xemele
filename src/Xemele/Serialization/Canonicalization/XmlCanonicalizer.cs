using System.Text;
using Xemele.XPath;
namespace Xemele.Serialization;

/// <summary>Selects the XML canonicalization algorithm.</summary>
public enum XmlCanonicalizationMode
{
    /// <summary>Inclusive Canonical XML 1.0.</summary>
    Inclusive10 = 0,
    /// <summary>Inclusive Canonical XML 1.1.</summary>
    Inclusive11 = 1,
    /// <summary>Exclusive XML Canonicalization 1.0.</summary>
    Exclusive10 = 2
}
/// <summary>Configures canonicalization, namespace inclusion, cancellation, and output quotas.</summary>
public sealed record XmlCanonicalizationOptions
{
    /// <summary>Gets the canonicalization algorithm. Defaults to inclusive Canonical XML 1.0.</summary>
    public XmlCanonicalizationMode Mode { get; init; } = XmlCanonicalizationMode.Inclusive10;
    /// <summary>Gets whether comments are included in canonical output. Defaults to false.</summary>
    public bool IncludeComments
    {
        get; init;
    }
    /// <summary>Gets prefixes to render inclusively in exclusive mode; use #default for the default namespace.</summary>
    /// <remarks>The list must be empty for inclusive modes.</remarks>
    public IReadOnlyList<string> InclusivePrefixes { get; init; } = [];
    /// <summary>Gets the processing limits applied to canonicalization.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
    /// <summary>Gets the positive maximum output byte count, or null for no output-byte limit. Defaults to 16 MiB.</summary>
    public long? MaxOutputBytes { get; init; } = 16_777_216;
    /// <summary>Gets the token used to cancel canonicalization and output.</summary>
    public CancellationToken CancellationToken
    {
        get; init;
    }
}
/// <summary>Writes deterministic canonical XML as UTF-8 bytes without closing the output stream.</summary>
public static class XmlCanonicalizer
{
    /// <summary>Canonicalizes a complete document.</summary>
    /// <param name="document">The document to canonicalize.</param>
    /// <param name="output">The writable, caller-owned stream.</param>
    /// <param name="options">Canonicalization options, or null for defaults.</param>
    /// <exception cref="ArgumentNullException">The document or output stream is null.</exception>
    /// <exception cref="ArgumentException">The output is unwritable or the options are invalid.</exception>
    /// <exception cref="InvalidOperationException">A processing limit is exceeded or the document changes during canonicalization.</exception>
    /// <remarks>Do not mutate the document during this call. Failures during output may leave partial bytes in the stream.</remarks>
    public static void Canonicalize(XmlDocument document, Stream output, XmlCanonicalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var operation = new Operation(output, options);
        operation.Run(document, null);
    }
    /// <summary>Canonicalizes a node set drawn from one document, preserving document order and applicable namespace context.</summary>
    /// <param name="nodes">The selected, connected XPath items. An empty selection produces no output.</param>
    /// <param name="output">The writable, caller-owned stream.</param>
    /// <param name="options">Canonicalization options, or null for defaults.</param>
    /// <exception cref="ArgumentNullException">The selection or output stream is null.</exception>
    /// <exception cref="ArgumentException">The selection contains default items or mixed owners, changes size during capture, or the options or output are invalid.</exception>
    /// <exception cref="InvalidOperationException">A processing limit is exceeded or the document changes during canonicalization.</exception>
    /// <remarks>Duplicate items are included once. Do not mutate the selection or its document during the call.</remarks>
    public static void Canonicalize(IReadOnlyList<XPathItem> nodes, Stream output, XmlCanonicalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var operation = new Operation(output, options);
        var selected = new HashSet<ItemIdentity>();
        XmlDocument? document = null;
        // Own identities, never retain the caller's mutable list.
        var count = nodes.Count;
        for (var i = 0; i < count; i++)
        {
            operation.Charge();
            var item = nodes[i];
            if (item.IsDefault)
            {
                throw new ArgumentException("Default XPath item.", nameof(nodes));
            }

            document ??= item.Document;
            if (!ReferenceEquals(document, item.Document))
            {
                throw new ArgumentException("Mixed XPath owners.", nameof(nodes));
            }

            XPathItem.RequireConnected(item.Identity.Node);
            operation.BindOwner(document);
            operation.ValidateItem(item);
            selected.Add(item.Identity);
        }
        if (nodes.Count != count)
        {
            throw new ArgumentException("Selection changed during snapshot.", nameof(nodes));
        }

        if (document is not null)
        {
            operation.Run(document, selected);
        }
    }

    // All state is owned by one call. Preflight captures references to immutable
    // DOM strings, selected identities, namespace context and synthesized XML
    // attributes. The output pass uses bounded UTF-8 scratch, never an owned result.
    private sealed class Operation
    {
        private readonly Stream output;
        private readonly XmlCanonicalizationOptions options;
        private readonly XmlResourceLimits limits;
        private readonly HashSet<string> inclusive = new(StringComparer.Ordinal);
        private long work, characters, bytes;
        private XmlDocument document = null!;
        private long revision;
        private readonly Action<long> meter;
        private readonly List<Piece> plan = [];
        private readonly byte[] buffer = new byte[4096];
        private int used;
        private sealed record Attribute(string Local, string Uri, string Prefix, string Value);
        private sealed record Piece(XmlNodeKind Kind, bool End, string Local, string Prefix, string Value,
            List<KeyValuePair<string, string>>? Namespaces = null, List<Attribute>? Attributes = null, int Outer = 0);
        private sealed class Frame(XmlNode node, Frame? parent, bool included, Dictionary<string, string> scope,
            Dictionary<string, string> bindings, Dictionary<string, string> rendered)
        {
            internal readonly XmlNode Node = node;
            internal readonly Frame? Parent = parent;
            internal readonly bool Included = included;
            internal readonly Dictionary<string, string> Scope = scope, Bindings = bindings, Rendered = rendered;
            internal XmlNode TextFirst;
            internal long TextScalars;
        }
        internal Operation(Stream output, XmlCanonicalizationOptions? options)
        {
            ArgumentNullException.ThrowIfNull(output);
            this.output = output;
            meter = Charge;
            this.options = options ?? new();
            this.options.CancellationToken.ThrowIfCancellationRequested();
            if (!output.CanWrite)
            {
                throw new ArgumentException("Writable stream required.", nameof(output));
            }

            if (!Enum.IsDefined(this.options.Mode))
            {
                throw new ArgumentException("Unknown canonicalization mode.", nameof(options));
            }

            if (this.options.MaxOutputBytes is <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            limits = this.options.Limits ?? throw new ArgumentException("Limits required.", nameof(options));
            limits.Validate();
            var prefixes = this.options.InclusivePrefixes ?? throw new ArgumentException("Prefix list required.", nameof(options));
            if (this.options.Mode != XmlCanonicalizationMode.Exclusive10 && prefixes.Count != 0)
            {
                throw new ArgumentException("Inclusive prefixes require exclusive mode.", nameof(options));
            }

            var count = prefixes.Count;
            for (var i = 0; i < count; i++)
            {
                var prefix = prefixes[i] ?? throw new ArgumentException("Null prefix.", nameof(options));
                Inspect(prefix);
                if (prefix != "#default")
                {
                    TreeValidation.Name(prefix);
                }

                inclusive.Add(prefix == "#default" ? "" : prefix);
            }
        }
        internal void Charge(long amount = 1)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            if (amount > long.MaxValue - work || limits.EvaluationWork is long cap && amount > cap - work)
            {
                throw new InvalidOperationException("Canonicalization work limit exceeded.");
            }

            work += amount;
        }
        private void Check()
        {
            Charge();
            if (document.ContentRevision < 0 || document.ContentRevision != revision)
            {
                throw new InvalidOperationException("Document mutated during canonicalization.");
            }
        }
        private long Inspect(string value, bool aggregate = false)
        {
            long count = 0;
            foreach (var rune in value.EnumerateRunes())
            {
                Charge();
                count++;
            }
            if (limits.TokenCharacters is int cap && count > cap)
            {
                throw new InvalidOperationException("Canonicalization token limit exceeded.");
            }

            if (aggregate)
            {
                if (count > long.MaxValue - characters || limits.DocumentCharacters is long total && count > total - characters)
                {
                    throw new InvalidOperationException("Canonicalization character limit exceeded.");
                }

                characters += count;
            }
            return count;
        }
        private int Compare(string a, string b)
        {
            var x = a.EnumerateRunes().GetEnumerator();
            var y = b.EnumerateRunes().GetEnumerator();
            while (true)
            {
                Charge();
                bool ax = x.MoveNext(), by = y.MoveNext();
                if (!ax || !by)
                {
                    return ax ? 1 : by ? -1 : 0;
                }

                var difference = x.Current.Value.CompareTo(y.Current.Value);
                if (difference != 0)
                {
                    return difference;
                }
            }
        }
        internal void BindOwner(XmlDocument owner)
        {
            if (document is not null)
            {
                Check();
                return;
            }
            document = owner;
            revision = owner.ContentRevision;
            Check();
            if (!owner.SourceInputComplete)
            {
                throw new InvalidOperationException("Incomplete source document.");
            }

            owner.RefuseUnresolvedReferences();
        }
        internal void ValidateItem(XPathItem item)
        {
            Check();
            if (item.CapturedContentRevision < 0 || item.CapturedContentRevision != revision)
            {
                throw new ArgumentException("Stale XPath snapshot.");
            }

            var node = item.Identity.Node;
            if (item.Kind == XPathItemKind.Attribute)
            {
                var attribute = item.Identity.Attribute;
                if (!attribute.IsAttached || attribute.OwnerElement != node || attribute.Value != item.StringValue || attribute.Prefix != item.Prefix)
                {
                    throw new ArgumentException("Stale XPath attribute.");
                }
            }
            else if (item.Kind == XPathItemKind.Namespace)
            {
                if (XPathItem.NamespaceUriFor(node, item.Identity.NamespacePrefix!) != item.StringValue)
                {
                    throw new ArgumentException("Stale XPath namespace.");
                }
            }
            else if (item.Kind == XPathItemKind.Text)
            {
                if (XPathItem.IsText(node.PreviousSibling))
                {
                    throw new ArgumentException("Stale logical text identity.");
                }

                var offset = 0;
                for (var member = node; XPathItem.IsText(member); member = member.NextSibling)
                {
                    Inspect(member.Value);
                    if (member.Value.Length > item.StringValue.Length - offset || !item.StringValue.AsSpan(offset, member.Value.Length).SequenceEqual(member.Value))
                    {
                        throw new ArgumentException("Stale XPath text.");
                    }

                    offset += member.Value.Length;
                }
                if (offset != item.StringValue.Length || offset == 0)
                {
                    throw new ArgumentException("Stale XPath text.");
                }
            }
            else if (item.Kind is XPathItemKind.Comment or XPathItemKind.ProcessingInstruction && node.Value != item.StringValue)
            {
                throw new ArgumentException("Stale XPath item.");
            }
        }
        internal void Run(XmlDocument owner, HashSet<ItemIdentity>? selected)
        {
            BindOwner(owner);
            Check();
            document.RefuseUnresolvedReferences();
            if (document.RootElement.IsDefault)
            {
                throw new InvalidOperationException("Complete document required.");
            }

            var empty = new Dictionary<string, string>(StringComparer.Ordinal);
            var root = new Frame(document.DocumentNode, null, true, empty, empty, empty);
            var stack = new Stack<(XmlNode Node, Frame Parent, bool End)>();
            for (var n = document.DocumentNode.LastChild; !n.IsDefault; n = n.PreviousSibling)
            {
                stack.Push((n, root, false));
            }

            bool Has(XPathItemKind kind, XmlNode node, XmlAttribute attribute = default, string? prefix = null) =>
                selected is null || selected.Contains(new(kind, node, attribute, prefix));
            while (stack.Count != 0)
            {
                Check();
                var (node, parent, end) = stack.Pop();
                if (end)
                {
                    if (parent.Included)
                    {
                        plan.Add(new(XmlNodeKind.Element, true, node.LocalName, node.Prefix, ""));
                    }

                    continue;
                }
                var kind = node.Kind;
                if (kind == XmlNodeKind.Element)
                {
                    var depth = 0;
                    for (var f = parent; f.Parent is not null; f = f.Parent)
                    {
                        Charge();
                        depth++;
                    }
                    if (limits.Depth is int maxDepth && depth + 1 > maxDepth)
                    {
                        throw new InvalidOperationException("Canonicalization depth limit exceeded.");
                    }

                    Inspect(node.LocalName, true);
                    Inspect(node.NamespaceUri, true);
                    Inspect(node.Prefix, true);
                    var scope = new Dictionary<string, string>(parent.Scope, StringComparer.Ordinal);
                    foreach (var ns in node.NamespaceDeclarations)
                    {
                        Charge();
                        Inspect(ns.Prefix, true);
                        Inspect(ns.Value, true);
                        RequireAbsolute(ns.Value);
                        scope[ns.Prefix] = ns.Value;
                    }
                    // Expanded names in manually built trees must agree with the retained namespace axis.
                    RequireAbsolute(node.NamespaceUri);
                    RequireBinding(scope, node.Prefix, node.NamespaceUri, false);
                    var include = Has(XPathItemKind.Element, node);
                    var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var ns in scope)
                    {
                        Charge();
                        if (ns.Value.Length != 0 && Has(XPathItemKind.Namespace, node, prefix: ns.Key))
                        {
                            bindings.Add(ns.Key, ns.Value);
                        }
                    }
                    var attributes = new List<Attribute>();
                    var attributeCount = 0;
                    foreach (var a in node.Attributes)
                    {
                        Charge();
                        attributeCount++;
                        if (limits.AttributesPerElement is int maxAttributes && attributeCount > maxAttributes)
                        {
                            throw new InvalidOperationException("Canonicalization attribute limit exceeded.");
                        }

                        Inspect(a.LocalName, true);
                        Inspect(a.NamespaceUri, true);
                        Inspect(a.Prefix, true);
                        Inspect(a.Value, true);
                        RequireAbsolute(a.NamespaceUri);
                        RequireBinding(scope, a.Prefix, a.NamespaceUri, true);
                        if (Has(XPathItemKind.Attribute, node, a))
                        {
                            attributes.Add(new(a.LocalName, a.NamespaceUri, a.Prefix, a.Value));
                        }
                    }
                    var nearest = parent;
                    while (!nearest.Included)
                    {
                        Charge();
                        nearest = nearest.Parent!;
                    }
                    var rendered = nearest.Rendered;
                    if (include)
                    {
                        if (options.Mode != XmlCanonicalizationMode.Exclusive10 && !parent.Included)
                        {
                            Inherit(node, parent, attributes);
                        }

                        Sort(attributes, (a, b) => { var c = Compare(a.Uri, b.Uri); return c != 0 ? c : Compare(a.Local, b.Local); });
                        var namespaces = new List<KeyValuePair<string, string>>();
                        var exclusive = options.Mode == XmlCanonicalizationMode.Exclusive10;
                        var visible = new HashSet<string>(inclusive, StringComparer.Ordinal);
                        if (exclusive)
                        {
                            visible.Add(node.Prefix);
                            foreach (var a in attributes)
                            {
                                if (a.Prefix.Length != 0)
                                {
                                    visible.Add(a.Prefix);
                                }
                            }
                        }
                        foreach (var binding in bindings)
                        {
                            Charge();
                            if (binding.Key == "xml" || exclusive && !visible.Contains(binding.Key))
                            {
                                continue;
                            }

                            var previous = exclusive ? rendered : nearest.Bindings;
                            if (!previous.TryGetValue(binding.Key, out var prior) || prior != binding.Value)
                            {
                                namespaces.Add(binding);
                            }
                        }
                        var priorBindings = exclusive ? rendered : nearest.Bindings;
                        if (!bindings.ContainsKey("") && (!exclusive || visible.Contains("")) && priorBindings.TryGetValue("", out var priorDefault) && priorDefault.Length != 0)
                        {
                            namespaces.Add(new("", ""));
                        }

                        Sort(namespaces, (a, b) => Compare(a.Key, b.Key));
                        rendered = new Dictionary<string, string>(rendered, StringComparer.Ordinal);
                        foreach (var ns in namespaces)
                        {
                            rendered[ns.Key] = ns.Value;
                        }

                        plan.Add(new(kind, false, node.LocalName, node.Prefix, "", namespaces, attributes));
                    }
                    var frame = new Frame(node, parent, include, scope, bindings, rendered);
                    stack.Push((node, frame, true));
                    for (var n = node.LastChild; !n.IsDefault; n = n.PreviousSibling)
                    {
                        Charge();
                        stack.Push((n, frame, false));
                    }
                }
                else if (XPathItem.IsText(node))
                {
                    var scalars = Inspect(node.Value, true);
                    if (!XPathItem.IsText(node.PreviousSibling))
                    {
                        parent.TextFirst = node;
                        parent.TextScalars = 0;
                    }
                    if (scalars > long.MaxValue - parent.TextScalars)
                    {
                        throw new InvalidOperationException("Text capacity exceeded.");
                    }

                    parent.TextScalars += scalars;
                    if (limits.TokenCharacters is int tokenCap && parent.TextScalars > tokenCap)
                    {
                        throw new InvalidOperationException("Canonicalization text token limit exceeded.");
                    }

                    if (Has(XPathItemKind.Text, parent.TextFirst))
                    {
                        plan.Add(new(XmlNodeKind.Text, false, "", "", node.Value));
                    }
                }
                else
                {
                    Inspect(node.LocalName, true);
                    Inspect(node.Value, true);
                    var itemKind = kind == XmlNodeKind.Comment ? XPathItemKind.Comment : XPathItemKind.ProcessingInstruction;
                    if (Has(itemKind, node) && (kind != XmlNodeKind.Comment || options.IncludeComments))
                    {
                        var outer = 0;
                        if (node.Parent.Kind == XmlNodeKind.Document)
                        {
                            outer = -1;
                            for (var n = node.PreviousSibling; !n.IsDefault; n = n.PreviousSibling)
                            {
                                Charge();
                                if (n.Kind == XmlNodeKind.Element)
                                {
                                    outer = 1;
                                    break;
                                }
                            }
                        }
                        plan.Add(new(kind, false, node.LocalName, "", node.Value, Outer: outer));
                    }
                }
            }
            Check();
            foreach (var piece in plan)
            {
                Render(piece);
            }

            Drain();
            Check();
        }
        private void Sort<T>(List<T> values, Comparison<T> comparison)
        {
            // Direct bounded merge sorting keeps cancellation/limit exceptions
            // intact; List.Sort wraps comparer exceptions as InvalidOperationException.
            if (values.Count < 2)
            {
                return;
            }

            var scratch = new T[values.Count];
            for (long width = 1; width < values.Count; width *= 2)
            {
                for (long start = 0; start < values.Count; start += 2 * width)
                {
                    int left = (int)start, middle = (int)Math.Min(start + width, values.Count), right = middle;
                    var end = (int)Math.Min(start + 2 * width, values.Count);
                    for (var dest = (int)start; dest < end; dest++)
                    {
                        Charge();
                        scratch[dest] = left < middle && (right >= end || comparison(values[left], values[right]) <= 0) ? values[left++] : values[right++];
                    }
                }
                for (var i = 0; i < values.Count; i++)
                {
                    Charge();
                    values[i] = scratch[i];
                }
            }
        }
        private void RequireAbsolute(string uri)
        {
            if (uri.Length == 0)
            {
                return;
            }
            // Absolute namespace names require a scheme. Probe borrowed characters;
            // do not allocate a platform Uri or normalize the retained namespace.
            if (uri[0] is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z'))
            {
                throw new ArgumentException("Relative namespace name.");
            }

            for (var i = 1; i < uri.Length; i++)
            {
                Charge();
                var c = uri[i];
                if (c == ':')
                {
                    return;
                }

                if (c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '+' or '-' or '.'))
                {
                    break;
                }
            }
            throw new ArgumentException("Relative namespace name.");
        }
        private static void RequireBinding(Dictionary<string, string> scope, string prefix, string uri, bool attribute)
        {
            var binding = prefix == "xml" ? TreeValidation.XmlUri :
                attribute && prefix.Length == 0 ? "" : scope.GetValueOrDefault(prefix, "");
            if (binding != uri || prefix.Length != 0 && binding.Length == 0)
            {
                throw new InvalidOperationException("Expanded name disagrees with its namespace binding.");
            }
        }
        private void Inherit(XmlNode node, Frame parent, List<Attribute> attributes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in node.Attributes)
            {
                if (attribute.NamespaceUri == TreeValidation.XmlUri)
                {
                    seen.Add(attribute.LocalName);
                }
            }

            var v11 = options.Mode == XmlCanonicalizationMode.Inclusive11;
            var bases = new List<string>();
            if (v11 && node.GetAttribute("base", TreeValidation.XmlUri) is string ownBase)
            {
                bases.Add(ownBase);
            }

            var omittedBase = false;
            var baseBoundary = false;
            for (var f = parent; f.Parent is not null; f = f.Parent!)
            {
                Charge();
                // The omitted xml:base chain ends at any included ancestor,
                // while xml:lang/xml:space still inspect the full ancestor chain.
                if (f.Included)
                {
                    baseBoundary = true;
                }

                foreach (var a in f.Node.Attributes)
                {
                    Charge();
                    if (a.NamespaceUri != TreeValidation.XmlUri)
                    {
                        continue;
                    }

                    if (v11 && a.LocalName == "base")
                    {
                        if (!baseBoundary && !f.Included)
                        {
                            bases.Add(a.Value);
                            omittedBase = true;
                        }
                        continue;
                    }
                    if (v11 && a.LocalName is not ("lang" or "space"))
                    {
                        continue;
                    }

                    if (seen.Add(a.LocalName))
                    {
                        attributes.Add(new(a.LocalName, a.NamespaceUri, a.Prefix, a.Value));
                    }
                }
            }
            if (v11 && omittedBase)
            {
                var combined = bases[0];
                for (var i = 1; i < bases.Count; i++)
                {
                    Inspect(bases[i]);
                    Inspect(combined);
                    combined = BaseReferences.Join(bases[i], combined, meter);
                }
                attributes.RemoveAll(a => a.Uri == TreeValidation.XmlUri && a.Local == "base");
                Inspect(combined);
                if (combined.Length != 0)
                {
                    attributes.Add(new("base", TreeValidation.XmlUri, "xml", combined));
                }
            }
        }
        private void Render(Piece p)
        {
            Check();
            if (p.Kind == XmlNodeKind.Element)
            {
                Literal(p.End ? "</"u8 : "<"u8);
                Name(p.Prefix, p.Local);
                if (!p.End)
                {
                    foreach (var ns in p.Namespaces!)
                    {
                        Literal(" xmlns"u8);
                        if (ns.Key.Length != 0)
                        {
                            Literal(":"u8);
                            Utf8(ns.Key);
                        }
                        Literal("=\""u8);
                        Escape(ns.Value, 1);
                        Literal("\""u8);
                    }
                    foreach (var a in p.Attributes!)
                    {
                        Literal(" "u8);
                        Name(a.Prefix, a.Local);
                        Literal("=\""u8);
                        Escape(a.Value, 1);
                        Literal("\""u8);
                    }
                }
                Literal(">"u8);
            }
            else if (p.Kind == XmlNodeKind.Text)
            {
                Escape(p.Value, 0);
            }
            else
            {
                if (p.Outer == 1)
                {
                    Literal("\n"u8);
                }

                if (p.Kind == XmlNodeKind.Comment)
                {
                    Literal("<!--"u8);
                    Escape(p.Value, 2);
                    Literal("-->"u8);
                }
                else
                {
                    Literal("<?"u8);
                    Utf8(p.Local);
                    if (p.Value.Length != 0)
                    {
                        Literal(" "u8);
                        Escape(p.Value, 2);
                    }
                    Literal("?>"u8);
                }
                if (p.Outer == -1)
                {
                    Literal("\n"u8);
                }
            }
        }
        private void Name(string prefix, string local)
        {
            if (prefix.Length != 0)
            {
                Utf8(prefix);
                Literal(":"u8);
            }
            Utf8(local);
        }
        private void Escape(string text, int mode)
        {
            ReadOnlySpan<char> remaining = text;
            while (!remaining.IsEmpty)
            {
                var count = CanonicalEscapes.Encode(remaining, buffer.AsSpan(used), mode, out var consumed, meter);
                if (count == 0)
                {
                    Drain();
                    continue;
                }
                AddBytes(count);
                used += count;
                remaining = remaining[consumed..];
            }
        }
        private void Utf8(string value) => Escape(value, 3);
        private void AddBytes(int count)
        {
            if (count > long.MaxValue - bytes || options.MaxOutputBytes is long cap && count > cap - bytes)
            {
                throw new InvalidOperationException("Canonicalization output limit exceeded.");
            }

            bytes += count;
        }
        private void Literal(ReadOnlySpan<byte> value)
        {
            if (buffer.Length - used < value.Length)
            {
                Drain();
            }

            AddBytes(value.Length);
            value.CopyTo(buffer.AsSpan(used));
            used += value.Length;
        }
        private void Drain()
        {
            Check();
            if (used == 0)
            {
                return;
            }

            output.Write(buffer.AsSpan(0, used));
            used = 0;
            Check();
        }
    }
}

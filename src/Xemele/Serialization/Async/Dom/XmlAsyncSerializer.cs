using System.Buffers;
using System.Globalization;
using System.Text;

namespace Xemele;

/// <summary>Serializes an owned DOM incrementally through asynchronous stream writes.</summary>
public static class XmlAsyncSerializer
{
    /// <summary>Serializes a document through asynchronous writes without closing the output stream.</summary>
    /// <param name="document">The document with exactly one root element and no unresolved references.</param>
    /// <param name="output">The writable, caller-owned stream.</param>
    /// <param name="options">Serialization options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling serialization and writes.</param>
    /// <returns>A task representing serialization and completion of buffered writes.</returns>
    /// <exception cref="ArgumentException">The stream is unwritable or the options are unsupported.</exception>
    /// <exception cref="InvalidOperationException">The tree is not serializable as an XML document.</exception>
    /// <remarks>Failures can leave partial output. Do not mutate the document until the returned task completes.</remarks>
    public static Task SerializeAsync(XmlDocument document, Stream output, XmlSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("Stream must be writable.", nameof(output));
        }

        var (Options, CodePage) = Prepare(document, options, cancellationToken);
        return SerializeCoreAsync(document, output, Options, CodePage, cancellationToken);
    }

    private static async Task SerializeCoreAsync(XmlDocument document, Stream stream, XmlSaveOptions options, int codePage, CancellationToken token)
    {
        // Disposing this internal scratch owner never touches the caller stream.
        using var output = new WriterOutput(stream, codePage, token);
        if (options.EmitByteOrderMark)
        {
            await output.PreambleAsync(token).ConfigureAwait(false);
        }

        await new SerializationOperation(options, output, token).RunAsync(document).ConfigureAwait(false);
        await output.CompleteAsync(token).ConfigureAwait(false);
    }
    private static (XmlSaveOptions Options, int CodePage) Prepare(XmlDocument document, XmlSaveOptions? options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(document);
        token.ThrowIfCancellationRequested();
        options ??= new();
        if (options.Encoding is null || options.NamespaceHandling is not (XmlNamespaceHandling.Strict or XmlNamespaceHandling.Repair))
        {
            throw new ArgumentException("Invalid save options.", nameof(options));
        }
        // Only the code page is retained. A fresh strict encoder ignores mutable
        // caller fallback/preamble state and never invokes a caller encoding.
        var codePage = options.Encoding.CodePage;
        if (codePage is not (65001 or 1200 or 1201))
        {
            throw new ArgumentException("Unsupported encoding.", nameof(options));
        }

        document.RefuseUnresolvedReferences();
        int roots = 0, work = 0;
        foreach (var child in document.DocumentNode.Children)
        {
            if ((++work & 2047) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            if (child.Kind == XmlNodeKind.Element)
            {
                roots++;
            }
            else if (child.Kind is not (XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction))
            {
                throw new InvalidOperationException("Illegal document child.");
            }
        }
        if (roots != 1)
        {
            throw new InvalidOperationException("Exactly one root element is required.");
        }

        return (options with
        {
            Encoding = WriterOutput.StrictEncoding(codePage)
        }, codePage);
    }
    private sealed class SerializationOperation(XmlSaveOptions options, WriterOutput output, CancellationToken token)
    {
        private readonly Dictionary<string, string> scope = new(StringComparer.Ordinal) { ["xml"] = TreeValidation.XmlUri };
        // Reverse index supports deterministic prefix reuse without rescanning
        // every ancestor binding for each attribute.
        private readonly Dictionary<string, SortedSet<string>> byUri = new(StringComparer.Ordinal);
        private readonly List<Undo> undo = [];
        private readonly List<Frame> frames = [];
        private readonly List<Declaration> declarations = [];
        private readonly List<string> attributePrefixes = [];
        private readonly HashSet<string> local = new(StringComparer.Ordinal);
        private readonly HashSet<string> reserved = new(StringComparer.Ordinal);
        private readonly HashSet<(string, string)> expanded = [];
        private int generated, steps, brackets;
        internal long Work
        {
            get; private set;
        }
        internal long BindingWork
        {
            get; private set;
        }
        internal int MaxDepth
        {
            get; private set;
        }
        private readonly record struct Undo(string Prefix, string? Old);
        private readonly record struct Declaration(string Prefix, string Uri);
        private readonly record struct Frame(XmlNode Node, string Prefix, int Mark, bool Mixed, int Depth);
        private void Tick()
        {
            Work++;
            if ((++steps & 2047) == 0)
            {
                token.ThrowIfCancellationRequested();
            }
        }
        private static void Invalid() => throw new InvalidOperationException("Invalid XML serializable tree state.");
        internal async ValueTask RunAsync(XmlDocument document)
        {
            if (!options.OmitXmlDeclaration)
            {
                await output.WriteAsync("<?xml version=\"1.0\"", token).ConfigureAwait(false);
                {
                    await output.WriteAsync(" encoding=\"", token).ConfigureAwait(false);
                    await output.WriteAsync(output.Label, token).ConfigureAwait(false);
                    await output.WriteAsync("\"", token).ConfigureAwait(false);
                }
                await output.WriteAsync("?>", token).ConfigureAwait(false);
            }
            var first = true;
            foreach (var child in document.DocumentNode.Children)
            {
                if (options.Indent && (!first || !options.OmitXmlDeclaration))
                {
                    await output.WriteAsync("\n", token).ConfigureAwait(false);
                }

                first = false;
                await VisitAsync(child, 0).ConfigureAwait(false);
                while (frames.Count != 0)
                {
                    await TraverseChildrenAsync(frames[^1].Node.FirstChild).ConfigureAwait(false);
                }
            }
        }
        private async ValueTask TraverseChildrenAsync(XmlNode next)
        {
            while (true)
            {
                Tick();
                if (!next.IsDefault)
                {
                    var parent = frames[^1];
                    if (options.Indent && !parent.Mixed)
                    {
                        await IndentAsync(checked(parent.Depth + 1)).ConfigureAwait(false);
                    }

                    var sibling = next.NextSibling;
                    var count = frames.Count;
                    await VisitAsync(next, checked(parent.Depth + 1)).ConfigureAwait(false);
                    if (frames.Count != count)
                    {
                        next = next.FirstChild;
                        continue;
                    }
                    next = sibling;
                    continue;
                }
                var closing = frames[^1];
                if (options.Indent && !closing.Mixed)
                {
                    await IndentAsync(closing.Depth).ConfigureAwait(false);
                }

                await output.WriteAsync("</", token).ConfigureAwait(false);
                await QNameAsync(closing.Prefix, closing.Node.LocalName).ConfigureAwait(false);
                await output.WriteAsync(">", token).ConfigureAwait(false);
                brackets = 0;
                Restore(closing.Mark);
                frames.RemoveAt(frames.Count - 1);
                if (frames.Count == 0)
                {
                    break;
                }

                next = closing.Node.NextSibling;
            }
        }
        private async ValueTask VisitAsync(XmlNode node, int depth)
        {
            Tick();
            if (node.Kind == XmlNodeKind.Element)
            {
                await ElementAsync(node, depth).ConfigureAwait(false);
                return;
            }
            if (node.Kind == XmlNodeKind.Text)
            {
                await EscapeAsync(node.Value, false).ConfigureAwait(false);
                return;
            }
            brackets = 0;
            ValidateText(node.Value);
            switch (node.Kind)
            {
                case XmlNodeKind.CData:
                    Delimiter(node.Value, "]]>");
                    await output.WriteAsync("<![CDATA[", token).ConfigureAwait(false);
                    await output.WriteAsync(node.Value, token).ConfigureAwait(false);
                    await output.WriteAsync("]]>", token).ConfigureAwait(false);
                    break;
                case XmlNodeKind.Comment:
                    Delimiter(node.Value, "--");
                    if (node.Value.EndsWith('-'))
                    {
                        Invalid();
                    }

                    await output.WriteAsync("<!--", token).ConfigureAwait(false);
                    await output.WriteAsync(node.Value, token).ConfigureAwait(false);
                    await output.WriteAsync("-->", token).ConfigureAwait(false);
                    break;
                case XmlNodeKind.ProcessingInstruction:
                    ValidateName(node.LocalName);
                    if (node.LocalName.Equals("xml", StringComparison.OrdinalIgnoreCase))
                    {
                        Invalid();
                    }

                    Delimiter(node.Value, "?>");
                    await output.WriteAsync("<?", token).ConfigureAwait(false);
                    await output.WriteAsync(node.LocalName, token).ConfigureAwait(false);
                    if (node.Value.Length != 0)
                    {
                        await output.WriteAsync(" ", token).ConfigureAwait(false);
                        await output.WriteAsync(node.Value, token).ConfigureAwait(false);
                    }
                    await output.WriteAsync("?>", token).ConfigureAwait(false);
                    break;
                default:
                    Invalid();
                    break;
            }
        }
        private async ValueTask ElementAsync(XmlNode node, int depth)
        {
            brackets = 0;
            ValidateExpanded(node.LocalName, node.NamespaceUri, node.Prefix);
            var mark = undo.Count;
            declarations.Clear();
            attributePrefixes.Clear();
            local.Clear();
            reserved.Clear();
            expanded.Clear();
            reserved.Add(node.Prefix);
            foreach (var a in node.Attributes)
            {
                Tick();
                ValidateExpanded(a.LocalName, a.NamespaceUri, a.Prefix, attribute: true);
                if (!expanded.Add((a.NamespaceUri, a.LocalName)))
                {
                    Invalid();
                }

                reserved.Add(a.Prefix);
            }
            foreach (var n in node.NamespaceDeclarations)
            {
                Tick();
                ValidateBinding(n.Prefix, n.Value);
                if (!local.Add(n.Prefix))
                {
                    Invalid();
                }

                reserved.Add(n.Prefix);
                var uri = n.Value;
                if (options.NamespaceHandling == XmlNamespaceHandling.Repair && n.Prefix.Length == 0 && node.NamespaceUri.Length == 0)
                {
                    uri = "";
                }

                declarations.Add(new(n.Prefix, uri));
                Bind(n.Prefix, uri);
            }
            var prefix = Resolve(node.Prefix, node.NamespaceUri, false);
            foreach (var a in node.Attributes)
            {
                Tick();
                attributePrefixes.Add(Resolve(a.Prefix, a.NamespaceUri, true));
            }
            await output.WriteAsync("<", token).ConfigureAwait(false);
            await QNameAsync(prefix, node.LocalName).ConfigureAwait(false);
            foreach (var n in declarations)
            {
                Tick();
                await output.WriteAsync(" xmlns", token).ConfigureAwait(false);
                if (n.Prefix.Length != 0)
                {
                    await output.WriteAsync(":", token).ConfigureAwait(false);
                    await output.WriteAsync(n.Prefix, token).ConfigureAwait(false);
                }
                await output.WriteAsync("=\"", token).ConfigureAwait(false);
                await EscapeAsync(n.Uri, true).ConfigureAwait(false);
                await output.WriteAsync("\"", token).ConfigureAwait(false);
            }
            var index = 0;
            foreach (var a in node.Attributes)
            {
                Tick();
                await output.WriteAsync(" ", token).ConfigureAwait(false);
                await QNameAsync(attributePrefixes[index++], a.LocalName).ConfigureAwait(false);
                await output.WriteAsync("=\"", token).ConfigureAwait(false);
                await EscapeAsync(a.Value, true).ConfigureAwait(false);
                await output.WriteAsync("\"", token).ConfigureAwait(false);
            }
            if (node.FirstChild.IsDefault)
            {
                await output.WriteAsync("/>", token).ConfigureAwait(false);
                Restore(mark);
                return;
            }
            var mixed = false;
            foreach (var child in node.Children)
            {
                Tick();
                if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
                {
                    mixed = true;
                }
            }
            await output.WriteAsync(">", token).ConfigureAwait(false);
            frames.Add(new(node, prefix, mark, mixed, depth));
            MaxDepth = Math.Max(MaxDepth, frames.Count);
        }
        private string Binding(string prefix) => scope.TryGetValue(prefix, out var uri) ? uri : "";
        private string Resolve(string prefix, string uri, bool attribute)
        {
            if (uri == TreeValidation.XmlUri)
            {
                return "xml";
            }

            if (attribute && uri.Length == 0)
            {
                return "";
            }

            var matches = (!attribute || prefix.Length != 0) && Binding(prefix) == uri;
            if (matches)
            {
                return prefix;
            }

            if (options.NamespaceHandling == XmlNamespaceHandling.Strict)
            {
                Invalid();
            }

            if (!attribute && uri.Length == 0)
            {
                Generate("", "");
                return "";
            }
            if (!attribute && !local.Contains(prefix))
            {
                Generate(prefix, uri);
                return prefix;
            }
            if (attribute && byUri.TryGetValue(uri, out var prefixes) && prefixes.Count != 0)
            {
                return prefixes.Min!;
            }

            string fresh;
            do
            {
                Tick();
                generated = checked(generated + 1);
                fresh = "ns" + generated.ToString(CultureInfo.InvariantCulture);
            }
            while (scope.ContainsKey(fresh) || reserved.Contains(fresh));
            Generate(fresh, uri);
            return fresh;
        }
        private void Generate(string prefix, string uri)
        {
            declarations.Add(new(prefix, uri));
            local.Add(prefix);
            Bind(prefix, uri);
        }
        private void Bind(string prefix, string uri)
        {
            var old = scope.TryGetValue(prefix, out var previous) ? previous : null;
            undo.Add(new(prefix, old));
            SetBinding(prefix, uri);
        }
        private void SetBinding(string prefix, string? uri)
        {
            Tick();
            BindingWork++;
            if (prefix.Length != 0 && scope.TryGetValue(prefix, out var old) && byUri.TryGetValue(old, out var previous))
            {
                previous.Remove(prefix);
                if (previous.Count == 0)
                {
                    byUri.Remove(old);
                }
            }
            if (uri is null)
            {
                scope.Remove(prefix);
                return;
            }
            scope[prefix] = uri;
            if (prefix.Length != 0 && prefix != "xml")
            {
                if (!byUri.TryGetValue(uri, out var prefixes))
                {
                    byUri.Add(uri, prefixes = new(StringComparer.Ordinal));
                }

                prefixes.Add(prefix);
            }
        }
        private void Restore(int mark)
        {
            for (var i = undo.Count - 1; i >= mark; i--)
            {
                Tick();
                SetBinding(undo[i].Prefix, undo[i].Old);
            }
            undo.RemoveRange(mark, undo.Count - mark);
        }
        private async ValueTask QNameAsync(string prefix, string localName)
        {
            if (prefix.Length != 0)
            {
                await output.WriteAsync(prefix, token).ConfigureAwait(false);
                await output.WriteAsync(":", token).ConfigureAwait(false);
            }
            await output.WriteAsync(localName, token).ConfigureAwait(false);
        }
        private async ValueTask IndentAsync(int depth)
        {
            await output.WriteAsync("\n", token).ConfigureAwait(false);
            // Fixed literal blocks avoid constructing depth-sized indentation.
            const string spaces = "                                                                ";
            var count = checked((long)depth * 2);
            while (count != 0)
            {
                Tick();
                var n = (int)Math.Min(count, spaces.Length);
                await output.WriteAsync(spaces.AsMemory(0, n), token).ConfigureAwait(false);
                count -= n;
            }
        }
        private static bool Scalar(int c) => c is 9 or 10 or 13 or >= 0x20 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000 and <= 0x10ffff;
        private static bool Start(int c) => c is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= 0xc0 and <= 0xd6 or >= 0xd8 and <= 0xf6 or >= 0xf8 and <= 0x2ff or >= 0x370 and <= 0x37d or >= 0x37f and <= 0x1fff or >= 0x200c and <= 0x200d or >= 0x2070 and <= 0x218f or >= 0x2c00 and <= 0x2fef or >= 0x3001 and <= 0xd7ff or >= 0xf900 and <= 0xfdcf or >= 0xfdf0 and <= 0xfffd or >= 0x10000 and <= 0xeffff;
        private int ReadScalar(ReadOnlySpan<char> text, out int width)
        {
            Tick();
            if (Rune.DecodeFromUtf16(text, out var rune, out width) != OperationStatus.Done || !Scalar(rune.Value))
            {
                Invalid();
            }
            return rune.Value;
        }
        private void ValidateText(string value)
        {
            ReadOnlySpan<char> rest = value;
            while (!rest.IsEmpty)
            {
                ReadScalar(rest, out var width);
                rest = rest[width..];
            }
        }
        private void ValidateName(string name, bool colon = false)
        {
            if (name.Length == 0)
            {
                Invalid();
            }

            ReadOnlySpan<char> rest = name;
            var first = true;
            while (!rest.IsEmpty)
            {
                var c = ReadScalar(rest, out var width);
                if (!(Start(c) || colon && c == ':' || !first && c is '-' or '.' or >= '0' and <= '9' or 0xb7 or >= 0x300 and <= 0x36f or >= 0x203f and <= 0x2040))
                {
                    Invalid();
                }

                first = false;
                rest = rest[width..];
            }
        }
        private void ValidateBinding(string prefix, string uri)
        {
            if (prefix.Length != 0)
            {
                ValidateName(prefix);
            }

            ValidateText(uri);
            if (prefix == "xmlns" || uri == TreeValidation.XmlnsUri || (prefix == "xml") != (uri == TreeValidation.XmlUri) || prefix.Length != 0 && uri.Length == 0)
            {
                Invalid();
            }
        }
        private void ValidateExpanded(string localName, string uri, string prefix, bool attribute = false)
        {
            ValidateName(localName);
            ValidateBinding(prefix, uri);
            if (attribute && localName == "xmlns" && uri.Length == 0 && prefix.Length == 0)
            {
                Invalid();
            }
        }
        private void Delimiter(string value, ReadOnlySpan<char> delimiter)
        {
            // Bounded chunk search with overlap, including boundary matches.
            ReadOnlySpan<char> rest = value;
            while (!rest.IsEmpty)
            {
                token.ThrowIfCancellationRequested();
                var n = Math.Min(rest.Length, 2048);
                if (rest[..Math.Min(rest.Length, n + delimiter.Length - 1)].Contains(delimiter, StringComparison.Ordinal))
                {
                    Invalid();
                }

                rest = rest[n..];
            }
        }
        // Borrow scalar spans only in synchronous segments; immutable DOM strings
        // own every memory slice retained by the bounded encoder across awaits.
        private async ValueTask EscapeAsync(string value, bool attribute)
        {
            int run = 0, sinceWrite = 0;
            for (var i = 0; i < value.Length;)
            {
                var c = ReadScalar(value.AsSpan(i), out var width);
                var replacement = c switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '"' when attribute => "&quot;",
                    9 when attribute => "&#x9;",
                    10 when attribute => "&#xA;",
                    13 => "&#xD;",
                    '>' when !attribute && brackets >= 2 => "&gt;",
                    _ => null
                };
                if (replacement is not null)
                {
                    await output.WriteAsync(value.AsMemory(run, i - run), token).ConfigureAwait(false);
                    await output.WriteAsync(replacement, token).ConfigureAwait(false);
                    run = i + width;
                    sinceWrite = 0;
                }
                if (!attribute)
                {
                    brackets = c == ']' ? Math.Min(2, brackets + 1) : 0;
                }

                i += width;
                sinceWrite += width;
                if (sinceWrite >= 2048)
                {
                    await output.WriteAsync(value.AsMemory(run, i - run), token).ConfigureAwait(false);
                    run = i;
                    sinceWrite = 0;
                }
            }
            await output.WriteAsync(value.AsMemory(run), token).ConfigureAwait(false);
        }
    }
}

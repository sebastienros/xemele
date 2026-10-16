using System.Buffers;
using System.Globalization;
using System.Text;
namespace Xemele;

/// <summary>A sequential, forward-only XML 1.0 writer that leaves its stream open.</summary>
/// <remarks>Calls must not overlap or reenter the writer. Cancellation or a failure after state mutation faults the writer.
/// Finish all elements before ending the document. Disposal only releases buffers; it neither completes the document nor flushes pending output.</remarks>
public sealed partial class XmlWriter : IDisposable
{
    private readonly XmlSaveOptions options;
    private readonly WriterOutput output;
    private readonly CancellationToken token;
    private readonly List<Frame> frames = [];
    private readonly List<AttributeRow> attributes = [];
    private readonly List<Declaration> declarations = [];
    private readonly List<Undo> undo = [];
    private readonly Dictionary<string, string> scope = new(StringComparer.Ordinal) { ["xml"] = TreeValidation.XmlUri };
    private readonly HashSet<string> local = new(StringComparer.Ordinal);
    private readonly HashSet<string> reserved = new(StringComparer.Ordinal);
    private readonly HashSet<(string, string)> expanded = [];
    private readonly List<string> attributePrefixes = [];
    private bool started, root, completed, faulted, disposed, mutating, documentNode;
    private int busy, generated, brackets, steps;
    private enum ContentMode
    {
        Undecided, Mixed, Elements
    }
    private record struct Frame(string Name, string Uri, string Prefix, int Mark, bool Sealed, ContentMode Mode);
    private readonly record struct AttributeRow(string Name, string Value, string Uri, string Prefix);
    private readonly record struct Declaration(string Prefix, string Uri);
    private readonly record struct Undo(string Prefix, string? Old);
    private XmlWriter(Stream stream, XmlSaveOptions snapshot, int page, CancellationToken cancellationToken)
    {
        options = snapshot;
        token = cancellationToken;
        output = new(stream, page, cancellationToken);
    }
    /// <summary>Creates a forward writer over a caller-owned stream.</summary>
    /// <param name="output">The writable stream, left open when the writer is disposed.</param>
    /// <param name="options">Serialization options, or null for defaults.</param>
    /// <param name="cancellationToken">A token observed throughout the writer's lifetime.</param>
    /// <returns>A writer that has not yet emitted a document declaration or content.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="ArgumentException">The stream is unwritable or options are unsupported.</exception>
    public static XmlWriter Create(Stream output, XmlSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("Stream must be writable.", nameof(output));
        }

        cancellationToken.ThrowIfCancellationRequested();
        options ??= new();
        if (options.Encoding is null || options.NamespaceHandling is not (XmlNamespaceHandling.Strict or XmlNamespaceHandling.Repair))
        {
            throw new ArgumentException("Invalid save options.", nameof(options));
        }

        var page = options.Encoding.CodePage;
        if (page is not (65001 or 1200 or 1201))
        {
            throw new ArgumentException("Unsupported encoding.", nameof(options));
        }

        return new(output, options with
        {
            Encoding = WriterOutput.StrictEncoding(page)
        }, page, cancellationToken);
    }
    // Shared operation guard: later async partials retain it across awaits.
    private void Enter(bool allowCompleted = false, bool disposing = false)
    {
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
        {
            Invalid();
        }

        try
        {
            if (disposing)
            {
                return;
            }

            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted || completed && !allowCompleted)
            {
                Invalid();
            }

            if (token.IsCancellationRequested)
            {
                faulted = true;
                token.ThrowIfCancellationRequested();
            }
            mutating = false;
        }
        catch { Volatile.Write(ref busy, 0); throw; }
    }
    private void Exit() => Volatile.Write(ref busy, 0);
    private void Fail(Exception error)
    {
        if (mutating || error is OperationCanceledException)
        {
            faulted = true;
        }
    }
    private static void Invalid() => throw new InvalidOperationException("Invalid XML writer state or namespace binding.");
    private void Mutable() => mutating = true;
    private void ActiveElement()
    {
        if (frames.Count == 0)
        {
            Invalid();
        }
    }
    private void Pending()
    {
        ActiveElement();
        if (frames[^1].Sealed)
        {
            Invalid();
        }
    }
    private void Tick()
    {
        if ((++steps & 2047) == 0)
        {
            token.ThrowIfCancellationRequested();
        }
    }
    /// <summary>Starts the document, emitting the configured byte-order mark and XML declaration.</summary>
    /// <exception cref="InvalidOperationException">The document has already started or the writer cannot accept a write.</exception>
    /// <remarks>Other content-writing methods start the document automatically if needed.</remarks>
    public void WriteStartDocument()
    {
        Enter();
        try
        {
            if (started)
            {
                Invalid();
            }

            Mutable();
            Start();
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Starts an element and opens its pending start tag for attributes and namespace declarations.</summary>
    /// <param name="localName">The element's local name.</param>
    /// <param name="namespaceUri">The element's namespace URI, or an empty string for no namespace.</param>
    /// <param name="prefix">The requested prefix, or an empty string for an unprefixed name.</param>
    /// <exception cref="ArgumentException">A name or namespace argument is invalid.</exception>
    /// <exception cref="InvalidOperationException">A second document element is requested or the writer cannot accept a write.</exception>
    public void WriteStartElement(string localName, string namespaceUri = "", string prefix = "")
    {
        Enter();
        try
        {
            ValidateExpanded(localName, namespaceUri, prefix);
            if (frames.Count == 0 && root)
            {
                Invalid();
            }

            Mutable();
            Start();
            Structural();
            var mark = undo.Count;
            frames.Add(new(localName, namespaceUri, prefix, mark, false, ContentMode.Undecided));
            if (frames.Count == 1)
            {
                root = true;
            }

            attributes.Clear();
            declarations.Clear();
            local.Clear();
            reserved.Clear();
            expanded.Clear();
            reserved.Add(prefix);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Adds a namespace declaration to the pending start tag.</summary>
    /// <param name="prefix">The prefix to bind, or an empty string for the default namespace.</param>
    /// <param name="namespaceUri">The namespace URI to bind.</param>
    /// <exception cref="InvalidOperationException">No unsealed start tag exists, the prefix is already declared on it, or the writer cannot accept a write.</exception>
    /// <exception cref="ArgumentException">The binding is invalid.</exception>
    public void WriteNamespace(string prefix, string namespaceUri)
    {
        Enter();
        try
        {
            Pending();
            ValidateBinding(prefix, namespaceUri);
            if (local.Contains(prefix))
            {
                Invalid();
            }

            Mutable();
            local.Add(prefix);
            reserved.Add(prefix);
            declarations.Add(new(prefix, namespaceUri));
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Adds an attribute to the pending start tag.</summary>
    /// <param name="localName">The attribute's local name.</param>
    /// <param name="value">The unescaped attribute value.</param>
    /// <param name="namespaceUri">The namespace URI, or an empty string for no namespace.</param>
    /// <param name="prefix">The requested prefix.</param>
    /// <exception cref="ArgumentException">The name, namespace arguments, or value contain invalid XML.</exception>
    /// <exception cref="InvalidOperationException">No unsealed start tag exists, the expanded name is duplicated, or the writer cannot accept a write.</exception>
    public void WriteAttribute(string localName, string value, string namespaceUri = "", string prefix = "")
    {
        Enter();
        try
        {
            Pending();
            ValidateExpanded(localName, namespaceUri, prefix, attribute: true);
            ValidateText(value);
            if (expanded.Contains((namespaceUri, localName)))
            {
                Invalid();
            }

            Mutable();
            expanded.Add((namespaceUri, localName));
            reserved.Add(prefix);
            attributes.Add(new(localName, value, namespaceUri, prefix));
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Writes escaped character content inside the current element.</summary>
    /// <param name="text">The unescaped characters, borrowed only for this call.</param>
    /// <exception cref="ArgumentException">The content contains invalid XML characters.</exception>
    /// <exception cref="InvalidOperationException">There is no active element, mixed content conflicts with prior indentation, or the writer cannot accept a write.</exception>
    public void WriteText(ReadOnlySpan<char> text)
    {
        Enter();
        try
        {
            ActiveElement();
            ValidateText(text);
            CanMix();
            Mutable();
            Mix();
            Seal(false);
            Escape(text, false);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Writes a CDATA section inside the current element.</summary>
    /// <param name="text">The section content, borrowed only for this call; it must not contain the CDATA closing delimiter.</param>
    /// <exception cref="ArgumentException">The content contains invalid XML characters or the closing delimiter.</exception>
    /// <exception cref="InvalidOperationException">There is no active element, mixed content conflicts with prior indentation, or the writer cannot accept a write.</exception>
    public void WriteCData(ReadOnlySpan<char> text)
    {
        Enter();
        try
        {
            ActiveElement();
            ValidateText(text);
            Delimiter(text, "]]>");
            CanMix();
            Mutable();
            Mix();
            Seal(false);
            brackets = 0;
            output.Write("<![CDATA[");
            output.Write(text);
            output.Write("]]>");
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Writes a comment inside or outside the document element.</summary>
    /// <param name="text">The comment content, borrowed only for this call; it must not contain two consecutive hyphens or end with a hyphen.</param>
    /// <exception cref="ArgumentException">The comment content is not valid XML comment text.</exception>
    public void WriteComment(ReadOnlySpan<char> text)
    {
        Enter();
        try
        {
            ValidateText(text);
            Delimiter(text, "--");
            if (text.EndsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException("Illegal comment delimiter.", nameof(text));
            }

            Mutable();
            Start();
            Structural();
            brackets = 0;
            output.Write("<!--");
            output.Write(text);
            output.Write("-->");
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Writes a processing instruction inside or outside the document element.</summary>
    /// <param name="target">The instruction target, which must not be XML in any letter case.</param>
    /// <param name="data">The data, borrowed only for this call; it must not contain the processing-instruction closing delimiter.</param>
    /// <exception cref="ArgumentException">The target or instruction data is invalid.</exception>
    public void WriteProcessingInstruction(string target, ReadOnlySpan<char> data)
    {
        Enter();
        try
        {
            ValidateName(target, colon: true);
            ValidateText(data);
            Delimiter(data, "?>");
            if (target.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Reserved PI target.", nameof(target));
            }

            Mutable();
            Start();
            Structural();
            brackets = 0;
            output.Write("<?");
            output.Write(target);
            if (!data.IsEmpty)
            {
                output.Write(" ");
                output.Write(data);
            }
            output.Write("?>");
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Closes the current element, using empty-element syntax if its start tag is still pending.</summary>
    /// <exception cref="InvalidOperationException">There is no active element, namespace bindings are invalid, or the writer cannot accept a write.</exception>
    public void WriteEndElement()
    {
        Enter();
        try
        {
            ActiveElement();
            Mutable();
            var frame = frames[^1];
            if (!frame.Sealed)
            {
                Seal(true);
            }
            else
            {
                if (options.Indent && frame.Mode == ContentMode.Elements)
                {
                    Indent(frames.Count - 1);
                }

                output.Write("</");
                QName(frame.Prefix, frame.Name);
                output.Write(">");
            }
            Restore(frame.Mark);
            frames.RemoveAt(frames.Count - 1);
            brackets = 0;
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Completes buffered byte output and marks the document complete.</summary>
    /// <exception cref="InvalidOperationException">No root was written, an element remains open, or the writer cannot accept a write.</exception>
    /// <remarks>Does not automatically close elements or flush the underlying stream. Only flushing and disposal are permitted after completion.</remarks>
    public void WriteEndDocument()
    {
        Enter();
        try
        {
            if (!root || frames.Count != 0)
            {
                Invalid();
            }

            Mutable();
            output.Complete();
            completed = true;
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Seals any pending start tag, drains buffered output, and flushes the underlying stream.</summary>
    /// <remarks>May be called after document completion. Sealing prevents further attributes or namespace declarations on that tag.</remarks>
    public void Flush()
    {
        Enter(allowCompleted: true);
        try
        {
            Mutable();
            if (frames.Count != 0)
            {
                Seal(false);
            }

            output.Complete();
            token.ThrowIfCancellationRequested();
            output.Stream.Flush();
        }
        catch (Exception error) { Fail(error); throw; }
        finally { Exit(); }
    }
    /// <summary>Releases buffers and writer state without closing the stream, flushing output, or completing open elements.</summary>
    public void Dispose()
    {
        Enter(disposing: true);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            output.Dispose();
            frames.Clear();
            attributes.Clear();
            declarations.Clear();
            undo.Clear();
            scope.Clear();
            local.Clear();
            reserved.Clear();
            expanded.Clear();
            attributePrefixes.Clear();
        }
        finally { Exit(); }
    }
    private void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        if (options.EmitByteOrderMark)
        {
            output.Preamble();
        }

        if (!options.OmitXmlDeclaration)
        {
            output.Write("<?xml version=\"1.0\" encoding=\"");
            output.Write(output.Label);
            output.Write("\"?>");
        }
    }
    private void Structural()
    {
        brackets = 0;
        if (frames.Count == 0)
        {
            if (options.Indent && (documentNode || !options.OmitXmlDeclaration))
            {
                output.Write("\n");
            }

            documentNode = true;
            return;
        }
        Seal(false);
        var parent = frames[^1];
        if (parent.Mode == ContentMode.Undecided)
        {
            parent.Mode = ContentMode.Elements;
            frames[^1] = parent;
        }
        if (options.Indent && parent.Mode == ContentMode.Elements)
        {
            Indent(frames.Count);
        }
    }
    private void CanMix()
    {
        if (options.Indent && frames[^1].Mode == ContentMode.Elements)
        {
            Invalid();
        }
    }
    private void Mix()
    {
        var f = frames[^1];
        f.Mode = ContentMode.Mixed;
        frames[^1] = f;
    }
    private void Seal(bool empty)
    {
        if (frames.Count == 0 || frames[^1].Sealed)
        {
            return;
        }

        var f = frames[^1];
        // Resolve all names before emitting this tag. Scope mutation failures fault
        // the operation, because the pending state is no longer recoverable.
        for (var i = 0; i < declarations.Count; i++)
        {
            Tick();
            var d = declarations[i];
            if (options.NamespaceHandling == XmlNamespaceHandling.Repair && d.Prefix.Length == 0 && f.Uri.Length == 0)
            {
                d = new(d.Prefix, "");
                declarations[i] = d;
            }
            Bind(d.Prefix, d.Uri);
        }
        f.Prefix = Resolve(f.Prefix, f.Uri, false);
        attributePrefixes.Clear();
        foreach (var a in attributes)
        {
            Tick();
            attributePrefixes.Add(Resolve(a.Prefix, a.Uri, true));
        }
        output.Write("<");
        QName(f.Prefix, f.Name);
        foreach (var d in declarations)
        {
            Tick();
            output.Write(" xmlns");
            if (d.Prefix.Length != 0)
            {
                output.Write(":");
                output.Write(d.Prefix);
            }
            output.Write("=\"");
            Escape(d.Uri, true);
            output.Write("\"");
        }
        for (var i = 0; i < attributes.Count; i++)
        {
            Tick();
            var a = attributes[i];
            output.Write(" ");
            QName(attributePrefixes[i], a.Name);
            output.Write("=\"");
            Escape(a.Value, true);
            output.Write("\"");
        }
        output.Write(empty ? "/>" : ">");
        f.Sealed = true;
        frames[^1] = f;
        // Release caller strings as soon as the pending start tag is sealed.
        attributes.Clear();
        declarations.Clear();
        local.Clear();
        reserved.Clear();
        expanded.Clear();
        attributePrefixes.Clear();
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

        if ((!attribute || prefix.Length != 0) && Binding(prefix) == uri)
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
        if (attribute)
        {
            string? best = null;
            foreach (var pair in scope)
            {
                Tick();
                if (pair.Key.Length != 0 && pair.Value == uri && (best is null || string.CompareOrdinal(pair.Key, best) < 0))
                {
                    best = pair.Key;
                }
            }
            if (best is not null)
            {
                return best;
            }
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
        undo.Add(new(prefix, scope.TryGetValue(prefix, out var old) ? old : null));
        scope[prefix] = uri;
    }
    private void Restore(int mark)
    {
        for (var i = undo.Count - 1; i >= mark; i--)
        {
            Tick();
            var entry = undo[i];
            if (entry.Old is null)
            {
                scope.Remove(entry.Prefix);
            }
            else
            {
                scope[entry.Prefix] = entry.Old;
            }
        }
        undo.RemoveRange(mark, undo.Count - mark);
    }
    private void QName(string prefix, string name)
    {
        if (prefix.Length != 0)
        {
            output.Write(prefix);
            output.Write(":");
        }
        output.Write(name);
    }
    private void Indent(int depth)
    {
        output.Write("\n");
        const string spaces = "                                                                ";
        var count = checked((long)depth * 2);
        while (count != 0)
        {
            Tick();
            var n = (int)Math.Min(count, spaces.Length);
            output.Write(spaces.AsSpan(0, n));
            count -= n;
        }
    }
    private void ValidateName(string name, bool colon = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0)
        {
            throw new ArgumentException("Empty XML name.", nameof(name));
        }

        ReadOnlySpan<char> rest = name;
        var first = true;
        while (!rest.IsEmpty)
        {
            Tick();
            if (Rune.DecodeFromUtf16(rest, out var rune, out var width) != OperationStatus.Done)
            {
                throw new ArgumentException("Invalid XML name.", nameof(name));
            }

            var c = rune.Value;
            var start = c is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                >= 0xc0 and <= 0xd6 or >= 0xd8 and <= 0xf6 or >= 0xf8 and <= 0x2ff or >= 0x370 and <= 0x37d or
                >= 0x37f and <= 0x1fff or >= 0x200c and <= 0x200d or >= 0x2070 and <= 0x218f or >= 0x2c00 and <= 0x2fef or
                >= 0x3001 and <= 0xd7ff or >= 0xf900 and <= 0xfdcf or >= 0xfdf0 and <= 0xfffd or >= 0x10000 and <= 0xeffff;
            if (!(start || colon && c == ':' || !first && c is '-' or '.' or >= '0' and <= '9' or 0xb7 or >= 0x300 and <= 0x36f or >= 0x203f and <= 0x2040))
            {
                throw new ArgumentException("Invalid XML name.", nameof(name));
            }

            first = false;
            rest = rest[width..];
        }
    }
    private void ValidateBinding(string prefix, string uri)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(uri);
        if (prefix.Length != 0)
        {
            ValidateName(prefix);
        }

        ValidateText(uri);
        if (prefix == "xmlns" || uri == TreeValidation.XmlnsUri || (prefix == "xml") != (uri == TreeValidation.XmlUri) || prefix.Length != 0 && uri.Length == 0)
        {
            throw new ArgumentException("Reserved or empty namespace binding.");
        }
    }
    private void ValidateExpanded(string name, string uri, string prefix, bool attribute = false)
    {
        ValidateName(name);
        ValidateBinding(prefix, uri);
        if (attribute && name == "xmlns" && uri.Length == 0 && prefix.Length == 0)
        {
            throw new ArgumentException("Use a namespace declaration for xmlns.", nameof(name));
        }
    }
    private void ValidateText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateText(text.AsSpan());
    }
    private void ValidateText(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            Tick();
            if (Rune.DecodeFromUtf16(text, out var rune, out var width) != OperationStatus.Done ||
                !(rune.Value is 9 or 10 or 13 or >= 0x20 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000 and <= 0x10ffff))
            {
                throw new ArgumentException("Illegal XML scalar.", nameof(text));
            }

            text = text[width..];
        }
    }
    private void Delimiter(ReadOnlySpan<char> text, ReadOnlySpan<char> delimiter)
    {
        while (!text.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            var n = Math.Min(text.Length, 2048);
            if (text[..Math.Min(text.Length, n + delimiter.Length - 1)].Contains(delimiter, StringComparison.Ordinal))
            {
                throw new ArgumentException("Illegal XML delimiter.", nameof(text));
            }

            text = text[n..];
        }
    }
    private void Escape(ReadOnlySpan<char> text, bool attribute)
    {
        // Scalar validity was checked before mutation. Simple scalar loop is the
        // correctness baseline; no unmeasured SearchValues optimization is used.
        var run = 0;
        for (var i = 0; i < text.Length; i++)
        {
            Tick();
            var c = text[i];
            ReadOnlySpan<char> replacement = c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '"' when attribute => "&quot;",
                '\t' when attribute => "&#x9;",
                '\n' when attribute => "&#xA;",
                '\r' => "&#xD;",
                '>' when !attribute && brackets >= 2 => "&gt;",
                _ => default
            };
            if (!replacement.IsEmpty)
            {
                output.Write(text[run..i]);
                output.Write(replacement);
                run = i + 1;
            }
            if (!attribute)
            {
                brackets = c == ']' ? Math.Min(2, brackets + 1) : 0;
            }

            if (i - run >= 2048)
            {
                output.Write(text[run..(i + 1)]);
                run = i + 1;
            }
        }
        output.Write(text[run..]);
    }
}

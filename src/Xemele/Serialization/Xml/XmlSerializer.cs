using System.Buffers;
using System.Globalization;
using System.Text;
namespace Xemele;
public enum XmlNamespaceHandling { Strict, Repair }
public sealed record XmlSaveOptions
{
    public bool Indent { get; init; }
    public bool OmitXmlDeclaration { get; init; }
    public Encoding Encoding { get; init; } = new UTF8Encoding(false, true);
    public bool EmitByteOrderMark { get; init; }
    public XmlNamespaceHandling NamespaceHandling { get; init; } = XmlNamespaceHandling.Strict;
}
public static class XmlSerializer
{
    public static string Serialize(XmlDocument document, XmlSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = Prepare(document, options, cancellationToken);
        using var sink = new Output(null, snapshot.CodePage, cancellationToken);
        new SerializationOperation(snapshot.Options, sink, cancellationToken).Run(document, false);
        return sink.Result();
    }
    public static void Serialize(XmlDocument document, Stream output, XmlSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(output));
        var snapshot = Prepare(document, options, cancellationToken);
        using var sink = new Output(output, snapshot.CodePage, cancellationToken);
        if (snapshot.Options.EmitByteOrderMark) sink.Preamble();
        new SerializationOperation(snapshot.Options, sink, cancellationToken).Run(document, true);
        sink.Complete();
    }
    internal static (string Text, long Steps, long Bindings, int Depth) Measure(XmlDocument document, XmlSaveOptions options)
    {
        var snapshot = Prepare(document, options, default);
        using var sink = new Output(null, snapshot.CodePage, default);
        var operation = new SerializationOperation(snapshot.Options, sink, default);
        operation.Run(document, false);
        return (sink.Result(), operation.Work, operation.BindingWork, operation.MaxDepth);
    }
    private static (XmlSaveOptions Options, int CodePage) Prepare(XmlDocument document, XmlSaveOptions? options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(document);
        token.ThrowIfCancellationRequested();
        options ??= new();
        if (options.Encoding is null || options.NamespaceHandling is not (XmlNamespaceHandling.Strict or XmlNamespaceHandling.Repair))
            throw new ArgumentException("Invalid save options.", nameof(options));
        // Only the code page is retained. A fresh strict encoder ignores mutable
        // caller fallback/preamble state and never invokes a caller encoding.
        int codePage = options.Encoding.CodePage;
        if (codePage is not (65001 or 1200 or 1201)) throw new ArgumentException("Unsupported encoding.", nameof(options));
        int roots = 0, work = 0;
        foreach (var child in document.DocumentNode.Children)
        {
            if ((++work & 2047) == 0) token.ThrowIfCancellationRequested();
            if (child.Kind == XmlNodeKind.Element) roots++;
            else if (child.Kind is not (XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction)) throw new InvalidOperationException("Illegal document child.");
        }
        if (roots != 1) throw new InvalidOperationException("Exactly one root element is required.");
        return (options with { Encoding = StrictEncoding(codePage) }, codePage);
    }
    private static Encoding StrictEncoding(int page) => page switch
    {
        65001 => new UTF8Encoding(false, true),
        1200 => new UnicodeEncoding(false, false, true),
        _ => new UnicodeEncoding(true, false, true)
    };
    // Operation-local pooled bytes; no document-sized byte or character buffer.
    private sealed class Output : IDisposable
    {
        private readonly Stream? stream;
        private readonly StringBuilder? builder;
        private readonly Encoder? encoder;
        private readonly byte[]? bytes;
        private readonly CancellationToken token;
        private readonly int codePage;
        private int buffered;
        internal Output(Stream? stream, int codePage, CancellationToken token)
        {
            this.stream = stream; this.codePage = codePage; this.token = token;
            if (stream is null) builder = new();
            else { encoder = StrictEncoding(codePage).GetEncoder(); bytes = ArrayPool<byte>.Shared.Rent(8192); }
        }
        internal string Label => codePage switch { 65001 => "utf-8", 1200 => "utf-16le", _ => "utf-16be" };
        internal void Preamble()
        {
            ReadOnlySpan<byte> bom = codePage switch { 65001 => [0xef, 0xbb, 0xbf], 1200 => [0xff, 0xfe], _ => [0xfe, 0xff] };
            token.ThrowIfCancellationRequested(); stream!.Write(bom);
        }
        internal void Write(ReadOnlySpan<char> text)
        {
            while (!text.IsEmpty)
            {
                token.ThrowIfCancellationRequested();
                int length = Math.Min(text.Length, 2048);
                if (builder is not null) builder.Append(text[..length]);
                else
                {
                    // Include a pending surrogate from the preceding raw lexical
                    // chunk in the worst-case UTF-8 capacity (6145 bytes).
                    if (8192 - buffered < 6148) Drain();
                    encoder!.Convert(text[..length], bytes.AsSpan(buffered, 8192 - buffered), false, out int used, out int count, out _);
                    buffered += count;
                    // Convert may consume a partial chunk; preserve its progress
                    // rather than rejecting valid text at a buffer boundary.
                    length = used;
                    if (used == 0) Drain();
                }
                text = text[length..];
            }
        }
        internal void Complete()
        {
            token.ThrowIfCancellationRequested();
            if (8192 - buffered < 4) Drain();
            encoder!.Convert(ReadOnlySpan<char>.Empty, bytes.AsSpan(buffered, 8192 - buffered), true, out _, out int count, out _);
            buffered += count; Drain();
        }
        private void Drain()
        {
            if (buffered == 0) return;
            token.ThrowIfCancellationRequested(); stream!.Write(bytes.AsSpan(0, buffered)); buffered = 0;
        }
        internal string Result() => builder!.ToString();
        public void Dispose() { if (bytes is not null) ArrayPool<byte>.Shared.Return(bytes); }
    }
    private sealed class SerializationOperation(XmlSaveOptions options, Output output, CancellationToken token)
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
        internal long Work { get; private set; }
        internal long BindingWork { get; private set; }
        internal int MaxDepth { get; private set; }
        private readonly record struct Undo(string Prefix, string? Old);
        private readonly record struct Declaration(string Prefix, string Uri);
        private readonly record struct Frame(XmlNode Node, string Prefix, int Mark, bool Mixed, int Depth);
        private void Tick() { Work++; if ((++steps & 2047) == 0) token.ThrowIfCancellationRequested(); }
        private static void Invalid() => throw new InvalidOperationException("Invalid XML serializable tree state.");
        internal void Run(XmlDocument document, bool transport)
        {
            if (!options.OmitXmlDeclaration)
            {
                output.Write("<?xml version=\"1.0\"");
                if (transport) { output.Write(" encoding=\""); output.Write(output.Label); output.Write("\""); }
                output.Write("?>");
            }
            bool first = true;
            foreach (var child in document.DocumentNode.Children)
            {
                if (options.Indent && (!first || !options.OmitXmlDeclaration)) output.Write("\n");
                first = false;
                Visit(child, 0);
                while (frames.Count != 0)
                {
                    TraverseChildren(frames[^1].Node.FirstChild);
                }
            }
        }
        private void TraverseChildren(XmlNode next)
        {
            while (true)
            {
                Tick();
                if (!next.IsDefault)
                {
                    var parent = frames[^1];
                    if (options.Indent && !parent.Mixed) Indent(checked(parent.Depth + 1));
                    XmlNode sibling = next.NextSibling;
                    int count = frames.Count;
                    Visit(next, checked(parent.Depth + 1));
                    if (frames.Count != count) { next = next.FirstChild; continue; }
                    next = sibling; continue;
                }
                var closing = frames[^1];
                if (options.Indent && !closing.Mixed) Indent(closing.Depth);
                output.Write("</"); QName(closing.Prefix, closing.Node.LocalName); output.Write(">"); brackets = 0;
                Restore(closing.Mark); frames.RemoveAt(frames.Count - 1);
                if (frames.Count == 0) break;
                next = closing.Node.NextSibling;
            }
        }
        private void Visit(XmlNode node, int depth)
        {
            Tick();
            if (node.Kind == XmlNodeKind.Element) { Element(node, depth); return; }
            if (node.Kind == XmlNodeKind.Text) { Escape(node.Value, false); return; }
            brackets = 0;
            ValidateText(node.Value);
            switch (node.Kind)
            {
                case XmlNodeKind.CData:
                    Delimiter(node.Value, "]]>"); output.Write("<![CDATA["); output.Write(node.Value); output.Write("]]>"); break;
                case XmlNodeKind.Comment:
                    Delimiter(node.Value, "--"); if (node.Value.EndsWith('-')) Invalid();
                    output.Write("<!--"); output.Write(node.Value); output.Write("-->"); break;
                case XmlNodeKind.ProcessingInstruction:
                    ValidateName(node.LocalName); if (node.LocalName.Equals("xml", StringComparison.OrdinalIgnoreCase)) Invalid();
                    Delimiter(node.Value, "?>"); output.Write("<?"); output.Write(node.LocalName);
                    if (node.Value.Length != 0) { output.Write(" "); output.Write(node.Value); } output.Write("?>"); break;
                default: Invalid(); break;
            }
        }
        private void Element(XmlNode node, int depth)
        {
            brackets = 0; ValidateExpanded(node.LocalName, node.NamespaceUri, node.Prefix);
            int mark = undo.Count;
            declarations.Clear(); attributePrefixes.Clear(); local.Clear(); reserved.Clear(); expanded.Clear();
            reserved.Add(node.Prefix);
            foreach (var a in node.Attributes)
            {
                Tick(); ValidateExpanded(a.LocalName, a.NamespaceUri, a.Prefix, attribute: true);
                if (!expanded.Add((a.NamespaceUri, a.LocalName))) Invalid();
                reserved.Add(a.Prefix);
            }
            foreach (var n in node.NamespaceDeclarations)
            {
                Tick(); ValidateBinding(n.Prefix, n.Value);
                if (!local.Add(n.Prefix)) Invalid(); reserved.Add(n.Prefix);
                string uri = n.Value;
                if (options.NamespaceHandling == XmlNamespaceHandling.Repair && n.Prefix.Length == 0 && node.NamespaceUri.Length == 0) uri = "";
                declarations.Add(new(n.Prefix, uri)); Bind(n.Prefix, uri);
            }
            string prefix = Resolve(node.Prefix, node.NamespaceUri, false);
            foreach (var a in node.Attributes) { Tick(); attributePrefixes.Add(Resolve(a.Prefix, a.NamespaceUri, true)); }
            output.Write("<"); QName(prefix, node.LocalName);
            foreach (var n in declarations)
            {
                Tick(); output.Write(" xmlns"); if (n.Prefix.Length != 0) { output.Write(":"); output.Write(n.Prefix); }
                output.Write("=\""); Escape(n.Uri, true); output.Write("\"");
            }
            int index = 0;
            foreach (var a in node.Attributes)
            {
                Tick(); output.Write(" "); QName(attributePrefixes[index++], a.LocalName);
                output.Write("=\""); Escape(a.Value, true); output.Write("\"");
            }
            if (node.FirstChild.IsDefault) { output.Write("/>"); Restore(mark); return; }
            bool mixed = false;
            foreach (var child in node.Children) { Tick(); if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData) mixed = true; }
            output.Write(">"); frames.Add(new(node, prefix, mark, mixed, depth));
            MaxDepth = Math.Max(MaxDepth, frames.Count);
        }
        private string Binding(string prefix) => scope.TryGetValue(prefix, out var uri) ? uri : "";
        private string Resolve(string prefix, string uri, bool attribute)
        {
            if (uri == TreeValidation.XmlUri) return "xml";
            if (attribute && uri.Length == 0) return "";
            bool matches = (!attribute || prefix.Length != 0) && Binding(prefix) == uri;
            if (matches) return prefix;
            if (options.NamespaceHandling == XmlNamespaceHandling.Strict) Invalid();
            if (!attribute && uri.Length == 0) { Generate("", ""); return ""; }
            if (!attribute && !local.Contains(prefix)) { Generate(prefix, uri); return prefix; }
            if (attribute && byUri.TryGetValue(uri, out var prefixes) && prefixes.Count != 0) return prefixes.Min!;
            string fresh;
            do { Tick(); generated = checked(generated + 1); fresh = "ns" + generated.ToString(CultureInfo.InvariantCulture); }
            while (scope.ContainsKey(fresh) || reserved.Contains(fresh));
            Generate(fresh, uri); return fresh;
        }
        private void Generate(string prefix, string uri) { declarations.Add(new(prefix, uri)); local.Add(prefix); Bind(prefix, uri); }
        private void Bind(string prefix, string uri)
        {
            string? old = scope.TryGetValue(prefix, out var previous) ? previous : null;
            undo.Add(new(prefix, old)); SetBinding(prefix, uri);
        }
        private void SetBinding(string prefix, string? uri)
        {
            Tick(); BindingWork++;
            if (prefix.Length != 0 && scope.TryGetValue(prefix, out var old) && byUri.TryGetValue(old, out var previous))
            {
                previous.Remove(prefix); if (previous.Count == 0) byUri.Remove(old);
            }
            if (uri is null) { scope.Remove(prefix); return; }
            scope[prefix] = uri;
            if (prefix.Length != 0 && prefix != "xml")
            {
                if (!byUri.TryGetValue(uri, out var prefixes)) byUri.Add(uri, prefixes = new(StringComparer.Ordinal));
                prefixes.Add(prefix);
            }
        }
        private void Restore(int mark)
        {
            for (int i = undo.Count - 1; i >= mark; i--) { Tick(); SetBinding(undo[i].Prefix, undo[i].Old); }
            undo.RemoveRange(mark, undo.Count - mark);
        }
        private void QName(string prefix, string localName)
        {
            if (prefix.Length != 0) { output.Write(prefix); output.Write(":"); } output.Write(localName);
        }
        private void Indent(int depth)
        {
            output.Write("\n");
            // Fixed literal blocks avoid constructing depth-sized indentation.
            const string spaces = "                                                                ";
            long count = checked((long)depth * 2);
            while (count != 0) { Tick(); int n = (int)Math.Min(count, spaces.Length); output.Write(spaces.AsSpan(0, n)); count -= n; }
        }
        private static bool Scalar(int c) => c is 9 or 10 or 13 or >= 0x20 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000 and <= 0x10ffff;
        private static bool Start(int c) => c is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= 0xc0 and <= 0xd6 or >= 0xd8 and <= 0xf6 or >= 0xf8 and <= 0x2ff or >= 0x370 and <= 0x37d or >= 0x37f and <= 0x1fff or >= 0x200c and <= 0x200d or >= 0x2070 and <= 0x218f or >= 0x2c00 and <= 0x2fef or >= 0x3001 and <= 0xd7ff or >= 0xf900 and <= 0xfdcf or >= 0xfdf0 and <= 0xfffd or >= 0x10000 and <= 0xeffff;
        private int ReadScalar(ReadOnlySpan<char> text, out int width)
        {
            Tick(); if (Rune.DecodeFromUtf16(text, out var rune, out width) != OperationStatus.Done || !Scalar(rune.Value)) { Invalid(); }
            return rune.Value;
        }
        private void ValidateText(string value)
        {
            ReadOnlySpan<char> rest = value; while (!rest.IsEmpty) { ReadScalar(rest, out int width); rest = rest[width..]; }
        }
        private void ValidateName(string name, bool colon = false)
        {
            if (name.Length == 0) Invalid();
            ReadOnlySpan<char> rest = name; bool first = true;
            while (!rest.IsEmpty)
            {
                int c = ReadScalar(rest, out int width);
                if (!(Start(c) || colon && c == ':' || !first && c is '-' or '.' or >= '0' and <= '9' or 0xb7 or >= 0x300 and <= 0x36f or >= 0x203f and <= 0x2040)) Invalid();
                first = false; rest = rest[width..];
            }
        }
        private void ValidateBinding(string prefix, string uri)
        {
            if (prefix.Length != 0) ValidateName(prefix); ValidateText(uri);
            if (prefix == "xmlns" || uri == TreeValidation.XmlnsUri || (prefix == "xml") != (uri == TreeValidation.XmlUri) || prefix.Length != 0 && uri.Length == 0) Invalid();
        }
        private void ValidateExpanded(string localName, string uri, string prefix, bool attribute = false)
        {
            ValidateName(localName); ValidateBinding(prefix, uri); if (attribute && localName == "xmlns" && uri.Length == 0 && prefix.Length == 0) Invalid();
        }
        private void Delimiter(string value, ReadOnlySpan<char> delimiter)
        {
            // Bounded chunk search with overlap, including boundary matches.
            ReadOnlySpan<char> rest = value;
            while (!rest.IsEmpty)
            {
                token.ThrowIfCancellationRequested(); int n = Math.Min(rest.Length, 2048);
                if (rest[..Math.Min(rest.Length, n + delimiter.Length - 1)].Contains(delimiter, StringComparison.Ordinal)) Invalid();
                rest = rest[n..];
            }
        }
        private void Escape(string value, bool attribute)
        {
            ReadOnlySpan<char> text = value; int run = 0, sinceWrite = 0;
            for (int i = 0; i < text.Length;)
            {
                int c = ReadScalar(text[i..], out int width);
                ReadOnlySpan<char> replacement = c switch
                {
                    '&' => "&amp;", '<' => "&lt;", '"' when attribute => "&quot;",
                    9 when attribute => "&#x9;", 10 when attribute => "&#xA;", 13 => "&#xD;",
                    '>' when !attribute && brackets >= 2 => "&gt;", _ => default
                };
                if (!replacement.IsEmpty)
                {
                    output.Write(text[run..i]); output.Write(replacement); run = i + width; sinceWrite = 0;
                }
                if (!attribute) brackets = c == ']' ? Math.Min(2, brackets + 1) : 0;
                i += width; sinceWrite += width;
                if (sinceWrite >= 2048) { output.Write(text[run..i]); run = i; sinceWrite = 0; }
            }
            output.Write(text[run..]);
        }
    }
}

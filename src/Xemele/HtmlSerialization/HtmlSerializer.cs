using System.Text;

namespace Xemele.Html;

public enum HtmlSerializationMode { Legacy, Html5 }
public sealed record HtmlSaveOptions
{
    public HtmlSerializationMode Mode { get; init; } = HtmlSerializationMode.Html5;
}

public static class HtmlSerializer
{
    public static string Serialize(HtmlDocument document, HtmlSaveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Serialize(document.DocumentNode, options);
    }

    public static string Serialize(HtmlNode node, HtmlSaveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        var mode = Validate(options);
        // The builder and final string belong to the public owned-result boundary.
        var output = new StringWriter(new StringBuilder(), System.Globalization.CultureInfo.InvariantCulture);
        Write(node, output, mode, default);
        return output.ToString();
    }

    public static void Save(HtmlDocument document, Stream stream, HtmlSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        var mode = Validate(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));
        // A fixed buffer bounds UTF-8 transport independently of output length.
        // Explicit flush only on success: disposal during an I/O/cancellation fault
        // must neither write pending bytes nor mask the original exception.
        var output = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
        Write(document.DocumentNode, output, mode, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        output.Flush();
    }

    private static HtmlSerializationMode Validate(HtmlSaveOptions? options)
    {
        var mode = options?.Mode ?? HtmlSerializationMode.Html5;
        if (mode is not (HtmlSerializationMode.Legacy or HtmlSerializationMode.Html5)) throw new ArgumentOutOfRangeException(nameof(options));
        return mode;
    }

    internal static void Write(HtmlNode root, TextWriter output, HtmlSerializationMode mode, CancellationToken cancellation)
    {
        bool legacy = mode == HtmlSerializationMode.Legacy;
        if (legacy && root.Kind == HtmlNodeKind.Document)
            for (int i = 0; i < root.Children.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (root.Children[i].Doctype is { } d) { Doctype(output, d, cancellation); break; }
            }
        HtmlNode current = root;
        bool raw = false;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            HtmlNode? first = null;
            switch (current.Kind)
            {
                case HtmlNodeKind.Document:
                case HtmlNodeKind.Fragment:
                    first = current.FirstChild;
                    break;
                case HtmlNodeKind.Element:
                    output.Write('<'); Name(output, current.LocalName, ElementPrefix(current, legacy), legacy, cancellation);
                    for (int i = 0; i < current.Attributes.Count; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var attribute = current.Attributes[i];
                        output.Write(' '); Name(output, attribute.LocalName, AttributePrefix(attribute, legacy), legacy, cancellation);
                        if (legacy && Boolean(attribute.LocalName)) continue;
                        output.Write("=\"");
                        if (legacy && UriAttribute(current, attribute)) Uri(output, attribute.Value, cancellation);
                        else Escape(output, attribute.Value, true, legacy, cancellation);
                        output.Write('"');
                    }
                    output.Write('>');
                    if (!Void(current, legacy))
                    {
                        first = legacy ? current.FirstChild ?? current.TemplateContent?.FirstChild : (current.TemplateContent ?? current).FirstChild;
                        if (first is null) End(output, current, legacy, cancellation);
                        else if (legacy && NativeHtml(current) && NativeRaw(current.LocalName)) raw = true;
                    }
                    break;
                case HtmlNodeKind.Text:
                    if (legacy ? raw : Html5Raw(current, cancellation)) Literal(output, current.Value, legacy, cancellation);
                    else Escape(output, current.Value, false, legacy, cancellation);
                    break;
                case HtmlNodeKind.Comment:
                    output.Write("<!--"); Literal(output, current.Value, legacy, cancellation); output.Write("-->");
                    break;
                case HtmlNodeKind.ProcessingInstruction:
                    output.Write("<?"); Literal(output, current.LocalName, legacy, cancellation); output.Write(' ');
                    Literal(output, current.Value, legacy, cancellation); output.Write(legacy ? ">" : "?>");
                    break;
                case HtmlNodeKind.DocumentType:
                    if (!legacy) { output.Write("<!DOCTYPE "); Literal(output, current.LocalName, false, cancellation); output.Write('>'); }
                    break;
            }
            if (first is not null) { current = first; continue; }
            while (true)
            {
                if (current == root)
                {
                    if (legacy && root.Kind == HtmlNodeKind.Document) output.Write('\n');
                    return;
                }
                if (current.NextSibling is { } sibling) { current = sibling; break; }
                raw = false;
                var parent = current.Parent!;
                // Legacy templates are plain native elements. Preserve their
                // ordinary children, then project the separate content graph.
                if (legacy && parent.TemplateContent?.FirstChild is { } content)
                { current = content; break; }
                current = parent == root ? parent : parent.TemplateHost ?? parent;
                if (current.Kind == HtmlNodeKind.Element) End(output, current, legacy, cancellation);
            }
        }
    }

    private static bool IsHtml(HtmlNode n) => n.NamespaceUri == HtmlDocument.HtmlNamespace;
    private static bool NativeHtml(HtmlNode n) => IsHtml(n) || n.NamespaceUri.Length == 0;
    private static bool Void(HtmlNode n, bool legacy) => legacy ? NativeHtml(n) && NativeVoid(n.LocalName) : IsHtml(n) && (n.LocalName is "area" or "base" or "basefont" or "bgsound" or "br" or "col" or "embed" or "frame" or "hr" or "img" or "input" or "keygen" or "link" or "meta" or "param" or "source" or "track" or "wbr");
    private static bool RawName(string name) => name is "style" or "script" or "xmp" or "iframe" or "noembed" or "noframes" or "plaintext";
    private static bool Html5Raw(HtmlNode text, CancellationToken cancellation)
    {
        var parent = text.Parent;
        if (parent is null || !IsHtml(parent)) return false;
        if (RawName(parent.LocalName)) return true;
        if (parent.LocalName != "noscript" || !text.OwnerDocument.ScriptingEnabled) return false;
        // Template contents have an inert document in the browser algorithm. The
        // frozen managed graph models that edge using TemplateHost instead.
        for (var n = parent; n is not null; n = n.StructuralParent)
            { cancellation.ThrowIfCancellationRequested(); if (n.TemplateHost is not null) return false; }
        return true;
    }
    private static string ElementPrefix(HtmlNode n, bool legacy) => legacy && NativeHtml(n) ? "" : !legacy && n.NamespaceUri is HtmlDocument.HtmlNamespace or "http://www.w3.org/2000/svg" or "http://www.w3.org/1998/Math/MathML" ? "" : n.Prefix;
    private static string AttributePrefix(HtmlAttribute a, bool legacy) => legacy ? a.NamespaceUri.Length == 0 ? "" : a.Prefix : a.NamespaceUri switch
    {
        "" => "", "http://www.w3.org/XML/1998/namespace" => "xml",
        "http://www.w3.org/2000/xmlns/" => a.LocalName == "xmlns" ? "" : "xmlns",
        "http://www.w3.org/1999/xlink" => "xlink", _ => a.Prefix
    };
    private static void Name(TextWriter output, string local, string prefix, bool legacy, CancellationToken cancellation)
    {
        if (prefix.Length != 0) { Literal(output, prefix, legacy, cancellation); output.Write(':'); }
        Literal(output, local, legacy, cancellation);
    }
    private static void End(TextWriter output, HtmlNode n, bool legacy, CancellationToken cancellation)
    { output.Write("</"); Name(output, n.LocalName, ElementPrefix(n, legacy), legacy, cancellation); output.Write('>'); }
    internal static bool Boolean(ReadOnlySpan<char> name) => name.Length switch
    {
        5 => AsciiEqual(name, "defer") || AsciiEqual(name, "ismap"),
        6 => AsciiEqual(name, "nohref") || AsciiEqual(name, "nowrap"),
        7 => AsciiEqual(name, "checked") || AsciiEqual(name, "compact") || AsciiEqual(name, "declare") || AsciiEqual(name, "noshade"),
        8 => AsciiEqual(name, "disabled") || AsciiEqual(name, "multiple") || AsciiEqual(name, "noresize") || AsciiEqual(name, "readonly") || AsciiEqual(name, "selected"),
        _ => false
    };
    private static bool AsciiEqual(ReadOnlySpan<char> value, ReadOnlySpan<char> lower)
    {
        if (value.Length != lower.Length) return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i]; if (c is >= 'A' and <= 'Z') c = (char)(c + 32);
            if (c != lower[i]) return false;
        }
        return true;
    }
    private static bool UriAttribute(HtmlNode n, HtmlAttribute a) => NativeHtml(n) && a.NamespaceUri.Length == 0 && (AsciiEqual(a.LocalName, "href") || AsciiEqual(a.LocalName, "action") || AsciiEqual(a.LocalName, "src") || AsciiEqual(a.LocalName, "name") && AsciiEqual(n.LocalName, "a"));
    private static bool NativeVoid(ReadOnlySpan<char> name) => name.Length switch
    {
        2 => AsciiEqual(name, "br") || AsciiEqual(name, "hr"),
        3 => AsciiEqual(name, "col") || AsciiEqual(name, "img") || AsciiEqual(name, "wbr"),
        4 => AsciiEqual(name, "area") || AsciiEqual(name, "base") || AsciiEqual(name, "link") || AsciiEqual(name, "meta"),
        5 => AsciiEqual(name, "embed") || AsciiEqual(name, "frame") || AsciiEqual(name, "input") || AsciiEqual(name, "param") || AsciiEqual(name, "track"),
        6 => AsciiEqual(name, "keygen") || AsciiEqual(name, "source"),
        7 => AsciiEqual(name, "bgsound") || AsciiEqual(name, "isindex"),
        8 => AsciiEqual(name, "basefont"), _ => false
    };
    private static bool NativeRaw(ReadOnlySpan<char> name) => name.Length switch
    {
        3 => AsciiEqual(name, "xmp"), 5 => AsciiEqual(name, "style"),
        6 => AsciiEqual(name, "script") || AsciiEqual(name, "iframe"),
        7 => AsciiEqual(name, "noembed"), 8 => AsciiEqual(name, "noframes"),
        9 => AsciiEqual(name, "plaintext"), _ => false
    };


    internal static int Stop(ReadOnlySpan<char> value, bool attribute, bool legacy)
    {
        for (int i = 0; i < value.Length; i++)
            if (value[i] == '&' || (!attribute || legacy) && value[i] is '<' or '>' || attribute && value[i] == '"' || (legacy ? value[i] == '\0' : value[i] == '\u00a0')) return i;
        return -1;
    }
    internal static void Escape(TextWriter output, ReadOnlySpan<char> value, bool attribute, bool legacy, CancellationToken cancellation)
    {
        while (!value.IsEmpty)
        {
            cancellation.ThrowIfCancellationRequested();
            // A bounded slice also bounds cancellation latency within long runs.
            var part = value[..Math.Min(value.Length, 1024)];
            int stop = Stop(part, attribute, legacy);
            if (stop < 0) { output.Write(part); value = value[part.Length..]; continue; }
            output.Write(part[..stop]);
            char c = part[stop];
            if (legacy && c == '\0') return;
            output.Write(c switch { '&' => "&amp;", '"' => "&quot;", '\u00a0' => "&nbsp;", '<' => attribute && !legacy ? "<" : "&lt;", _ => attribute && !legacy ? ">" : "&gt;" });
            value = value[(stop + 1)..];
        }
    }
    private static void Literal(TextWriter output, ReadOnlySpan<char> value, bool legacy, CancellationToken cancellation)
    {
        while (!value.IsEmpty)
        {
            cancellation.ThrowIfCancellationRequested();
            var part = value[..Math.Min(1024, value.Length)];
            int zero = legacy ? part.IndexOf('\0') : -1;
            if (zero >= 0) { output.Write(part[..zero]); return; }
            output.Write(part); value = value[part.Length..];
        }
    }
    private static void Doctype(TextWriter output, HtmlDoctype d, CancellationToken cancellation)
    {
        output.Write("<!DOCTYPE "); Literal(output, d.Name, true, cancellation);
        if (d.PublicIdentifier is { } pub)
        {
            output.Write(" PUBLIC "); Quoted(output, pub, cancellation);
            if (d.SystemIdentifier is { } sys) { output.Write(' '); Quoted(output, sys, cancellation); }
        }
        else if (d.SystemIdentifier is { } sys && sys != "about:legacy-compat") { output.Write(" SYSTEM "); Quoted(output, sys, cancellation); }
        output.Write(">\n");
    }
    private static void Quoted(TextWriter output, ReadOnlySpan<char> value, CancellationToken cancellation)
    {
        bool single = false, twice = false;
        int length = value.Length;
        for (int offset = 0; offset < length;)
        {
            cancellation.ThrowIfCancellationRequested();
            var part = value.Slice(offset, Math.Min(1024, length - offset));
            int zero = part.IndexOf('\0');
            if (zero >= 0) { length = offset + zero; part = part[..zero]; }
            single |= part.Contains('\''); twice |= part.Contains('"'); offset += part.Length;
        }
        value = value[..length];
        char quote = twice && !single ? '\'' : '"';
        output.Write(quote);
        while (!value.IsEmpty)
        {
            cancellation.ThrowIfCancellationRequested();
            var part = value[..Math.Min(1024, value.Length)];
            int index = quote == '"' ? part.IndexOf('"') : -1;
            if (index < 0) { output.Write(part); value = value[part.Length..]; }
            else { output.Write(part[..index]); output.Write("&quot;"); value = value[(index + 1)..]; }
        }
        output.Write(quote);
    }
    private static void Uri(TextWriter output, ReadOnlySpan<char> value, CancellationToken cancellation)
    {
        int initial = 0;
        while (initial < value.Length && value[initial] is ' ' or '\t' or '\n' or '\r' or '\f')
        { if ((initial & 1023) == 0) cancellation.ThrowIfCancellationRequested(); initial++; }
        Literal(output, value[..initial], true, cancellation); value = value[initial..];
        Span<byte> bytes = stackalloc byte[4];
        Span<char> escaped = stackalloc char[3]; escaped[0] = '%';
        const string hex = "0123456789ABCDEF";
        while (!value.IsEmpty)
        {
            cancellation.ThrowIfCancellationRequested();
            char c = value[0]; if (c == '\0') return;
            if (c is '"' or '&') { output.Write(c == '"' ? "&quot;" : "&amp;"); value = value[1..]; }
            else if (c > 0x20 && c < 0x7f) { output.Write(c); value = value[1..]; }
            else
            {
                System.Text.Rune.DecodeFromUtf16(value, out var rune, out int consumed);
                if (consumed == 0) consumed = 1;
                int count = rune.EncodeToUtf8(bytes);
                for (int i = 0; i < count; i++) { escaped[1] = hex[bytes[i] >> 4]; escaped[2] = hex[bytes[i] & 15]; output.Write(escaped); }
                value = value[consumed..];
            }
        }
    }
}

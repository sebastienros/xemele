using System.Text;

// Capability detection only, after native parsing succeeds. This does not validate
// XML or manufacture events. libxml2 consumes top-level Misc whitespace silently.
internal static class DocumentEnvelope
{
    internal static string? UnsupportedReason(byte[] bytes, string? hint, string nativeEncoding)
    {
        string text;
        try
        {
            Encoding? encoding = ChooseEncoding(bytes, hint, nativeEncoding);
            if (encoding is null)
                return "Cannot safely inspect document envelope for this native encoding.";
            text = encoding.GetString(bytes);
        }
        catch (DecoderFallbackException) { return "Cannot safely decode native document envelope."; }
        ReadOnlySpan<char> source = text;
        int offset = source.Length > 0 && source[0] == '\uFEFF' ? 1 : 0;
        int depth = 0;
        bool rootSeen = false;
        while (offset < source.Length)
        {
            if (source[offset] != '<')
            {
                int end = source[offset..].IndexOf('<');
                end = end < 0 ? source.Length : offset + end;
                if (depth == 0 && end > offset)
                {
                    foreach (char ch in source[offset..end])
                        if (ch is not (' ' or '\t' or '\r' or '\n'))
                            return "Ambiguous content outside document element.";
                    return "libxml2 omits whitespace outside document element; use BCL for these v1 text events.";
                }
                offset = end;
                continue;
            }
            if (source[offset..].StartsWith("<!--", StringComparison.Ordinal))
            {
                if (!SkipDelimited(source, ref offset, "-->"))
                    return "Ambiguous document comment boundary.";
            }
            else if (source[offset..].StartsWith("<?", StringComparison.Ordinal))
            {
                if (!SkipDelimited(source, ref offset, "?>"))
                    return "Ambiguous document PI boundary.";
            }
            else if (source[offset..].StartsWith("<![CDATA[", StringComparison.Ordinal))
            {
                if (depth == 0 || !SkipDelimited(source, ref offset, "]]>"))
                    return "Ambiguous document CDATA boundary.";
            }
            else if (source[offset..].StartsWith("<!DOCTYPE", StringComparison.Ordinal))
            {
                if (depth != 0 || rootSeen || !SkipDoctype(source, ref offset))
                    return "Ambiguous document DTD boundary.";
            }
            else
            {
                bool closing = offset + 1 < source.Length && source[offset + 1] == '/';
                int end = TagEnd(source, offset);
                if (end < 0)
                    return "Ambiguous document tag boundary.";
                int tail = end - 1;
                while (tail > offset && source[tail] is ' ' or '\t' or '\r' or '\n')
                    tail--;
                bool empty = source[tail] == '/';
                if (closing)
                {
                    if (depth == 0)
                        return "Ambiguous document element depth.";
                    depth--;
                }
                else
                {
                    if (depth == 0)
                    {
                        if (rootSeen)
                            return "Ambiguous multiple document elements.";
                        rootSeen = true;
                    }
                    if (!empty)
                        depth++;
                }
                offset = end + 1;
            }
        }
        return rootSeen && depth == 0 ? null : "Ambiguous document envelope.";
    }

    private static bool SkipDelimited(ReadOnlySpan<char> source, ref int offset, string terminator)
    {
        int end = source[offset..].IndexOf(terminator, StringComparison.Ordinal);
        if (end < 0)
            return false;
        offset += end + terminator.Length;
        return true;
    }

    private static int TagEnd(ReadOnlySpan<char> source, int offset)
    {
        char quote = '\0';
        for (int i = offset + 1; i < source.Length; i++)
        {
            char ch = source[i];
            if (quote != '\0')
            {
                if (ch == quote)
                    quote = '\0';
            }
            else if (ch is '\'' or '"')
                quote = ch;
            else if (ch == '>')
                return i;
        }
        return -1;
    }

    private static bool SkipDoctype(ReadOnlySpan<char> source, ref int offset)
    {
        int brackets = 0;
        char quote = '\0';
        for (int i = offset + 9; i < source.Length; i++)
        {
            char ch = source[i];
            if (quote != '\0')
            {
                if (ch == quote)
                    quote = '\0';
                continue;
            }
            if (ch is '\'' or '"')
            {
                quote = ch;
                continue;
            }
            if (source[i..].StartsWith("<!--", StringComparison.Ordinal))
            {
                if (!SkipDelimited(source, ref i, "-->"))
                    return false;
                i--;
                continue;
            }
            if (source[i..].StartsWith("<?", StringComparison.Ordinal))
            {
                if (!SkipDelimited(source, ref i, "?>"))
                    return false;
                i--;
                continue;
            }
            if (ch == '[')
                brackets++;
            else if (ch == ']')
            {
                if (--brackets < 0)
                    return false;
            }
            else if (ch == '>' && brackets == 0)
            {
                offset = i + 1;
                return true;
            }
        }
        return false;
    }

    private static Encoding? ChooseEncoding(ReadOnlySpan<byte> bytes, string? hint, string name)
    {
        if (hint == "utf-8")
            return new UTF8Encoding(false, true);
        // Physical signatures take precedence over generic declaration names.
        if (bytes.StartsWith(new byte[] { 0, 0, 0xFE, 0xFF }) || bytes.StartsWith(new byte[] { 0, 0, 0, 0x3C }))
            return new UTF32Encoding(true, false, true);
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 }) || bytes.StartsWith(new byte[] { 0x3C, 0, 0, 0 }))
            return new UTF32Encoding(false, false, true);
        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF }) || bytes.StartsWith(new byte[] { 0, 0x3C }))
            return new UnicodeEncoding(true, false, true);
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE }) || bytes.StartsWith(new byte[] { 0x3C, 0 }))
            return new UnicodeEncoding(false, false, true);
        return name.ToUpperInvariant() switch
        {
            "" or "UTF-8" or "UTF8" => new UTF8Encoding(false, true),
            "UTF-16LE" => new UnicodeEncoding(false, false, true),
            "UTF-16BE" => new UnicodeEncoding(true, false, true),
            "ISO-8859-1" or "ISO8859-1" or "LATIN1" => Encoding.Latin1,
            "US-ASCII" or "ASCII" => Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            _ => null
        };
    }
}

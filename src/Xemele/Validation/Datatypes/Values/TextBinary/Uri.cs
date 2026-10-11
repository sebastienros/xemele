namespace Xemele.Validation;

// RFC2396 Appendix A + RFC2732, after the XLink 1.0 §5.4 escape projection.
// Project disallowed scalars to an escaped atom; never construct an escaped URI,
// absolutize it, or pass arbitrary input to System.Uri/IPAddress/regex parsers.
internal static class TextBinaryUri
{
    private const int Escaped = 256;
    internal static bool Validate(ReadOnlySpan<char> text, ISchemaOperation op, out XmlDatatypeError? error)
    {
        error = null;
        int body = 0; bool absolute = false, scheme = true; int scanned = 0;
        for (int i = 0; i < text.Length;)
        {
            if (!Token(text, ref i, op, out int c, out error)) return false;
            if (c is '/' or '?' or '#') break;
            if (c == ':')
            {
                if (!scheme || scanned == 0) return TextBinaryDatatypeProvider.Invalid(out error);
                absolute = true; body = i; break;
            }
            scheme &= scanned == 0 ? TextBinaryDatatypeProvider.Alpha(c) : TextBinaryDatatypeProvider.Alpha(c) || c is >= '0' and <= '9' or '+' or '-' or '.';
            scanned++;
        }
        int position = body;
        if (!op.Charge(1, default)) return TextBinaryDatatypeProvider.Stop(out error);
        bool authority = text.Length - position >= 2 && text[position] == '/' && text[position + 1] == '/';
        if (authority)
        {
            position += 2; int start = position;
            while (position < text.Length)
            {
                int at = position;
                if (!Token(text, ref position, op, out int c, out error)) return false;
                if (c is '/' or '?' or '#') { position = at; break; }
            }
            if (!Authority(text[start..position], op, out error)) return false;
        }
        // Absolute opaque URIs need a first non-slash uric atom; hier_part can
        // have '/' followed by an empty path. Query-only relative references
        // follow RFC2396 §5.2/C.1 (despite the Appendix A rel_segment ambiguity).
        bool opaque = false;
        if (absolute && !authority)
        {
            if (position == text.Length || text[position] == '#') return TextBinaryDatatypeProvider.Invalid(out error);
            opaque = text[position] != '/';
        }
        int state = opaque ? 1 : 0; bool firstSegment = !absolute && !authority;
        while (position < text.Length)
        {
            if (!Token(text, ref position, op, out int c, out error)) return false;
            if (c == '#') { if (state == 2) return TextBinaryDatatypeProvider.Invalid(out error); state = 2; continue; }
            if (c == '?' && state == 0) { state = 1; continue; }
            if (state == 0)
            {
                if (c == '/') { firstSegment = false; continue; }
                if (!(Unreserved(c) || c == Escaped || c is ';' or ':' or '@' or '&' or '=' or '+' or '$' or ',') || firstSegment && c == ':')
                    return TextBinaryDatatypeProvider.Invalid(out error);
            }
            else if (!Uric(c)) return TextBinaryDatatypeProvider.Invalid(out error);
        }
        return true;
    }
    private static bool Token(ReadOnlySpan<char> text, ref int i, ISchemaOperation op, out int c, out XmlDatatypeError? error)
    {
        if (!TextBinaryDatatypeProvider.Read(text, ref i, op, out c, out error)) return false;
        if (c == '%')
        {
            for (int n = 0; n < 2; n++)
            {
                if (i == text.Length) return TextBinaryDatatypeProvider.Invalid(out error);
                if (!op.Charge(1, default)) return TextBinaryDatatypeProvider.Stop(out error);
                if (TextBinaryDatatypeProvider.HexDigit(text[i++]) < 0) return TextBinaryDatatypeProvider.Invalid(out error);
            }
            c = Escaped;
        }
        else if (c > 127 || c <= 32 || c == 127 || c is '<' or '>' or '"' or '{' or '}' or '|' or '\\' or '^' or '`') c = Escaped;
        return true;
    }
    private static bool Unreserved(int c) => TextBinaryDatatypeProvider.Alpha(c) || c is >= '0' and <= '9' or '-' or '_' or '.' or '!' or '~' or '*' or '\'' or '(' or ')';
    private static bool Uric(int c) => Unreserved(c) || c == Escaped || c is ';' or '/' or '?' or ':' or '@' or '&' or '=' or '+' or '$' or ',' or '[' or ']';
    private static bool Authority(ReadOnlySpan<char> text, ISchemaOperation op, out XmlDatatypeError? error)
    {
        error = null; int open = -1, close = -1, atSign = -1;
        for (int i = 0; i < text.Length;)
        {
            int at = i;
            if (!Token(text, ref i, op, out int c, out error)) return false;
            if (c == '[') { if (open >= 0) return TextBinaryDatatypeProvider.Invalid(out error); open = at; }
            else if (c == ']') { if (close >= 0) return TextBinaryDatatypeProvider.Invalid(out error); close = at; }
            else if (c == '@') atSign = at;
            else if (!(Unreserved(c) || c == Escaped || c is '$' or ',' or ';' or ':' or '&' or '=' or '+')) return TextBinaryDatatypeProvider.Invalid(out error);
        }
        if (open < 0 && close < 0) return true; // Empty server or nonempty reg_name.
        if (open != atSign + 1 || close <= open || atSign >= close) return TextBinaryDatatypeProvider.Invalid(out error);
        // An IPv6 host requires the server alternative, so userinfo cannot have '@'.
        for (int i = 0; i < atSign;)
        {
            if (!Token(text, ref i, op, out int c, out error)) return false;
            if (c == '@') return TextBinaryDatatypeProvider.Invalid(out error);
        }
        if (!Ipv6(text[(open + 1)..close], op, out error)) return false;
        if (close + 1 == text.Length) return true;
        if (!op.Charge(1, default)) return TextBinaryDatatypeProvider.Stop(out error);
        if (text[close + 1] != ':') return TextBinaryDatatypeProvider.Invalid(out error);
        for (int i = close + 2; i < text.Length; i++)
        { if (!op.Charge(1, default)) return TextBinaryDatatypeProvider.Stop(out error); if (text[i] is < '0' or > '9') return TextBinaryDatatypeProvider.Invalid(out error); }
        return true;
    }
    private static bool Ipv6(ReadOnlySpan<char> input, ISchemaOperation op, out XmlDatatypeError? error)
    {
        error = null; int groups = 0, digits = 0, start = 0; bool compressed = false;
        for (int i = 0; i < input.Length; i++)
        {
            if (!op.Charge(1, default)) return TextBinaryDatatypeProvider.Stop(out error);
            char c = input[i];
            if (c == '.')
            {
                if (!Ipv4(input[start..], op, out error)) return false;
                groups += 2; digits = 0; start = input.Length; break;
            }
            if (c == ':')
            {
                if (digits != 0) { groups++; digits = 0; }
                else if (i != 0) return TextBinaryDatatypeProvider.Invalid(out error);
                if (i + 1 < input.Length && input[i + 1] == ':')
                {
                    if (!op.Charge(1, default)) return TextBinaryDatatypeProvider.Stop(out error);
                    if (compressed) return TextBinaryDatatypeProvider.Invalid(out error);
                    compressed = true; i++;
                }
                else if (i == 0 || i == input.Length - 1) return TextBinaryDatatypeProvider.Invalid(out error);
                start = i + 1;
            }
            else if (TextBinaryDatatypeProvider.HexDigit(c) < 0 || ++digits > 4) return TextBinaryDatatypeProvider.Invalid(out error);
        }
        if (digits != 0) groups++;
        if (input.IsEmpty || (compressed ? groups >= 8 : groups != 8)) return TextBinaryDatatypeProvider.Invalid(out error);
        return true;
    }
    private static bool Ipv4(ReadOnlySpan<char> input, ISchemaOperation op, out XmlDatatypeError? error)
    {
        error = null; int groups = 0, digits = 0, number = 0;
        foreach (char c in input)
        {
            if (!op.Charge(1, default)) return TextBinaryDatatypeProvider.Stop(out error);
            if (c == '.') { if (digits == 0 || ++groups > 3) return TextBinaryDatatypeProvider.Invalid(out error); digits = number = 0; }
            else if (c is < '0' or > '9' || ++digits > 3 || (number = number * 10 + c - '0') > 255) return TextBinaryDatatypeProvider.Invalid(out error);
        }
        return groups == 3 && digits != 0 || TextBinaryDatatypeProvider.Invalid(out error);
    }
}

namespace Xemele.Html;
internal static class HtmlAscii
{
    internal static bool Equals(ReadOnlySpan<char> text, ReadOnlySpan<char> expected)
    {
        if (text.Length != expected.Length) return false;
        for (int i = 0; i < text.Length; i++)
        {
            char a = text[i], b = expected[i]; if (a is >= 'A' and <= 'Z') a += (char)32; if (b is >= 'A' and <= 'Z') b += (char)32;
            if (a != b) return false;
        }
        return true;
    }
    internal static bool StartsWith(ReadOnlySpan<char> text, ReadOnlySpan<char> expected) => text.Length >= expected.Length && Equals(text[..expected.Length], expected);
    internal static int IndexOf(ReadOnlySpan<char> text, ReadOnlySpan<char> expected) { for (int i = 0; i <= text.Length - expected.Length; i++) if (Equals(text.Slice(i, expected.Length), expected)) return i; return -1; }
}

using System.Buffers;
using System.Text;
namespace Xemele;
internal static class TreeValidation
{
    internal const string XmlUri = "http://www.w3.org/XML/1998/namespace";
    internal const string XmlnsUri = "http://www.w3.org/2000/xmlns/";
    internal static void Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ReadOnlySpan<char> rest = value;
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out var rune, out int length) != OperationStatus.Done ||
                !(rune.Value is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF))
                throw new ArgumentException("Illegal XML scalar.", nameof(value));
            rest = rest[length..];
        }
    }
    private static bool Start(int c) => c is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
        >= 0xC0 and <= 0xD6 or >= 0xD8 and <= 0xF6 or >= 0xF8 and <= 0x2FF or >= 0x370 and <= 0x37D or
        >= 0x37F and <= 0x1FFF or >= 0x200C and <= 0x200D or >= 0x2070 and <= 0x218F or
        >= 0x2C00 and <= 0x2FEF or >= 0x3001 and <= 0xD7FF or >= 0xF900 and <= 0xFDCF or
        >= 0xFDF0 and <= 0xFFFD or >= 0x10000 and <= 0xEFFFF;
    internal static void Name(string name, bool colon = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0) throw new ArgumentException("Empty XML name.", nameof(name));
        ReadOnlySpan<char> rest = name;
        bool first = true;
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out var rune, out int length) != OperationStatus.Done)
                throw new ArgumentException("Invalid XML name.", nameof(name));
            int c = rune.Value;
            if (!(Start(c) || (colon && c == ':') || (!first && (c is '-' or '.' or >= '0' and <= '9' or 0xB7 or >= 0x300 and <= 0x36F or >= 0x203F and <= 0x2040))))
                throw new ArgumentException("Invalid XML name.", nameof(name));
            first = false; rest = rest[length..];
        }
    }
    internal static void Binding(string prefix, string uri)
    {
        ArgumentNullException.ThrowIfNull(prefix); ArgumentNullException.ThrowIfNull(uri);
        if (prefix.Length != 0) Name(prefix);
        Text(uri);
        if (prefix == "xmlns" || uri == XmlnsUri || (prefix == "xml") != (uri == XmlUri) || (prefix.Length != 0 && uri.Length == 0))
            throw new ArgumentException("Reserved or empty namespace binding.");
    }
    internal static void Expanded(string local, string uri, string prefix)
    {
        Name(local); Binding(prefix, uri);
        if (local == "xmlns") throw new ArgumentException("xmlns is a reserved name.", nameof(local));
    }
    internal static void Value(XmlNodeKind kind, string value)
    {
        Text(value);
        if ((kind == XmlNodeKind.Comment && (value.Contains("--", StringComparison.Ordinal) || value.EndsWith('-'))) ||
            (kind == XmlNodeKind.CData && value.Contains("]]>", StringComparison.Ordinal)) ||
            (kind == XmlNodeKind.ProcessingInstruction && value.Contains("?>", StringComparison.Ordinal)))
            throw new ArgumentException("Illegal XML delimiter.", nameof(value));
    }
}

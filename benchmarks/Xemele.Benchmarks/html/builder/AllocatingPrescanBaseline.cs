// Mechanically retained Sniff/attribute baseline from95b1952; only1024window clamp added for identical semantics.
namespace Xemele.Html;

// Transport and decoder buffers have one operation owner; no full input copy is
// made. The sniff window is at most 1024 original bytes for either public lane.
internal static class AllocatingPrescanBaseline
{
    internal static (string Encoding, int Bom) Sniff(ReadOnlySpan<byte> input)
    {
        input = input[..Math.Min(1024, input.Length)];
        if (input.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return ("utf-8", 3);
        }

        if (input.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return ("utf-16be", 2);
        }

        if (input.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return ("utf-16le", 2);
        }

        var position = 0;
        while (position < input.Length)
        {
            if (input[position] != '<')
            {
                position++;
                continue;
            }
            if (input[position..].StartsWith("<!--"u8))
            {
                var end = input[(position + 4)..].IndexOf("-->"u8);
                if (end < 0)
                {
                    break;
                }

                position += end + 7;
                continue;
            }
            if (Match(input[position..], "<meta"u8) && position + 5 < input.Length && SpaceOrSlash(input[position + 5]))
            {
                position += 5;
                bool gotPragma = false, needPragma = false;
                string? charset = null;
                var seen = new HashSet<string>(StringComparer.Ordinal); // bounded sniff-window attribute names only.
                while (Attribute(input, ref position, out var name, out var value))
                {
                    if (!seen.Add(name))
                    {
                        continue;
                    }

                    if (name == "http-equiv" && EqualsAscii(value.AsSpan(), "content-type"))
                    {
                        gotPragma = true;
                    }

                    if (name == "content" && charset is null)
                    {
                        charset = FromContent(value.AsSpan());
                        if (charset is not null)
                        {
                            needPragma = true;
                        }
                    }
                    if (name == "charset")
                    {
                        charset = HtmlEncodingTables.Label(value.AsSpan());
                        needPragma = false;
                    }
                }
                if (charset is null || needPragma && !gotPragma || charset == "replacement")
                {
                    continue;
                }

                if (charset is "utf-16be" or "utf-16le")
                {
                    charset = "utf-8";
                }

                if (charset == "x-user-defined")
                {
                    charset = "windows-1252";
                }

                return (charset, 0);
            }
            if (position + 1 < input.Length && (Letter(input[position + 1]) || input[position + 1] == '/' && position + 2 < input.Length && Letter(input[position + 2])))
            {
                position++;
                while (position < input.Length && !SpaceOrSlash(input[position]) && input[position] != '>')
                {
                    position++;
                }

                while (Attribute(input, ref position, out _, out _))
                {
                }
                continue;
            }
            if (position + 1 < input.Length && input[position + 1] is (byte)'!' or (byte)'/' or (byte)'?')
            {
                var end = input[position..].IndexOf((byte)'>');
                if (end < 0)
                {
                    break;
                }

                position += end + 1;
                continue;
            }
            position++;
        }
        return ("windows-1252", 0);
    }
    private static bool Attribute(ReadOnlySpan<byte> data, ref int p, out string name, out string value)
    {
        name = value = "";
        while (p < data.Length && SpaceOrSlash(data[p]))
        {
            p++;
        }

        if (p == data.Length || data[p] == '>')
        {
            if (p < data.Length)
            {
                p++;
            }

            return false;
        }
        var start = p;
        while (p < data.Length && !SpaceOrSlash(data[p]) && data[p] is not ((byte)'=' or (byte)'>'))
        {
            p++;
        }

        name = Ascii(data[start..p]);
        while (p < data.Length && Space(data[p]))
        {
            p++;
        }

        if (p == data.Length)
        {
            return false;
        }

        if (data[p] != '=')
        {
            return true;
        }

        p++;
        while (p < data.Length && Space(data[p]))
        {
            p++;
        }

        if (p == data.Length)
        {
            return false;
        }

        var quote = data[p];
        if (quote is (byte)'\'' or (byte)'"')
        {
            start = ++p;
            while (p < data.Length && data[p] != quote)
            {
                p++;
            }

            if (p == data.Length)
            {
                return false;
            }

            value = Ascii(data[start..p]);
            p++;
        }
        else
        {
            start = p;
            while (p < data.Length && !Space(data[p]) && data[p] != '>')
            {
                p++;
            }

            if (p == data.Length)
            {
                return false;
            }

            value = Ascii(data[start..p]);
        }
        return true;
    }
    private static string? FromContent(ReadOnlySpan<char> content)
    {
        while (true)
        {
            var found = HtmlAscii.IndexOf(content, "charset");
            if (found < 0)
            {
                return null;
            }

            content = content[(found + 7)..].TrimStart(" \t\n\r\f".AsSpan());
            if (content.IsEmpty)
            {
                return null;
            }

            if (content[0] != '=')
            {
                continue;
            }

            content = content[1..].TrimStart(" \t\n\r\f".AsSpan());
            if (content.IsEmpty)
            {
                return null;
            }

            if (content[0] is '\'' or '"')
            {
                var end = content[1..].IndexOf(content[0]);
                if (end < 0)
                {
                    return null;
                }

                return HtmlEncodingTables.Label(content.Slice(1, end));
            }
            var length = 0;
            while (length < content.Length && content[length] is not ('\t' or '\n' or '\r' or '\f' or ' ' or ';'))
            {
                length++;
            }

            return HtmlEncodingTables.Label(content[..length]);
        }
    }
    private static string Ascii(ReadOnlySpan<byte> bytes)
    {
        return string.Create(bytes.Length, bytes, static (output, input) => { for (var i = 0; i < input.Length; i++) { output[i] = (char)(input[i] is >= 65 and <= 90 ? input[i] + 32 : input[i]); } });
    }
    private static bool Match(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        if (input.Length < literal.Length)
        {
            return false;
        }

        for (var i = 0; i < literal.Length; i++)
        {
            var c = input[i];
            if (c is >= 65 and <= 90)
            {
                c += 32;
            }

            if (c != literal[i])
            {
                return false;
            }
        }
        return true;
    }
    private static bool EqualsAscii(ReadOnlySpan<char> input, string literal) => HtmlAscii.Equals(input, literal);
    private static bool Space(byte c) => c is 9 or 10 or 12 or 13 or 32;
    private static bool SpaceOrSlash(byte c) => Space(c) || c == '/';
    private static bool Letter(byte c) => c is >= 65 and <= 90 or >= 97 and <= 122;
}

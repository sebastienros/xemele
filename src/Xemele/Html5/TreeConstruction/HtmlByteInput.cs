using System.Buffers;

namespace Xemele.Html;

// Transport and decoder buffers have one operation owner; no full input copy is
// made. The sniff window is at most 1024 original bytes for either public lane.
internal static class HtmlByteInput
{
    internal static void Parse(ReadOnlySpan<byte> input, HtmlTokenizer tokenizer)
    {
        var (encoding, bom) = Sniff(input[..Math.Min(1024, input.Length)]);
        using var decoder = new HtmlByteDecoder(encoding, tokenizer);
        decoder.Feed(input[bom..], bom);
        decoder.Complete(input.Length);
        tokenizer.Complete(input.Length);
    }
    internal static void Load(Stream stream, HtmlTokenizer tokenizer, CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            var prefix = 0;
            while (prefix < 1024)
            {
                cancellation.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, prefix, 1024 - prefix);
                if (read == 0)
                {
                    break;
                }

                prefix += read;
            }
            var (encoding, bom) = Sniff(buffer.AsSpan(0, prefix));
            using var decoder = new HtmlByteDecoder(encoding, tokenizer);
            decoder.Feed(buffer.AsSpan(bom, prefix - bom), bom);
            long offset = prefix;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, 0, 8192);
                if (read == 0)
                {
                    break;
                }

                decoder.Feed(buffer.AsSpan(0, read), offset);
                offset = checked(offset + read);
            }
            decoder.Complete(offset);
            tokenizer.Complete(offset);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    internal static (string Encoding, int Bom) Sniff(ReadOnlySpan<byte> input)
    {
        input = input[..Math.Min(1024, input.Length)];
        Span<Range> seen = stackalloc Range[512];
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
                var seenCount = 0; // At most 512 nonempty attribute names fit in the borrowed 1024-byte window.
                while (Attribute(input, ref position, out var name, out var value))
                {
                    var duplicate = false;
                    for (var i = 0; i < seenCount; i++)
                    {
                        if (HtmlAscii.Equals(input[seen[i]], input[name]))
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (duplicate)
                    {
                        continue;
                    }

                    seen[seenCount++] = name;
                    if (HtmlAscii.Equals(input[name], "http-equiv"u8) && HtmlAscii.Equals(input[value], "content-type"u8))
                    {
                        gotPragma = true;
                    }

                    if (HtmlAscii.Equals(input[name], "content"u8) && charset is null)
                    {
                        charset = FromContent(input[value]);
                        if (charset is not null)
                        {
                            needPragma = true;
                        }
                    }
                    if (HtmlAscii.Equals(input[name], "charset"u8))
                    {
                        charset = HtmlEncodingTables.Label(input[value]);
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
    private static bool Attribute(ReadOnlySpan<byte> data, ref int p, out Range name, out Range value)
    {
        name = value = 0..0;
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

        name = start..p;
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

            value = start..p;
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

            value = start..p;
        }
        return true;
    }
    private static string? FromContent(ReadOnlySpan<byte> content)
    {
        while (true)
        {
            var found = -1;
            for (var i = 0; i <= content.Length - 7; i++)
            {
                if (HtmlAscii.Equals(content.Slice(i, 7), "charset"u8))
                {
                    found = i;
                    break;
                }
            }

            if (found < 0)
            {
                return null;
            }

            content = TrimStart(content[(found + 7)..]);
            if (content.IsEmpty)
            {
                return null;
            }

            if (content[0] != '=')
            {
                continue;
            }

            content = TrimStart(content[1..]);
            if (content.IsEmpty)
            {
                return null;
            }

            if (content[0] is (byte)'\'' or (byte)'"')
            {
                var end = content[1..].IndexOf(content[0]);
                if (end < 0)
                {
                    return null;
                }

                return HtmlEncodingTables.Label(content.Slice(1, end));
            }
            var length = 0;
            while (length < content.Length && !Space(content[length]) && content[length] != ';')
            {
                length++;
            }

            return HtmlEncodingTables.Label(content[..length]);
        }
    }
    private static ReadOnlySpan<byte> TrimStart(ReadOnlySpan<byte> input)
    {
        var i = 0;
        while (i < input.Length && Space(input[i]))
        {
            i++;
        }

        return input[i..];
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
    private static bool Space(byte c) => c is 9 or 10 or 12 or 13 or 32;
    private static bool SpaceOrSlash(byte c) => Space(c) || c == '/';
    private static bool Letter(byte c) => c is >= 65 and <= 90 or >= 97 and <= 122;
}

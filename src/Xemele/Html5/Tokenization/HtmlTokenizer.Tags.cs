using System.Text;

namespace Xemele.Html;

// Whole-tag fast path for the Data state. A tag is consumed here only when the complete tag lies in
// the current input and every scalar of it takes the error-free path of the state machine; any other
// shape (errors, references, CR, control or non-ASCII markup characters, duplicate attributes, quota or
// cancellation boundaries, a tag split across chunks) is left untouched for the per-scalar path.
internal sealed partial class HtmlTokenizer
{
    private static readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> KnownNames = CreateKnownNames();

    private static Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> CreateKnownNames()
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in (ReadOnlySpan<string>)[
            "a", "abbr", "address", "area", "article", "aside", "audio", "b", "base", "bdi", "bdo", "big", "blockquote", "body", "br", "button",
            "canvas", "caption", "center", "cite", "code", "col", "colgroup", "data", "datalist", "dd", "del", "details", "dfn", "dialog", "div",
            "dl", "dt", "em", "embed", "fieldset", "figcaption", "figure", "font", "footer", "form", "frame", "frameset", "g", "h1", "h2", "h3",
            "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "i", "iframe", "img", "input", "ins", "kbd", "label", "legend", "li",
            "link", "main", "map", "mark", "math", "menu", "meta", "meter", "nav", "nobr", "noframes", "noscript", "object", "ol", "optgroup",
            "option", "output", "p", "param", "path", "picture", "pre", "progress", "q", "rp", "rt", "ruby", "s", "samp", "script", "section",
            "select", "slot", "small", "source", "span", "strike", "strong", "style", "sub", "summary", "sup", "svg", "table", "tbody", "td",
            "template", "textarea", "tfoot", "th", "thead", "time", "title", "tr", "track", "tt", "u", "ul", "var", "video", "wbr",
            "action", "alt", "aria-label", "aria-hidden", "async", "autocomplete", "border", "cellpadding", "cellspacing", "charset", "checked",
            "class", "cols", "colspan", "content", "dir", "disabled", "height", "href", "http-equiv", "id", "lang", "media", "method", "name",
            "onclick", "placeholder", "rel", "role", "rows", "rowspan", "selected", "size", "src", "srcset", "style", "tabindex", "target",
            "type", "value", "width", "defer", "for", "data-id", "data-role", "data-index", "data-value", "data-type", "xmlns",
        ])
        {
            table.TryAdd(name, name);
        }

        return table.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    // Lower-cases an ASCII name and returns the shared instance for well-known names.
    private static string NameString(ReadOnlySpan<char> name)
    {
        var buffer = name.Length <= 64 ? stackalloc char[64] : new char[name.Length];
        var lower = buffer[..name.Length];
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            lower[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
        }
        return KnownNames.TryGetValue(lower, out var known) ? known : new string(lower);
    }

    private static bool TagNameChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or ':' or '.';
    private static bool SpaceChar(char c) => c is '\t' or '\n' or '\f' or ' ';

    private bool TryReference(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets, ref int index)
    {
        var rest = input[index..];
        if (rest.Length < 3)
        {
            return false;
        }

        string? named = null;
        int consumed, scalars;
        var number = 0;
        if (rest[1] == '#')
        {
            int p = 2, radix = 10;
            if (rest[p] is 'x' or 'X')
            {
                radix = 16;
                p++;
            }
            var digitsStart = p;
            while (p < rest.Length && p - digitsStart <= 7 && Digit(rest[p], radix) >= 0)
            {
                number = number * radix + Digit(rest[p], radix);
                p++;
            }
            if (p == digitsStart || p - digitsStart > 7 || p >= rest.Length || rest[p] != ';')
            {
                return false;
            }

            if (number is 0 or 13 or > 0x10FFFF or (>= 1 and <= 8) or 11 or (>= 14 and <= 31) or (>= 0x7F and <= 0x9F) or (>= 0xD800 and <= 0xDFFF) or (>= 0xFDD0 and <= 0xFDEF) || (number & 0xFFFF) is 0xFFFE or 0xFFFF)
            {
                return false;
            }

            consumed = p + 1;
            scalars = 1;
        }
        else
        {
            var p = 1;
            while (p < rest.Length && p <= 32 && Alnum(rest[p]))
            {
                p++;
            }

            if (p == 1 || p >= rest.Length || rest[p] != ';')
            {
                return false;
            }

            consumed = p + 1;
            if (!HtmlNamedReferences.Match(rest[1..consumed], out var matched, out named) || matched != consumed - 1)
            {
                return false;
            }

            scalars = 0;
            for (var i = 0; i < named.Length; i++)
            {
                scalars++;
                if (char.IsHighSurrogate(named[i]) && i + 1 < named.Length && char.IsLowSurrogate(named[i + 1]))
                {
                    i++;
                }
            }
        }
        var units = named is null ? (number > 0xFFFF ? 2 : 1) : named.Length;
        var flushAt = limits.TokenCharacters is int textCap ? Math.Min(1024, textCap) : 1024;
        if (characters.Length + units >= flushAt)
        {
            return false;
        }

        if (limits.DocumentCharacters is long document && documentScalars + consumed > document)
        {
            return false;
        }

        if (limits.ExpandedCharacters is long expanded && expandedScalars + scalars > expanded)
        {
            return false;
        }

        if (cancellation.CanBeCanceled && (cancellationTicks & 255) + consumed > 255)
        {
            return false;
        }

        if (!SuppressLocations && characters.Length == 0)
        {
            characterOpening = new(SourceUri, line, column, byteOffsets.IsEmpty ? null : byteOffsets[index]);
        }

        if (named is not null)
        {
            characters.Append(named);
        }
        else
        {
            characters.AppendScalar(number);
        }

        expandedScalars += scalars;
        documentScalars += consumed;
        cancellationTicks = unchecked(cancellationTicks + consumed);
        column = checked(column + consumed);
        if (!SuppressLocations)
        {
            current = new(SourceUri, line, column - 1, byteOffsets.IsEmpty ? null : byteOffsets[index + consumed - 1]);
        }
        index += consumed;
        return true;
    }

    private bool TryTag(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets, ref int index)
    {
        var rest = input[index..];
        if (rest.Length < 3)
        {
            return false;
        }

        var end = rest[1] == '/';
        var p = end ? 2 : 1;
        if (p >= rest.Length || !Alpha(rest[p]))
        {
            return false;
        }

        var nameStart = p;
        while (p < rest.Length && TagNameChar(rest[p]))
        {
            p++;
        }

        var nameLength = p - nameStart;
        var tokenCap = limits.TokenCharacters;
        if (tokenCap is int nameCap && nameLength > nameCap)
        {
            return false;
        }

        var attributeCap = limits.AttributesPerElement;
        long attributeChars = 0, valueChars = 0;
        var selfClosing = false;
        attributes.Clear();
        while (true)
        {
            while (p < rest.Length && SpaceChar(rest[p]))
            {
                p++;
            }

            if (p >= rest.Length)
            {
                goto fail;
            }

            var c = rest[p];
            if (c == '>')
            {
                p++;
                break;
            }
            if (c == '/')
            {
                if (end || p + 1 >= rest.Length || rest[p + 1] != '>')
                {
                    goto fail;
                }

                selfClosing = true;
                p += 2;
                break;
            }
            if (end || !TagNameChar(c))
            {
                goto fail;
            }

            var attributeStart = p;
            while (p < rest.Length && TagNameChar(rest[p]))
            {
                p++;
            }

            if (p >= rest.Length)
            {
                goto fail;
            }

            var attributeName = NameString(rest[attributeStart..p]);
            foreach (var existing in attributes)
            {
                if ((object)existing.LocalName == attributeName || existing.LocalName == attributeName)
                {
                    goto fail;
                }
            }

            if (attributeCap is int maxAttributes && attributes.Count >= maxAttributes)
            {
                goto fail;
            }

            attributeChars += p - attributeStart;
            var q = p;
            while (q < rest.Length && SpaceChar(rest[q]))
            {
                q++;
            }

            if (q >= rest.Length)
            {
                goto fail;
            }

            if (rest[q] != '=')
            {
                attributes.Add(new(attributeName, ""));
                p = q;
                continue;
            }
            q++;
            while (q < rest.Length && SpaceChar(rest[q]))
            {
                q++;
            }

            if (q >= rest.Length)
            {
                goto fail;
            }

            ReadOnlySpan<char> value;
            var quote = rest[q];
            if (quote is '"' or '\'')
            {
                var tail = rest[(q + 1)..];
                var stop = tail.IndexOfAny(quote == '"' ? DoubleStops : SingleStops);
                if (stop < 0 || tail[stop] != quote)
                {
                    goto fail;
                }

                value = tail[..stop];
                p = q + 1 + stop + 1;
                if (p >= rest.Length || !(SpaceChar(rest[p]) || rest[p] is '/' or '>'))
                {
                    goto fail;
                }
            }
            else
            {
                var stop = rest[q..].IndexOfAny(UnquotedStops);
                if (stop <= 0 || !(SpaceChar(rest[q + stop]) || rest[q + stop] == '>'))
                {
                    goto fail;
                }

                value = rest.Slice(q, stop);
                p = q + stop;
            }
            if (PlainLength(value) != value.Length)
            {
                goto fail;
            }

            attributeChars += value.Length;
            valueChars += value.Length;
            attributes.Add(new(attributeName, value.Length == 0 ? "" : new string(value)));
        }
        {
            var length = p;
            if (tokenCap is int cap && attributeChars > cap)
            {
                goto fail;
            }

            if (limits.DocumentCharacters is long document && documentScalars + length > document)
            {
                goto fail;
            }

            if (limits.ExpandedCharacters is long expanded && expandedScalars + valueChars > expanded)
            {
                goto fail;
            }

            if (cancellation.CanBeCanceled && (cancellationTicks & 255) + length > 255)
            {
                goto fail;
            }

            var span = rest[..length];
            if (!SuppressLocations)
            {
                opening = new(SourceUri, line, column, byteOffsets.IsEmpty ? null : byteOffsets[index]);
            }
            var ownedName = NameString(span.Slice(nameStart, nameLength));
            documentScalars += length;
            expandedScalars += valueChars;
            cancellationTicks = unchecked(cancellationTicks + length);
            var lastLine = span.LastIndexOf('\n');
            if (lastLine < 0)
            {
                column = checked(column + length);
            }
            else
            {
                line = checked(line + span.Count('\n'));
                column = length - lastLine;
            }
            if (!SuppressLocations)
            {
                current = new(SourceUri, line, column - 1, byteOffsets.IsEmpty ? null : byteOffsets[index + length - 1]);
            }
            index += length;
            this.selfClosing = selfClosing;
            endTag = end;
            attributeScalars = attributeChars;
            if (!end)
            {
                lastStartTag = ownedName;
            }

            FlushCharacters();
            if (treeBuilder is not null)
            {
                treeBuilder.Process(new HtmlTreeBuilder.Token(end ? HtmlTokenKind.EndTag : HtmlTokenKind.StartTag, ownedName, Attributes: attributes, SelfClosing: selfClosing));
                attributes.Clear();
            }
            else
            {
                var owned = attributes.Count == 0 ? null : attributes.ToArray();
                attributes.Clear();
                callback!(HtmlToken.Trusted(end ? HtmlTokenKind.EndTag : HtmlTokenKind.StartTag, ownedName, "", owned, selfClosing, null, opening));
            }
            return true;
        }
    fail:
        attributes.Clear();
        return false;
    }
}
